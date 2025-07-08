#if !WINDOWS
#error Esta aplicación solo es compatible con Windows.
#endif

#pragma warning disable CA1416 // Validar la compatibilidad de la plataforma

using Microsoft.Extensions.Configuration;
using NAudio.Wave;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace WalkieTalkieApp
{
    public partial class MainForm : Form
    {
        private const int SampleRate = 44100; // Frecuencia de muestreo estándar corregida
        private const int Channels = 1;

        private Dictionary<string, string> contactos = new();
        private UdpClient? udpSender;
        private UdpClient? udpReceiver;
        private BufferedWaveProvider? waveProvider;
        private WaveOut? outputPlayer;
        private WaveInEvent? waveIn; // Usamos WaveInEvent directamente
        private MemoryStream? recordingStream;
        private WaveFileWriter? writer;
        private string userName = string.Empty;
        private GlobalHotKeyManager? hotKeyManager;
        private WaveFileReader? notificationSound;
        private WaveOutEvent? notificationPlayer;

        private bool isRecording = false;
        private string selectedContactName = string.Empty;
        private string audioDirectory = "audios";
        private const int Port = 5000;
        private ThreadSafeFlag f7Pressed = new ThreadSafeFlag();
        private bool isPlayingNotification = false;

        private Dictionary<string, MemoryStream> activeReceptions = new Dictionary<string, MemoryStream>();
        private Dictionary<string, WaveFileWriter> activeWriters = new Dictionary<string, WaveFileWriter>();
        private Dictionary<string, System.Timers.Timer> receptionTimers = new Dictionary<string, System.Timers.Timer>();
        private Dictionary<string, bool> hasPlayedNotification = new Dictionary<string, bool>();

        private Dictionary<string, List<AudioItem>> contactAudioHistory = new Dictionary<string, List<AudioItem>>();

        private readonly object recordingLock = new object();
        private readonly object receptionLock = new object();

        public MainForm(string userName)
        {
            InitializeComponent();
            this.userName = userName;
            this.Text = "Walkie Talkie Empresarial";
            this.lblUserName.Text = userName; // Asignar nombre de usuario a la nueva etiqueta

            SetUserImage(userName);

            Application.ThreadException += (sender, e) =>
                HandleGlobalException(e.Exception, "UI Thread");
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                HandleGlobalException((Exception)e.ExceptionObject, "AppDomain");

            LoadNotificationSounds();
            ApplyModernStyle();
            CargarConfiguracion();

            Directory.CreateDirectory(audioDirectory);
            Directory.CreateDirectory(Path.Combine(audioDirectory, userName));
            Directory.CreateDirectory(Path.Combine(audioDirectory, "Enviados"));

            CargarHistorialCompleto();
            ConfigurarInterfaz();
            IniciarServidorUDP();
            InicializarAudioTiempoReal();

            hotKeyManager = new GlobalHotKeyManager(
                keyDownAction: HandleF7KeyDown,
                keyUpAction: HandleF7KeyUp
            );
        }

        private void HandleGlobalException(Exception ex, string source)
        {
            Debug.WriteLine($"[{source} ERROR] {ex}");
            try
            {
                File.AppendAllText("crash_log.txt", $"[{DateTime.Now}] {source} ERROR: {ex}\n");
            }
            catch { }
        }

        private void LoadNotificationSounds()
        {
            try
            {
                // Ruta de f7.wav y receive.wav deben estar en la carpeta "sounds"
                string receiveSoundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds", "receive.wav");
                if (File.Exists(receiveSoundPath))
                {
                    // Este reproductor lo usará el otro PC para notificar la llegada de audio
                    notificationSound = new WaveFileReader(receiveSoundPath);
                    notificationPlayer = new WaveOutEvent();
                    notificationPlayer.Init(notificationSound);
                    Debug.WriteLine("Sonido de recepción cargado correctamente");
                }
                else
                {
                    Debug.WriteLine("Archivo receive.wav no encontrado");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando sonidos: {ex.Message}");
            }
        }

        private void PlayNotificationSound()
        {
            if (notificationPlayer == null || isPlayingNotification) return;

            try
            {
                isPlayingNotification = true;
                notificationSound!.Position = 0;
                notificationPlayer.Play();

                notificationPlayer.PlaybackStopped += (s, e) =>
                {
                    isPlayingNotification = false;
                };
            }
            catch
            {
                isPlayingNotification = false;
            }
        }

        private void PlayEndTxSound()
        {
            Task.Run(() =>
            {
                string endTxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds", "end_tx.wav");
                if (!File.Exists(endTxPath))
                {
                    Debug.WriteLine("ERROR: end_tx.wav no se encontró en la carpeta 'sounds'.");
                    return;
                }
                try
                {
                    using (var fs = new FileStream(endTxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var reader = new WaveFileReader(fs))
                    using (var player = new WaveOutEvent())
                    {
                        player.Init(reader);
                        player.Play();
                        while (player.PlaybackState == PlaybackState.Playing)
                        {
                            Thread.Sleep(50);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error al reproducir end_tx.wav: " + ex.Message);
                }
            });
        }

        private void PlayF7Sound()
        {
            Task.Run(() =>
            {
                try
                {
                    string f7SoundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds", "f7.wav");
                    if (!File.Exists(f7SoundPath)) return;

                    using (var reader = new WaveFileReader(f7SoundPath))
                    using (var player = new WaveOutEvent())
                    {
                        player.Init(reader);
                        player.Play();
                        while (player.PlaybackState == PlaybackState.Playing)
                        {
                            Thread.Sleep(50);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error al reproducir f7.wav: {ex.Message}");
                }
            });
        }

        private void ApplyModernStyle()
        {
            this.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.Font = new System.Drawing.Font("Segoe UI", 10);
            lblContactos.ForeColor = System.Drawing.Color.White;
            lblHistorial.ForeColor = System.Drawing.Color.White;
            lstHistorial.BackColor = System.Drawing.Color.FromArgb(63, 63, 70);
            lstHistorial.ForeColor = System.Drawing.Color.White;
            btnRecord.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0, 122, 204);
            btnPlay.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0, 122, 204);
            btnRecord.BackColor = System.Drawing.Color.FromArgb(0, 122, 204);
            btnRecord.ForeColor = System.Drawing.Color.White;
            btnPlay.BackColor = System.Drawing.Color.FromArgb(0, 122, 204);
            btnPlay.ForeColor = System.Drawing.Color.White;
            cmbContactos.BackColor = System.Drawing.Color.FromArgb(63, 63, 70);
            cmbContactos.ForeColor = System.Drawing.Color.White;
        }

        private void HandleF7KeyDown()
        {
            if (!f7Pressed.Value && !isRecording)
            {
                f7Pressed.Value = true;

                SafeInvoke(() =>
                {
                    btnRecord.Enabled = false; // 🔒 evitar clic durante F7

                    if (cmbContactos.SelectedIndex >= 0)
                    {
                        btnRecord_MouseDown(this, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                        FlashWindow();
                    }
                });
            }
        }


        private void HandleF7KeyUp()
        {
            if (f7Pressed.Value)
            {
                f7Pressed.Value = false;

                SafeInvoke(() =>
                {
                    btnRecord.Enabled = true; // ✅ habilitar nuevamente

                    if (isRecording)
                    {
                        btnRecord_MouseUp(this, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                    }
                });
            }
        }


        private void SafeInvoke(Action action)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try { action(); }
                        catch (Exception ex) { Debug.WriteLine($"SafeInvoke error: {ex.Message}"); }
                    }));
                }
                else
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SafeInvoke outer error: {ex.Message}");
            }
        }

        private void CargarConfiguracion()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();

                contactos = new Dictionary<string, string>();
                var contactosSection = config.GetSection("Contactos");
                foreach (var child in contactosSection.GetChildren())
                {
                    contactos[child.Key] = child.Value ?? string.Empty;
                }
            }
            catch
            {
                contactos = new Dictionary<string, string>();
                MessageBox.Show("Error cargando configuración. Usando valores predeterminados.");
            }
        }

        private void ConfigurarInterfaz()
        {
            cmbContactos.Items.Clear();

            var contactosExternos = contactos
                .Where(c => c.Key != userName)
                .Select(c => c.Key)
                .ToArray();

            cmbContactos.Items.AddRange(contactosExternos);

            cmbContactos.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            cmbContactos.SelectedIndexChanged += (s, e) =>
            {
                selectedContactName = cmbContactos.SelectedItem?.ToString() ?? string.Empty;
                MostrarHistorialContacto();
            };

            if (cmbContactos.Items.Count > 0)
            {
                cmbContactos.SelectedIndex = 0;
                selectedContactName = cmbContactos.SelectedItem?.ToString() ?? string.Empty;
                MostrarHistorialContacto();
            }
        }

        private void InicializarAudioTiempoReal()
        {
            waveProvider = new BufferedWaveProvider(new WaveFormat(SampleRate, Channels))
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            outputPlayer = new WaveOut
            {
                DesiredLatency = 300
            };
            outputPlayer.Init(waveProvider);
        }

        private void IniciarServidorUDP()
        {
            try
            {
                udpReceiver = new UdpClient(Port);
                Thread receiverThread = new Thread(RecibirAudioThread);
                receiverThread.IsBackground = true;
                receiverThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error iniciando servidor: {ex.Message}");
            }
        }

        private void RecibirAudioThread()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                try
                {
                    byte[] data = udpReceiver?.Receive(ref remoteEP) ?? Array.Empty<byte>();
                    string senderIP = remoteEP.Address.ToString();
                    string senderName = contactos.FirstOrDefault(c => c.Value == senderIP).Key ?? senderIP;

                    WaveFileWriter writerLocal = null;
                    lock (receptionLock)
                    {
                        if (!activeReceptions.TryGetValue(senderIP, out MemoryStream stream) ||
                            !activeWriters.TryGetValue(senderIP, out writerLocal))
                        {
                            stream = new MemoryStream();
                            writerLocal = new WaveFileWriter(stream, new WaveFormat(SampleRate, Channels));
                            activeReceptions[senderIP] = stream;
                            activeWriters[senderIP] = writerLocal;
                            hasPlayedNotification[senderIP] = false;
                        }

                        try
                        {
                            writerLocal.Write(data, 0, data.Length);
                        }
                        catch (ObjectDisposedException)
                        {
                            // Si se detecta que el writer se ha descartado, reinicializar el stream y el writer
                            stream = new MemoryStream();
                            writerLocal = new WaveFileWriter(stream, new WaveFormat(SampleRate, Channels));
                            activeReceptions[senderIP] = stream;
                            activeWriters[senderIP] = writerLocal;
                            writerLocal.Write(data, 0, data.Length);
                        }
                    }

                    this.Invoke((Action)(() =>
                    {
                        waveProvider?.AddSamples(data, 0, data.Length);
                        if (outputPlayer?.PlaybackState != PlaybackState.Playing)
                        {
                            outputPlayer?.Play();
                        }
                        lock (receptionLock)
                        {
                            if (!hasPlayedNotification.ContainsKey(senderIP) || !hasPlayedNotification[senderIP])
                            {
                                PlayNotificationSound();
                                hasPlayedNotification[senderIP] = true;
                            }
                        }
                    }));

                    StartReceptionTimer(senderIP);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    break;
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    File.AppendAllText("network_errors.log", $"[{DateTime.Now}] {ex}\n");
                }
            }
        }

        private void StartReceptionTimer(string senderIP)
        {
            lock (receptionLock)
            {
                if (receptionTimers.ContainsKey(senderIP))
                {
                    receptionTimers[senderIP].Stop();
                    receptionTimers[senderIP].Dispose();
                    receptionTimers.Remove(senderIP);
                }

                var timer = new System.Timers.Timer(500) { AutoReset = false };
                timer.Elapsed += (s, e) => FinalizeReception(senderIP);
                timer.Start();
                receptionTimers[senderIP] = timer;
            }
        }

        private void FinalizeReception(string senderIP)
        {
            MemoryStream? stream;
            WaveFileWriter? writerLocal;

            lock (receptionLock)
            {
                if (!activeReceptions.TryGetValue(senderIP, out stream) ||
                    !activeWriters.TryGetValue(senderIP, out writerLocal))
                {
                    return;
                }
                // Eliminar las referencias antes de procesar
                activeReceptions.Remove(senderIP);
                activeWriters.Remove(senderIP);

                if (receptionTimers.ContainsKey(senderIP))
                {
                    receptionTimers[senderIP].Stop();
                    receptionTimers[senderIP].Dispose();
                    receptionTimers.Remove(senderIP);
                }
                if (hasPlayedNotification.ContainsKey(senderIP))
                {
                    hasPlayedNotification.Remove(senderIP);
                }
            }

            try
            {
                writerLocal.Flush();
                stream.Position = 0;

                string senderName = contactos.FirstOrDefault(c => c.Value == senderIP).Key ?? senderIP;
                string folderPath = Path.Combine(audioDirectory, userName);
                Directory.CreateDirectory(folderPath);
                string fileName = $"{senderName}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                string filePath = Path.Combine(folderPath, fileName);

                using (var fileStream = File.Create(filePath))
                {
                    stream.CopyTo(fileStream);
                }

                this.Invoke((Action)(() =>
                {
                    if (cmbContactos.Items.Contains(senderName))
                    {
                        cmbContactos.SelectedItem = senderName;
                    }
                    MostrarHistorialContacto();
                    AgregarAudioAlHistorial(senderName, filePath, DateTime.Now);
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finalizando recepción: {ex.Message}");
            }
            finally
            {
                try { writerLocal?.Dispose(); } catch { }
                try { stream?.Dispose(); } catch { }
            }
        }

        private void AgregarAudioAlHistorial(string senderName, string filePath, DateTime timestamp)
        {
            if (!contactAudioHistory.ContainsKey(senderName))
            {
                contactAudioHistory[senderName] = new List<AudioItem>();
            }

            var newAudio = new AudioItem
            {
                FilePath = filePath,
                Sender = senderName,
                Timestamp = timestamp
            };

            contactAudioHistory[senderName].Insert(0, newAudio);

            if (senderName == selectedContactName)
            {
                lstHistorial.Items.Insert(0, newAudio.DisplayText);
            }
        }

        private void btnRecord_MouseDown(object? sender, MouseEventArgs e)
        {
            try
            {
                lock (recordingLock)
                {
                    if (isRecording || string.IsNullOrEmpty(selectedContactName))
                        return;

                    PlayF7Sound();

                    isRecording = true;
                    udpSender = new UdpClient();

                    waveIn = new WaveInEvent { WaveFormat = new WaveFormat(SampleRate, Channels) };
                    waveIn.DataAvailable += WaveIn_DataAvailable;

                    recordingStream = new MemoryStream();
                    writer = new WaveFileWriter(recordingStream, waveIn.WaveFormat);

                    waveIn.StartRecording();
                    btnRecord.Text = "SOLTAR PARA DEJAR DE HABLAR (F7)";
                    btnRecord.BackColor = Color.FromArgb(220, 20, 60);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en grabación: {ex.Message}", "Error crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
                isRecording = false;
                CleanupRecordingResources();
            }
        }


        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            lock (recordingLock)
            {
                if (!isRecording || writer == null)
                    return;

                try
                {
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                    if (contactos.TryGetValue(selectedContactName, out string? ipDestino) && ipDestino != null)
                    {
                        udpSender?.Send(e.Buffer, e.BytesRecorded, ipDestino, Port);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error enviando audio: {ex.Message}");
                }
            }
        }

        private void btnRecord_MouseUp(object? sender, MouseEventArgs e)
        {
            lock (recordingLock)
            {
                if (!isRecording)
                    return;

                isRecording = false;
                string filePath = string.Empty;

                try
                {
                    // Detener y liberar waveIn
                    if (waveIn != null)
                    {
                        waveIn.DataAvailable -= WaveIn_DataAvailable;
                        waveIn.StopRecording();
                        waveIn.Dispose();
                        waveIn = null;
                    }

                    // Cerrar el escritor si existe
                    if (writer != null)
                    {
                        writer.Flush();
                        writer.Dispose();
                        writer = null;
                    }

                    // Guardar audio si el stream aún está activo
                    if (recordingStream != null)
                    {
                        try
                        {
                            recordingStream.Position = 0;

                            string fileName = $"{selectedContactName}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                            filePath = Path.Combine(audioDirectory, "Enviados", fileName);

                            using (var fileStream = File.Create(filePath))
                            {
                                recordingStream.CopyTo(fileStream);
                            }

                            AgregarAudioAlHistorial(selectedContactName, filePath, DateTime.Now);
                        }
                        catch (ObjectDisposedException ex)
                        {
                            Debug.WriteLine($"[ERROR] El stream ya estaba cerrado: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ERROR] Fallo al guardar audio: {ex.Message}");
                        }
                        finally
                        {
                            recordingStream.Dispose();
                            recordingStream = null;
                        }
                    }

                    PlayEndTxSound();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al finalizar grabación: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnRecord.Text = "PRESIONAR PARA HABLAR (F7)";
                    btnRecord.BackColor = Color.FromArgb(0, 122, 204);
                    
                }
            }
        }


        private void CleanupRecordingResources()
        {
            waveIn?.StopRecording();
            waveIn?.Dispose();
            waveIn = null;

            writer?.Dispose();
            writer = null;

            recordingStream?.Dispose();
            recordingStream = null;

            udpSender?.Dispose();
            udpSender = null;
        }

        private void MostrarHistorialContacto()
        {
            lstHistorial.Items.Clear();

            if (contactAudioHistory.TryGetValue(selectedContactName, out var audioItems))
            {
                foreach (var audioItem in audioItems)
                {
                    lstHistorial.Items.Add(audioItem.DisplayText);
                }
            }
        }

        private void CargarHistorialCompleto()
        {
            contactAudioHistory.Clear();

            string userFolderPath = Path.Combine(audioDirectory, userName);
            if (Directory.Exists(userFolderPath))
            {
                foreach (var filePath in Directory.GetFiles(userFolderPath, "*.wav"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string[] parts = fileName.Split('_');
                    if (parts.Length >= 2 && DateTime.TryParseExact(parts[1], "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out var timestamp))
                    {
                        string senderName = parts[0];
                        AgregarAudioAlHistorial(senderName, filePath, timestamp);
                    }
                }
            }

            string sentFolderPath = Path.Combine(audioDirectory, "Enviados");
            if (Directory.Exists(sentFolderPath))
            {
                foreach (var filePath in Directory.GetFiles(sentFolderPath, "*.wav"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string[] parts = fileName.Split('_');
                    if (parts.Length >= 2 && DateTime.TryParseExact(parts[1], "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out var timestamp))
                    {
                        string receiverName = parts[0];
                        AgregarAudioAlHistorial(receiverName, filePath, timestamp);
                    }
                }
            }
        }

        private void FlashWindow()
        {
            var info = new FLASHWINFO
            {
                cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(FLASHWINFO))),
                hwnd = this.Handle,
                dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
                uCount = uint.MaxValue,
                dwTimeout = 0
            };
            FlashWindowEx(ref info);
        }

        private const uint FLASHW_TRAY = 2;
        private const uint FLASHW_TIMERNOFG = 12;
        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private void SetUserImage(string userName)
        {
            string imgPath = userName.ToLower() switch
            {
                "jose" => "resources\\jose.png",
                "leydy" => "resources\\leydy.png",
                "jennifer" => "resources\\jennifer.png",
                "daniel" => "resources\\daniel.png",
                "bodega" => "resources\\bodega.png",
                "pcprueba" => "resources\\pcprueba.png",
                 "facturacion" => "resources\\facturacion.png",
                _ => null
            };

            if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
            {
                picUser.Image = System.Drawing.Image.FromFile(imgPath);
            }
            else
            {
                picUser.Image = null;
            }
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (lstHistorial.SelectedIndex < 0)
            {
                MessageBox.Show("Selecciona un audio para reproducir.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (contactAudioHistory.TryGetValue(selectedContactName, out var audioItems) &&
                lstHistorial.SelectedIndex < audioItems.Count)
            {
                var audioItem = audioItems[lstHistorial.SelectedIndex];
                if (File.Exists(audioItem.FilePath))
                {
                    try
                    {
                        using (var waveFile = new WaveFileReader(audioItem.FilePath))
                        using (var waveOut = new WaveOutEvent())
                        {
                            waveOut.Init(waveFile);
                            waveOut.Play();
                            while (waveOut.PlaybackState == PlaybackState.Playing)
                            {
                                Application.DoEvents();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al reproducir el audio: {ex.Message}");
                    }
                }
                else
                {
                    MessageBox.Show("El archivo de audio no existe.");
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            hotKeyManager?.Dispose();

            foreach (var timer in receptionTimers.Values)
            {
                timer?.Stop();
                timer?.Dispose();
            }

            foreach (var writer in activeWriters.Values)
            {
                try
                {
                    writer?.Flush();
                    writer?.Dispose();
                }
                catch (ObjectDisposedException) { }
            }
            foreach (var stream in activeReceptions.Values)
            {
                try { stream?.Dispose(); }
                catch (ObjectDisposedException) { }
            }

            if (isRecording)
            {
                waveIn?.StopRecording();
                udpSender?.Close();
            }
            udpReceiver?.Close();
            outputPlayer?.Dispose();
            writer?.Dispose();
            recordingStream?.Dispose();
            notificationPlayer?.Dispose();
            notificationSound?.Dispose();

            base.OnFormClosing(e);
        }
    }

    public class AudioItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string DisplayText => $"{Sender} - {Timestamp:dd/MM/yyyy HH:mm:ss}";
    }
}