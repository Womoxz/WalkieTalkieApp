#pragma warning disable CA1416 // API sólo de Windows: la app es WinForms/Windows

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WalkieTalkieApp
{
    public class ReceptionEventArgs : EventArgs
    {
        public string Contact { get; init; } = string.Empty;
        public AudioItem? Item { get; init; }
    }

    public class PresenceEventArgs : EventArgs
    {
        public string Contact { get; init; } = string.Empty;
        public bool Online { get; init; }
    }

    /// <summary>
    /// Captura, envío, recepción y reproducción. Todo lo que antes vivía mezclado
    /// dentro de MainForm.
    ///
    /// Cambios de fondo respecto a la versión anterior:
    ///  - Un único socket UDP para enviar y recibir (antes se creaba un UdpClient
    ///    nuevo en cada pulsación de PTT y jamás se liberaba: una fuga por transmisión).
    ///  - El micrófono se abre una vez y se mantiene abierto, así no se pierde el
    ///    arranque de cada frase.
    ///  - Los audios recibidos se escriben directo a disco y se cierra el writer
    ///    ANTES de publicarlos, para que la cabecera RIFF quede con el tamaño correcto.
    ///  - Mezclador por remitente: si dos personas hablan a la vez ya no se pisan.
    ///  - Latidos de presencia para saber quién está en línea.
    /// </summary>
    public class AudioEngine : IDisposable
    {
        private readonly AppConfig config;
        private readonly string userName;

        private UdpClient? socket;
        private Thread? receiveThread;
        private volatile bool running;

        // --- Captura ---
        private WaveInEvent? waveIn;
        private WaveFileWriter? sendWriter;
        private string? sendTempPath;
        private List<string> sendContacts = new();
        private DateTime sendStarted;
        private volatile bool transmitting;
        private readonly object txLock = new();
        private System.Threading.Timer? maxTxTimer;

        // --- Reproducción en vivo ---
        private WaveOutEvent? livePlayer;
        private MixingSampleProvider? mixer;
        private VolumeSampleProvider? volumeControl;
        private readonly Dictionary<string, BufferedWaveProvider> liveBuffers = new();

        // --- Recepción a disco ---
        private class Reception
        {
            public WaveFileWriter Writer = null!;
            public string TempPath = string.Empty;
            public string Contact = string.Empty;
            public DateTime Started;
            public bool EsGrupo;
            public System.Threading.Timer? IdleTimer;
        }
        private readonly Dictionary<string, Reception> receptions = new();
        private readonly object rxLock = new();

        // La presencia (quién está en línea) la lleva DiscoveryService: sus latidos
        // de descubrimiento ya sirven para eso y así no se duplica el tráfico.

        // --- Reproducción de archivos ---
        private WaveOutEvent? filePlayer;
        private WaveFileReader? fileReader;
        private readonly object playLock = new();

        // --- Avisos sonoros (sin fugas de handlers) ---
        private readonly HashSet<WaveOutEvent> cuePlayers = new();

        /// <summary>Destinatarios de la transmisión que acaba de empezar.</summary>
        public event EventHandler<IReadOnlyList<string>>? TransmissionStarted;

        /// <summary>Una entrada de historial por destinatario (mismo archivo).</summary>
        public event EventHandler<IReadOnlyList<AudioItem>>? TransmissionEnded;
        public event EventHandler<ReceptionEventArgs>? ReceptionStarted;
        public event EventHandler<ReceptionEventArgs>? ReceptionEnded;
        public event EventHandler<float>? InputLevel;
        public event EventHandler? PlaybackFinished;
        public event EventHandler<string>? EngineError;

        public bool IsTransmitting => transmitting;
        public bool IsPlayingFile { get { lock (playLock) return filePlayer != null; } }
        public bool MicrophoneReady { get; private set; }
        public string? MicrophoneError { get; private set; }

        private WaveFormat Format => new(config.Audio.SampleRate, 16, 1);
        private const int ReceptionIdleMs = 700;

        public AudioEngine(AppConfig config, string userName)
        {
            this.config = config;
            this.userName = userName;
        }

        // ------------------------------------------------------------------
        // Arranque / parada
        // ------------------------------------------------------------------

        public void Start()
        {
            StartNetwork();
            StartPlayback();
            StartCapture();
        }

        private void StartNetwork()
        {
            try
            {
                socket = new UdpClient();
                // Sin esto, cerrar y reabrir rápido da "dirección en uso".
                socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, config.General.Puerto));

                running = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "WalkieTalkie-Receive"
                };
                receiveThread.Start();
            }
            catch (SocketException ex)
            {
                EngineError?.Invoke(this,
                    $"No se pudo abrir el puerto {config.General.Puerto}: {ex.Message}\n\n" +
                    "¿Hay otra copia de la aplicación abierta?");
            }
            catch (Exception ex)
            {
                EngineError?.Invoke(this, $"Error de red: {ex.Message}");
            }
        }

        private void StartPlayback()
        {
            try
            {
                mixer = new MixingSampleProvider(
                    WaveFormat.CreateIeeeFloatWaveFormat(config.Audio.SampleRate, 1))
                {
                    // Sin esto el mezclador se detiene cuando nadie habla.
                    ReadFully = true
                };

                volumeControl = new VolumeSampleProvider(mixer)
                {
                    Volume = Math.Clamp(config.Audio.Volumen, 0, 100) / 100f
                };

                livePlayer = new WaveOutEvent
                {
                    DeviceNumber = ResolveOutputDevice(),
                    DesiredLatency = Math.Max(60, config.Audio.PlaybackLatencyMs),
                    NumberOfBuffers = 3
                };
                livePlayer.Init(volumeControl);
                // Se deja sonando en silencio: arrancar el dispositivo al recibir
                // el primer paquete cortaba siempre la primera sílaba.
                livePlayer.Play();
            }
            catch (Exception ex)
            {
                EngineError?.Invoke(this, $"No se pudo iniciar la salida de audio: {ex.Message}");
            }
        }

        private void StartCapture()
        {
            if (!config.Audio.MantenerMicrofonoAbierto) return;
            OpenMicrophone();
        }

        private void OpenMicrophone()
        {
            if (waveIn != null) return;

            try
            {
                if (WaveInEvent.DeviceCount == 0)
                {
                    MicrophoneReady = false;
                    MicrophoneError = "No se detectó ningún micrófono.";
                    return;
                }

                waveIn = new WaveInEvent
                {
                    DeviceNumber = ResolveInputDevice(),
                    WaveFormat = Format,
                    BufferMilliseconds = Math.Max(20, config.Audio.BufferMilliseconds),
                    NumberOfBuffers = 3
                };
                waveIn.DataAvailable += OnMicData;
                waveIn.RecordingStopped += OnRecordingStopped;
                waveIn.StartRecording();

                MicrophoneReady = true;
                MicrophoneError = null;
            }
            catch (Exception ex)
            {
                MicrophoneReady = false;
                MicrophoneError = ex.Message;
                waveIn?.Dispose();
                waveIn = null;
                EngineError?.Invoke(this, $"No se pudo abrir el micrófono: {ex.Message}");
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                MicrophoneReady = false;
                MicrophoneError = e.Exception.Message;
                EngineError?.Invoke(this, $"El micrófono dejó de funcionar: {e.Exception.Message}");
            }
        }

        private int ResolveInputDevice()
        {
            int d = config.Audio.InputDevice;
            return (d >= 0 && d < WaveInEvent.DeviceCount) ? d : -1;
        }

        private int ResolveOutputDevice()
        {
            int d = config.Audio.OutputDevice;
            return (d >= 0 && d < WaveOut.DeviceCount) ? d : -1;
        }

        private void TrySend(byte[] data, string ip)
        {
            try
            {
                socket?.Send(data, data.Length, ip, config.General.Puerto);
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.NetworkLog, $"Envío a {ip}: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Recepción
        // ------------------------------------------------------------------

        private void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);

            while (running)
            {
                try
                {
                    byte[] data = socket!.Receive(ref remote);
                    string senderIp = remote.Address.ToString();

                    // Cualquiera en la red podía inyectar audio y se reproducía solo.
                    string? knownName = config.BuscarNombrePorIp(senderIp);
                    if (config.General.SoloContactosConocidos && knownName == null)
                        continue;

                    if (!AudioProtocol.TryParse(data, data.Length,
                            out var type, out string sender, out int offset, out int length))
                        continue;

                    // El nombre anunciado manda, pero sólo si lo conocemos.
                    string contact = config.Contactos.ContainsKey(sender)
                        ? sender
                        : (knownName ?? senderIp);

                    if (string.Equals(contact, userName, StringComparison.OrdinalIgnoreCase))
                        continue; // eco de nosotros mismos

                    if (length > 0)
                        HandleAudioPacket(contact, data, offset, length,
                            esGrupo: type == PacketType.AudioGrupo);
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
                    AppPaths.Log(AppPaths.NetworkLog, $"Recepción: {ex}");
                }
            }
        }

        private void HandleAudioPacket(string contact, byte[] data, int offset, int length, bool esGrupo)
        {
            // 1) Reproducir en vivo
            var buffer = GetLiveBuffer(contact);
            try
            {
                buffer.AddSamples(data, offset, length);
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.NetworkLog, $"Buffer de {contact}: {ex.Message}");
            }

            // 2) Guardar a disco
            bool isNew = false;
            lock (rxLock)
            {
                if (!receptions.TryGetValue(contact, out var rx))
                {
                    try
                    {
                        Directory.CreateDirectory(AppPaths.InboxDir(userName));
                        string temp = Path.Combine(AppPaths.InboxDir(userName),
                            $"~{Guid.NewGuid():N}.part");

                        rx = new Reception
                        {
                            TempPath = temp,
                            Contact = contact,
                            Started = DateTime.Now,
                            EsGrupo = esGrupo,
                            Writer = new WaveFileWriter(temp, Format)
                        };
                        rx.IdleTimer = new System.Threading.Timer(
                            _ => FinishReception(contact), null, ReceptionIdleMs, Timeout.Infinite);

                        receptions[contact] = rx;
                        isNew = true;
                    }
                    catch (Exception ex)
                    {
                        AppPaths.Log(AppPaths.NetworkLog, $"No se pudo crear el archivo de {contact}: {ex.Message}");
                        return;
                    }
                }

                try
                {
                    rx.Writer.Write(data, offset, length);
                    // Reiniciar la cuenta atrás de "dejó de hablar".
                    rx.IdleTimer?.Change(ReceptionIdleMs, Timeout.Infinite);
                }
                catch (Exception ex)
                {
                    AppPaths.Log(AppPaths.NetworkLog, $"Escritura de {contact}: {ex.Message}");
                }
            }

            if (isNew)
            {
                PlayCue("receive.wav");
                ReceptionStarted?.Invoke(this, new ReceptionEventArgs { Contact = contact });
            }
        }

        private BufferedWaveProvider GetLiveBuffer(string contact)
        {
            lock (liveBuffers)
            {
                if (liveBuffers.TryGetValue(contact, out var existing))
                    return existing;

                var buffer = new BufferedWaveProvider(Format)
                {
                    BufferDuration = TimeSpan.FromSeconds(3),
                    DiscardOnBufferOverflow = true,
                    ReadFully = true
                };
                liveBuffers[contact] = buffer;
                mixer?.AddMixerInput(buffer.ToSampleProvider());
                return buffer;
            }
        }

        private void FinishReception(string contact)
        {
            Reception? rx;
            lock (rxLock)
            {
                if (!receptions.TryGetValue(contact, out rx)) return;
                receptions.Remove(contact);

                rx.IdleTimer?.Dispose();
                rx.IdleTimer = null;

                // Dispose (no Flush) es lo que actualiza los tamaños de la cabecera RIFF.
                // Antes se copiaba el stream tras un Flush y los .wav recibidos
                // quedaban con duración 0 para reproductores externos.
                try { rx.Writer.Dispose(); } catch { }
            }

            AudioItem? item = null;
            try
            {
                string finalPath = Path.Combine(AppPaths.InboxDir(userName),
                    AudioItem.BuildFileName(contact, rx.Started));

                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(rx.TempPath, finalPath);

                item = new AudioItem
                {
                    FilePath = finalPath,
                    Contact = contact,
                    Timestamp = rx.Started,
                    Direction = AudioDirection.Recibido,
                    Duration = AudioItem.ReadDuration(finalPath),
                    EsGrupo = rx.EsGrupo,
                    Unread = true
                };
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.NetworkLog, $"Cierre de recepción de {contact}: {ex.Message}");
                try { if (File.Exists(rx.TempPath)) File.Delete(rx.TempPath); } catch { }
            }

            ReceptionEnded?.Invoke(this, new ReceptionEventArgs { Contact = contact, Item = item });
        }

        // ------------------------------------------------------------------
        // Transmisión
        // ------------------------------------------------------------------

        public bool StartTransmit(string contact) => StartTransmit(new[] { contact });

        /// <summary>
        /// Transmite a uno o varios destinatarios a la vez. El audio se graba una
        /// sola vez y se envía por separado a cada uno (unicast), así que solo lo
        /// reciben los elegidos.
        /// </summary>
        public bool StartTransmit(IEnumerable<string> contacts)
        {
            List<string> destinatarios;

            lock (txLock)
            {
                if (transmitting) return false;

                destinatarios = contacts
                    .Where(c => !string.IsNullOrWhiteSpace(c) && config.Contactos.ContainsKey(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (destinatarios.Count == 0) return false;

                if (waveIn == null)
                {
                    OpenMicrophone();
                    if (waveIn == null) return false;
                }

                try
                {
                    Directory.CreateDirectory(AppPaths.SentDir);
                    sendStarted = DateTime.Now;
                    sendContacts = destinatarios;
                    sendTempPath = Path.Combine(AppPaths.SentDir, $"~{Guid.NewGuid():N}.part");
                    sendWriter = new WaveFileWriter(sendTempPath, Format);
                    transmitting = true;
                }
                catch (Exception ex)
                {
                    EngineError?.Invoke(this, $"No se pudo iniciar la grabación: {ex.Message}");
                    CleanupTransmit();
                    return false;
                }
            }

            // Seguro contra PTT "pegado" (soltar la tecla durante un Alt+Tab).
            int maxMs = Math.Max(5, config.General.MaxSegundosTransmision) * 1000;
            maxTxTimer = new System.Threading.Timer(_ => StopTransmit(), null, maxMs, Timeout.Infinite);

            PlayCue("f7.wav");
            TransmissionStarted?.Invoke(this, destinatarios);
            return true;
        }

        public void StopTransmit()
        {
            var items = new List<AudioItem>();

            lock (txLock)
            {
                if (!transmitting) return;
                transmitting = false;

                var destinatarios = sendContacts;

                maxTxTimer?.Dispose();
                maxTxTimer = null;

                try
                {
                    sendWriter?.Dispose();
                    sendWriter = null;

                    if (sendTempPath != null && destinatarios.Count > 0 && File.Exists(sendTempPath))
                    {
                        string finalPath = Path.Combine(AppPaths.SentDir,
                            AudioItem.BuildFileName(destinatarios, sendStarted));

                        if (File.Exists(finalPath)) File.Delete(finalPath);
                        File.Move(sendTempPath, finalPath);

                        var duracion = AudioItem.ReadDuration(finalPath);

                        // Una entrada por destinatario, todas apuntando al mismo archivo.
                        foreach (string contact in destinatarios)
                        {
                            items.Add(new AudioItem
                            {
                                FilePath = finalPath,
                                Contact = contact,
                                Timestamp = sendStarted,
                                Direction = AudioDirection.Enviado,
                                Duration = duracion,
                                Recipients = destinatarios.Count > 1
                                    ? new List<string>(destinatarios)
                                    : new List<string>()
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppPaths.Log(AppPaths.CrashLog, $"Cierre de transmisión: {ex.Message}");
                }
                finally
                {
                    sendTempPath = null;
                    sendContacts = new List<string>();
                }
            }

            PlayCue("end_tx.wav");
            TransmissionEnded?.Invoke(this, items);
        }

        private void CleanupTransmit()
        {
            try { sendWriter?.Dispose(); } catch { }
            sendWriter = null;
            try { if (sendTempPath != null && File.Exists(sendTempPath)) File.Delete(sendTempPath); } catch { }
            sendTempPath = null;
            sendContacts = new List<string>();
            transmitting = false;
        }

        private void OnMicData(object? sender, WaveInEventArgs e)
        {
            // Cualquier excepción que escape de aquí detiene la captura por completo
            // y deja el micrófono muerto hasta reiniciar la aplicación.
            try
            {
                ProcessMicData(e);
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.CrashLog, $"Captura: {ex}");
            }
        }

        private void ProcessMicData(WaveInEventArgs e)
        {
            // Nivel para el medidor: se calcula siempre, así el usuario puede
            // comprobar que el micrófono funciona sin transmitir.
            InputLevel?.Invoke(this, PeakLevel(e.Buffer, e.BytesRecorded));

            if (!transmitting) return;

            lock (txLock)
            {
                if (!transmitting || sendWriter == null) return;

                try
                {
                    sendWriter.Write(e.Buffer, 0, e.BytesRecorded);

                    if (sendContacts.Count == 0) return;

                    // El paquete se arma una vez y se reparte a cada destinatario.
                    bool esGrupo = sendContacts.Count > 1;
                    byte[] packet = AudioProtocol.BuildAudio(
                        userName, e.Buffer, e.BytesRecorded, esGrupo);

                    foreach (string contact in sendContacts)
                    {
                        if (config.Contactos.TryGetValue(contact, out string? ip) &&
                            !string.IsNullOrWhiteSpace(ip))
                        {
                            TrySend(packet, ip);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppPaths.Log(AppPaths.NetworkLog, $"Transmisión: {ex.Message}");
                }
            }
        }

        private static float PeakLevel(byte[] buffer, int bytes)
        {
            int peak = 0;
            for (int i = 0; i + 1 < bytes; i += 2)
            {
                // Ojo: Math.Abs(short.MinValue) desborda. Se amplía a int primero.
                int sample = Math.Abs((int)BitConverter.ToInt16(buffer, i));
                if (sample > peak) peak = sample;
            }
            return Math.Min(1f, peak / 32768f);
        }

        // ------------------------------------------------------------------
        // Reproducción de archivos del historial
        // ------------------------------------------------------------------

        /// <summary>
        /// Antes esto era un while(...) Application.DoEvents() que congelaba la
        /// ventana y permitía reentrar pulsando Play dos veces.
        /// </summary>
        public bool PlayFile(string path)
        {
            StopPlayback();

            lock (playLock)
            {
                try
                {
                    fileReader = new WaveFileReader(path);
                    filePlayer = new WaveOutEvent { DeviceNumber = ResolveOutputDevice() };
                    filePlayer.Init(fileReader);
                    filePlayer.PlaybackStopped += OnFilePlaybackStopped;
                    filePlayer.Play();
                    return true;
                }
                catch (Exception ex)
                {
                    EngineError?.Invoke(this, $"No se pudo reproducir el audio: {ex.Message}");
                    DisposeFilePlayer();
                    return false;
                }
            }
        }

        private void OnFilePlaybackStopped(object? sender, StoppedEventArgs e)
        {
            lock (playLock) DisposeFilePlayer();
            PlaybackFinished?.Invoke(this, EventArgs.Empty);
        }

        public void StopPlayback()
        {
            WaveOutEvent? player;
            lock (playLock) player = filePlayer;

            try { player?.Stop(); } catch { }
        }

        private void DisposeFilePlayer()
        {
            if (filePlayer != null)
            {
                filePlayer.PlaybackStopped -= OnFilePlaybackStopped;
                try { filePlayer.Dispose(); } catch { }
                filePlayer = null;
            }
            try { fileReader?.Dispose(); } catch { }
            fileReader = null;
        }

        // ------------------------------------------------------------------
        // Avisos sonoros
        // ------------------------------------------------------------------

        /// <summary>
        /// La versión anterior suscribía PlaybackStopped en cada aviso sin
        /// desuscribir nunca, y los otros avisos hacían Thread.Sleep en bucle.
        /// </summary>
        public void PlayCue(string fileName)
        {
            if (!config.Audio.SonidosDeAviso) return;

            string path = AppPaths.Sound(fileName);
            if (!File.Exists(path)) return;

            try
            {
                var reader = new WaveFileReader(path);
                var player = new WaveOutEvent { DeviceNumber = ResolveOutputDevice() };
                player.Init(reader);

                void OnStopped(object? s, StoppedEventArgs e)
                {
                    player.PlaybackStopped -= OnStopped;
                    lock (cuePlayers) cuePlayers.Remove(player);
                    try { player.Dispose(); } catch { }
                    try { reader.Dispose(); } catch { }
                }

                player.PlaybackStopped += OnStopped;
                lock (cuePlayers) cuePlayers.Add(player); // evita que el GC se lo lleve
                player.Play();
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.CrashLog, $"Aviso {fileName}: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Reconfiguración en caliente
        // ------------------------------------------------------------------

        public void ApplyVolume(int percent)
        {
            if (volumeControl != null)
                volumeControl.Volume = Math.Clamp(percent, 0, 100) / 100f;
        }

        /// <summary>Reinicia la parte de audio tras cambiar dispositivos o calidad.</summary>
        public void RestartAudio()
        {
            StopTransmit();
            StopPlayback();

            if (waveIn != null)
            {
                waveIn.DataAvailable -= OnMicData;
                waveIn.RecordingStopped -= OnRecordingStopped;
                try { waveIn.StopRecording(); } catch { }
                try { waveIn.Dispose(); } catch { }
                waveIn = null;
            }

            try { livePlayer?.Dispose(); } catch { }
            livePlayer = null;
            lock (liveBuffers) liveBuffers.Clear();
            mixer = null;
            volumeControl = null;

            StartPlayback();
            StartCapture();
        }

        public void Dispose()
        {
            running = false;

            StopTransmit();
            StopPlayback();

            maxTxTimer?.Dispose();

            if (waveIn != null)
            {
                waveIn.DataAvailable -= OnMicData;
                waveIn.RecordingStopped -= OnRecordingStopped;
                try { waveIn.StopRecording(); } catch { }
                try { waveIn.Dispose(); } catch { }
                waveIn = null;
            }

            lock (rxLock)
            {
                foreach (var rx in receptions.Values)
                {
                    rx.IdleTimer?.Dispose();
                    try { rx.Writer.Dispose(); } catch { }
                    try { if (File.Exists(rx.TempPath)) File.Delete(rx.TempPath); } catch { }
                }
                receptions.Clear();
            }

            try { livePlayer?.Dispose(); } catch { }
            livePlayer = null;

            lock (cuePlayers)
            {
                foreach (var p in cuePlayers.ToList())
                {
                    try { p.Dispose(); } catch { }
                }
                cuePlayers.Clear();
            }

            try { socket?.Close(); } catch { }
            socket?.Dispose();
            socket = null;

            try { receiveThread?.Join(500); } catch { }
        }

        /// <summary>Borra audios más antiguos que N días. Antes la carpeta crecía sin límite.</summary>
        public static int PurgeOldAudios(string userName, int days)
        {
            if (days <= 0) return 0;

            int deleted = 0;
            DateTime limit = DateTime.Now.AddDays(-days);

            foreach (string dir in new[] { AppPaths.InboxDir(userName), AppPaths.SentDir })
            {
                if (!Directory.Exists(dir)) continue;

                foreach (string file in Directory.GetFiles(dir, "*.*"))
                {
                    try
                    {
                        // Los .part son restos de una recepción interrumpida.
                        bool isLeftover = file.EndsWith(".part", StringComparison.OrdinalIgnoreCase);
                        if (isLeftover || File.GetLastWriteTime(file) < limit)
                        {
                            File.Delete(file);
                            deleted++;
                        }
                    }
                    catch
                    {
                        // Archivo en uso: se intentará en el próximo arranque.
                    }
                }
            }
            return deleted;
        }
    }
}
