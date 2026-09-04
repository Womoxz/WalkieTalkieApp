using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WalkieTalkieApp
{
    public class ContactDiscoveredEventArgs : EventArgs
    {
        public string Name { get; init; } = string.Empty;
        public string Ip { get; init; } = string.Empty;
        /// <summary>true si no estaba en la lista; false si solo cambió su IP.</summary>
        public bool IsNew { get; init; }
    }

    /// <summary>
    /// Descubrimiento automático de equipos en la red local.
    ///
    /// Mantiene el mismo formato de mensajes que la versión instalada en
    /// C:\WalkieTalkie ("DISCOVER;nombre" y "RESPONSE;nombre" por broadcast en el
    /// puerto 5001), así que los equipos que todavía tengan aquella versión se
    /// siguen viendo con los que tengan esta.
    ///
    /// Mejoras sobre aquella implementación:
    ///  - La IP local se calcula una vez y se cachea; antes se abría un socket
    ///    nuevo por cada datagrama recibido solo para descartar los propios.
    ///  - Los contactos descubiertos se guardan en appsettings.json, así que la
    ///    lista sigue ahí aunque arranques con todos los demás equipos apagados.
    ///  - De los mismos latidos sale el estado "en línea / sin conexión", sin
    ///    tráfico adicional.
    ///  - Avisa al salir ("BYE;nombre") para que los demás lo marquen al instante.
    ///    Las versiones antiguas ignoran ese mensaje sin problemas.
    /// </summary>
    public class DiscoveryService : IDisposable
    {
        private const string MsgDiscover = "DISCOVER";
        private const string MsgResponse = "RESPONSE";
        private const string MsgBye = "BYE";

        private const int AnnounceIntervalMs = 4000;
        private const int OfflineTimeoutMs = 13000;   // ~3 latidos perdidos
        private const int SaveDebounceMs = 5000;

        private readonly AppConfig config;
        private readonly string userName;

        private UdpClient? socket;
        private Thread? listenThread;
        private System.Threading.Timer? announceTimer;
        private System.Threading.Timer? presenceTimer;
        private System.Threading.Timer? saveTimer;
        private readonly List<System.Threading.Timer> arranque = new();
        private volatile bool running;

        private readonly ConcurrentDictionary<string, DateTime> lastSeen =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, bool> onlineState =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly object saveLock = new();
        private bool pendingSave;

        public event EventHandler<ContactDiscoveredEventArgs>? ContactDiscovered;
        public event EventHandler<PresenceEventArgs>? PresenceChanged;

        public bool IsRunning => running;

        public DiscoveryService(AppConfig config, string userName)
        {
            this.config = config;
            this.userName = userName;
        }

        public bool IsOnline(string contact) =>
            onlineState.TryGetValue(contact, out bool v) && v;

        public void Start()
        {
            if (!config.General.DescubrimientoAutomatico) return;

            try
            {
                socket = new UdpClient();
                socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, config.General.PuertoDescubrimiento));
                socket.EnableBroadcast = true;

                running = true;

                listenThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "WalkieTalkie-Discovery"
                };
                listenThread.Start();

                announceTimer = new System.Threading.Timer(
                    _ => Announce(MsgDiscover), null, 0, AnnounceIntervalMs);

                // Ráfaga de arranque: varios avisos seguidos en los primeros
                // segundos. UDP pierde paquetes y los demás equipos pueden estar
                // arrancando a la vez; así la lista se pinta en verde enseguida
                // en vez de esperar al siguiente ciclo de 4 segundos.
                foreach (int ms in new[] { 250, 700, 1500, 3000 })
                {
                    var t = new System.Threading.Timer(_ => Announce(MsgDiscover), null,
                                                       ms, Timeout.Infinite);
                    arranque.Add(t);
                }

                presenceTimer = new System.Threading.Timer(
                    _ => CheckPresence(), null, AnnounceIntervalMs, 2000);
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.NetworkLog, $"No se pudo iniciar el descubrimiento: {ex.Message}");
                running = false;
            }
        }

        private void Announce(string kind)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes($"{kind};{userName}");
                socket?.Send(data, data.Length,
                    new IPEndPoint(IPAddress.Broadcast, config.General.PuertoDescubrimiento));
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.NetworkLog, $"Anuncio de descubrimiento: {ex.Message}");
            }
        }

        private void ListenLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);

            while (running)
            {
                try
                {
                    byte[] data = socket!.Receive(ref remote);

                    string[] parts = Encoding.UTF8.GetString(data).Split(';');
                    if (parts.Length != 2) continue;

                    string kind = parts[0];
                    string name = parts[1].Trim();
                    string ip = remote.Address.ToString();

                    // Basta con descartar los mensajes que llevan nuestro propio nombre:
                    // eso ya elimina el eco del broadcast. La versión anterior además
                    // descartaba por IP de origen, lo que impedía que dos usuarios
                    // distintos se vieran desde el mismo equipo (o desde máquinas
                    // virtuales que comparten dirección) y abría un socket nuevo en
                    // cada datagrama solo para averiguar la IP propia.
                    if (name.Length == 0 ||
                        string.Equals(name, userName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    switch (kind)
                    {
                        case MsgDiscover:
                            // Contestar directamente a quien pregunta (no por broadcast).
                            byte[] reply = Encoding.UTF8.GetBytes($"{MsgResponse};{userName}");
                            try { socket.Send(reply, reply.Length, remote); } catch { }
                            RegisterContact(name, ip);
                            break;

                        case MsgResponse:
                            RegisterContact(name, ip);
                            break;

                        case MsgBye:
                            lastSeen.TryRemove(name, out _);
                            SetOnline(name, false);
                            break;
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppPaths.Log(AppPaths.NetworkLog, $"Escucha de descubrimiento: {ex}");
                }
            }
        }

        private void RegisterContact(string name, string ip)
        {
            lastSeen[name] = DateTime.UtcNow;

            bool isNew = false;
            bool changed = false;

            lock (config.Contactos)
            {
                if (!config.Contactos.TryGetValue(name, out string? known))
                {
                    config.Contactos[name] = ip;
                    isNew = true;
                    changed = true;
                }
                else if (known != ip)
                {
                    // El DHCP le dio otra IP: se corrige sola.
                    config.Contactos[name] = ip;
                    changed = true;
                }
            }

            SetOnline(name, true);

            if (changed)
            {
                ContactDiscovered?.Invoke(this, new ContactDiscoveredEventArgs
                {
                    Name = name,
                    Ip = ip,
                    IsNew = isNew
                });

                ScheduleSave();
            }
        }

        private void SetOnline(string name, bool online)
        {
            if (onlineState.TryGetValue(name, out bool previous) && previous == online) return;

            onlineState[name] = online;
            PresenceChanged?.Invoke(this, new PresenceEventArgs { Contact = name, Online = online });
        }

        private void CheckPresence()
        {
            try
            {
                foreach (var contact in config.ContactosExternos(userName))
                {
                    bool online = lastSeen.TryGetValue(contact.Key, out var seen) &&
                                  (DateTime.UtcNow - seen).TotalMilliseconds < OfflineTimeoutMs;

                    SetOnline(contact.Key, online);
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.NetworkLog, $"Presencia: {ex.Message}");
            }
        }

        /// <summary>
        /// Guarda con retardo: en una red con varios equipos llegarían muchos
        /// descubrimientos seguidos y no tiene sentido reescribir el archivo en cada uno.
        /// </summary>
        private void ScheduleSave()
        {
            if (!config.General.GuardarContactosDescubiertos) return;

            lock (saveLock)
            {
                pendingSave = true;
                saveTimer ??= new System.Threading.Timer(_ => FlushSave(), null,
                    Timeout.Infinite, Timeout.Infinite);
                saveTimer.Change(SaveDebounceMs, Timeout.Infinite);
            }
        }

        private void FlushSave()
        {
            lock (saveLock)
            {
                if (!pendingSave) return;
                pendingSave = false;
            }

            try
            {
                config.Save();
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.NetworkLog, $"Guardado de contactos: {ex.Message}");
            }
        }

        public void Dispose()
        {
            running = false;

            try { if (socket != null) Announce(MsgBye); } catch { }

            announceTimer?.Dispose();
            presenceTimer?.Dispose();
            foreach (var t in arranque) t.Dispose();
            arranque.Clear();

            FlushSave();
            saveTimer?.Dispose();

            try { socket?.Close(); } catch { }
            socket?.Dispose();
            socket = null;

            try { listenThread?.Join(400); } catch { }
        }
    }
}
