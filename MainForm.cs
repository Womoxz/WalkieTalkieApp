using Microsoft.Extensions.Configuration;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    public partial class MainForm : Form
    {
        private Dictionary<string, string> contactos;
        private UdpClient udpSender;
        private UdpClient udpReceiver;
        private BufferedWaveProvider waveProvider;
        private WaveOut outputPlayer;
        private WaveInEvent waveIn;
        private bool isRecording = false;
        private string selectedContactName = "";
        private string audioDirectory = "audios";
        private const int Port = 5000;
        private MemoryStream recordingStream;
        private WaveFileWriter writer;
        private string userName;
        private KeyboardHook keyboardHook;
        private bool f7Pressed = false;
        private WaveFileReader notificationSound;
        private WaveOutEvent notificationPlayer;
        private WaveFileReader endTxSound;
        private bool isPlayingNotification = false;

        // Nuevos diccionarios para manejar las recepciones
        private Dictionary<string, MemoryStream> activeReceptions = new Dictionary<string, MemoryStream>();
        private Dictionary<string, WaveFileWriter> activeWriters = new Dictionary<string, WaveFileWriter>();
        private Dictionary<string, System.Timers.Timer> receptionTimers = new Dictionary<string, System.Timers.Timer>();
        private Dictionary<string, bool> hasPlayedNotification = new Dictionary<string, bool>();

        public MainForm(string userName)
        {
            InitializeComponent();
            this.userName = userName;
            this.Text = $"Walkie Talkie - {userName}";

            // Cargar sonidos de notificación
            LoadNotificationSounds();

            // Configurar interfaz
            ApplyModernStyle();
            CargarConfiguracion();
            ConfigurarInterfaz();

            // Iniciar componentes
            Directory.CreateDirectory(audioDirectory);
            Directory.CreateDirectory(Path.Combine(audioDirectory, userName));
            Directory.CreateDirectory(Path.Combine(audioDirectory, "Enviados"));
            CargarHistorial();
            IniciarServidorUDP();
            InicializarAudioTiempoReal();
            SetupKeyboardHook();
        }

        private void LoadNotificationSounds()
        {
            try
            {
                // Sonido de notificación al recibir audio
                string receiveSoundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds", "receive.wav");
                if (File.Exists(receiveSoundPath))
                {
                    notificationSound = new WaveFileReader(receiveSoundPath);
                    notificationPlayer = new WaveOutEvent();
                    notificationPlayer.Init(notificationSound);
                }

                // Sonido de fin de transmisión
                string endTxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds", "end_tx.wav");
                if (File.Exists(endTxPath))
                {
                    endTxSound = new WaveFileReader(endTxPath);
                }
            }
            catch
            {
                // Ignorar errores si no hay sonidos
            }
        }

        private void PlayNotificationSound()
        {
            if (notificationPlayer == null || isPlayingNotification) return;

            try
            {
                isPlayingNotification = true;
                notificationSound.Position = 0;
                notificationPlayer.Play();

                // Resetear bandera cuando termine
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
            if (endTxSound == null) return;

            try
            {
                using (var player = new WaveOutEvent())
                {
                    endTxSound.Position = 0;
                    player.Init(endTxSound);
                    player.Play();

                    // Mantener el sonido hasta que termine
                    while (player.PlaybackState == PlaybackState.Playing)
                    {
                        Application.DoEvents();
                        Thread.Sleep(50);
                    }
                }
            }
            catch
            {
                // Ignorar errores
            }
        }

        private void ApplyModernStyle()
        {
            this.BackColor = Color.FromArgb(45, 45, 48);
            lblContactos.ForeColor = Color.White;
            lblHistorial.ForeColor = Color.White;
            lstHistorial.BackColor = Color.FromArgb(63, 63, 70);
            lstHistorial.ForeColor = Color.White;
            btnRecord.FlatAppearance.BorderColor = Color.FromArgb(0, 122, 204);
            btnPlay.FlatAppearance.BorderColor = Color.FromArgb(0, 122, 204);
            btnRecord.BackColor = Color.FromArgb(0, 122, 204);
            btnRecord.ForeColor = Color.White;
            btnPlay.BackColor = Color.FromArgb(0, 122, 204);
            btnPlay.ForeColor = Color.White;
            cmbContactos.BackColor = Color.FromArgb(63, 63, 70);
            cmbContactos.ForeColor = Color.White;
        }

        private void SetupKeyboardHook()
        {
            try
            {
                keyboardHook = new KeyboardHook();
                keyboardHook.KeyDown += KeyboardHook_KeyDown;
                keyboardHook.KeyUp += KeyboardHook_KeyUp;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error configurando tecla F7: {ex.Message}\n" +
                                "La función de tecla rápida no estará disponible",
                                "Advertencia",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }

        private void KeyboardHook_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F7 && !f7Pressed && !isRecording)
            {
                f7Pressed = true;
                this.BeginInvoke((Action)(() =>
                {
                    if (cmbContactos.SelectedIndex >= 0)
                    {
                        btnRecord_MouseDown(null, null);
                        FlashWindow();
                    }
                }));
            }
        }

        private void KeyboardHook_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F7 && f7Pressed && isRecording)
            {
                f7Pressed = false;
                this.BeginInvoke((Action)(() =>
                {
                    btnRecord_MouseUp(null, null);
                }));
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
                    contactos[child.Key] = child.Value;
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

            // Filtrar contactos excluyéndose a sí mismo
            var contactosExternos = contactos
                .Where(c => c.Key != userName)
                .Select(c => c.Key)
                .ToArray();

            cmbContactos.Items.AddRange(contactosExternos);

            if (cmbContactos.Items.Count > 0)
                cmbContactos.SelectedIndex = 0;

            cmbContactos.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbContactos.SelectedIndexChanged += (s, e) =>
            {
                selectedContactName = cmbContactos.SelectedItem?.ToString();
                CargarHistorial();
            };
        }

        private void InicializarAudioTiempoReal()
        {
            waveProvider = new BufferedWaveProvider(new WaveFormat(44100, 1))
            {
                BufferDuration = TimeSpan.FromSeconds(3),
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
                    byte[] data = udpReceiver.Receive(ref remoteEP);
                    string senderIP = remoteEP.Address.ToString();
                    string senderName = contactos.FirstOrDefault(c => c.Value == senderIP).Key ?? senderIP;

                    // Iniciar nueva recepción si es necesario
                    if (!activeReceptions.ContainsKey(senderIP))
                    {
                        activeReceptions[senderIP] = new MemoryStream();
                        activeWriters[senderIP] = new WaveFileWriter(activeReceptions[senderIP], new WaveFormat(44100, 1));
                        hasPlayedNotification[senderIP] = false;
                    }

                    // Escribir audio en el buffer
                    activeWriters[senderIP].Write(data, 0, data.Length);

                    // Reproducir en tiempo real
                    this.Invoke((Action)(() =>
                    {
                        waveProvider.AddSamples(data, 0, data.Length);
                        if (outputPlayer.PlaybackState != PlaybackState.Playing)
                        {
                            outputPlayer.Play();
                        }

                        // Reproducir sonido de notificación solo una vez
                        if (!hasPlayedNotification[senderIP])
                        {
                            PlayNotificationSound();
                            hasPlayedNotification[senderIP] = true;
                        }
                    }));

                    // Iniciar/reiniciar temporizador
                    StartReceptionTimer(senderIP);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    // Cierre normal
                    break;
                }
                catch (Exception ex)
                {
                    File.AppendAllText("network_errors.log", $"[{DateTime.Now}] {ex}\n");
                }
            }
        }

        private void StartReceptionTimer(string senderIP)
        {
            // Cancelar temporizador anterior
            if (receptionTimers.ContainsKey(senderIP))
            {
                receptionTimers[senderIP].Stop();
                receptionTimers[senderIP].Dispose();
            }

            // Nuevo temporizador (500ms de silencio = fin de transmisión)
            var timer = new System.Timers.Timer(500) { AutoReset = false };
            timer.Elapsed += (s, e) => FinalizeReception(senderIP);
            timer.Start();
            receptionTimers[senderIP] = timer;
        }

        private void FinalizeReception(string senderIP)
        {
            try
            {
                if (activeReceptions.TryGetValue(senderIP, out var stream) &&
                    activeWriters.TryGetValue(senderIP, out var writer))
                {
                    writer.Flush();
                    stream.Position = 0;

                    // Obtener nombre del contacto
                    string senderName = contactos.FirstOrDefault(c => c.Value == senderIP).Key ?? senderIP;

                    // Guardar archivo único
                    string folderPath = Path.Combine(audioDirectory, userName);
                    Directory.CreateDirectory(folderPath);
                    string fileName = $"{senderName}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                    string filePath = Path.Combine(folderPath, fileName);

                    using (var fileStream = File.Create(filePath))
                    {
                        stream.WriteTo(fileStream);
                    }

                    // Actualizar UI
                    this.Invoke((Action)(() =>
                    {
                        lstHistorial.Items.Insert(0, $"{senderName} - {DateTime.Now:HH:mm:ss}");
                    }));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error finalizando recepción: {ex.Message}");
            }
            finally
            {
                // Limpiar recursos
                if (activeWriters.ContainsKey(senderIP))
                {
                    activeWriters[senderIP].Dispose();
                    activeWriters.Remove(senderIP);
                }
                if (activeReceptions.ContainsKey(senderIP))
                {
                    activeReceptions[senderIP].Dispose();
                    activeReceptions.Remove(senderIP);
                }
                if (receptionTimers.ContainsKey(senderIP))
                {
                    receptionTimers.Remove(senderIP);
                }
                if (hasPlayedNotification.ContainsKey(senderIP))
                {
                    hasPlayedNotification.Remove(senderIP);
                }
            }
        }

        private void btnRecord_MouseDown(object sender, MouseEventArgs e)
        {
            if (!isRecording && !string.IsNullOrEmpty(selectedContactName))
            {
                try
                {
                    isRecording = true;
                    udpSender = new UdpClient();
                    waveIn = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(44100, 1),
                        BufferMilliseconds = 100
                    };

                    // Preparar para guardar el audio completo
                    recordingStream = new MemoryStream();
                    writer = new WaveFileWriter(recordingStream, waveIn.WaveFormat);

                    waveIn.DataAvailable += (s, args) =>
                    {
                        try
                        {
                            // Guardar en memoria
                            writer.Write(args.Buffer, 0, args.BytesRecorded);

                            // Enviar por red
                            if (contactos.TryGetValue(selectedContactName, out string ipDestino))
                            {
                                udpSender.Send(args.Buffer, args.BytesRecorded, ipDestino, Port);
                            }
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText("audio_errors.log", $"[{DateTime.Now}] Send Error: {ex.Message}\n");
                        }
                    };

                    waveIn.StartRecording();
                    btnRecord.Text = "SOLTAR PARA DEJAR DE HABLAR (F7)";
                    btnRecord.BackColor = Color.FromArgb(220, 20, 60); // Rojo oscuro
                }
                catch (NAudio.MmException ex)
                {
                    isRecording = false;
                    string errorMessage = ex.Result switch
                    {
                        NAudio.MmResult.BadDeviceId => "ID de dispositivo incorrecto",
                        NAudio.MmResult.NoDriver => "Controlador de audio no encontrado",
                        NAudio.MmResult.InvalidHandle => "Acceso denegado. Verifique permisos del micrófono",
                        _ => $"Error de audio: {ex.Message} (Código: {ex.Result})"
                    };

                    MessageBox.Show(errorMessage, "Error de Grabación",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    isRecording = false;
                    MessageBox.Show($"Error general: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnRecord_MouseUp(object sender, MouseEventArgs e)
        {
            if (isRecording)
            {
                isRecording = false;
                waveIn.StopRecording();
                waveIn.Dispose();
                udpSender?.Close();
                btnRecord.Text = "MANTENER PARA HABLAR (F7)";
                btnRecord.BackColor = Color.FromArgb(0, 122, 204); // Azul

                // Guardar el audio completo
                GuardarAudioEnviado();

                // Reproducir sonido de fin de transmisión
                PlayEndTxSound();
            }
        }

        private void GuardarAudioEnviado()
        {
            try
            {
                writer.Flush();
                recordingStream.Position = 0;

                string folderPath = Path.Combine(audioDirectory, "Enviados");
                Directory.CreateDirectory(folderPath);
                string fileName = $"{selectedContactName}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                string filePath = Path.Combine(folderPath, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    recordingStream.WriteTo(fileStream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error guardando audio: {ex.Message}");
            }
            finally
            {
                writer?.Dispose();
                recordingStream?.Dispose();
                writer = null;
                recordingStream = null;
            }
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (lstHistorial.SelectedItem != null)
            {
                try
                {
                    string selectedItem = lstHistorial.SelectedItem.ToString();
                    string senderName = selectedItem.Split('-')[0].Trim();

                    string folderPath = Path.Combine(audioDirectory, userName);
                    string fileName = $"{senderName}_{GetDateForFile(selectedItem)}.wav";
                    string filePath = Path.Combine(folderPath, fileName);

                    if (File.Exists(filePath))
                    {
                        using (var audioFile = new AudioFileReader(filePath))
                        using (var outputDevice = new WaveOutEvent())
                        {
                            outputDevice.Init(audioFile);
                            outputDevice.Play();
                            while (outputDevice.PlaybackState == PlaybackState.Playing)
                            {
                                Application.DoEvents();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al reproducir: {ex.Message}");
                }
            }
        }

        private string GetDateForFile(string listItem)
        {
            string timePart = listItem.Split('-')[1].Trim();
            DateTime today = DateTime.Today;
            return $"{today:yyyyMMdd}_{timePart.Replace(":", "")}";
        }

        private void CargarHistorial()
        {
            lstHistorial.Items.Clear();
            string folderPath = Path.Combine(audioDirectory, userName);
            if (Directory.Exists(folderPath))
            {
                try
                {
                    var files = Directory.GetFiles(folderPath, "*.wav")
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .ToArray();

                    foreach (string file in files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        string[] parts = fileName.Split('_');
                        if (parts.Length < 3) continue;

                        string sender = parts[0];
                        string datePart = parts[1];
                        string timePart = parts[2];

                        if (DateTime.TryParseExact($"{datePart}{timePart}", "yyyyMMddHHmmss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out DateTime date))
                        {
                            lstHistorial.Items.Add($"{sender} - {date:HH:mm:ss}");
                        }
                    }
                }
                catch { }
            }
        }


        #region Flash Window API
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        public const uint FLASHW_ALL = 3;
        public const uint FLASHW_TIMERNOFG = 12;

        private void FlashWindow()
        {
            FLASHWINFO fInfo = new FLASHWINFO();
            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = this.Handle;
            fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
            fInfo.uCount = 3;
            fInfo.dwTimeout = 0;
            FlashWindowEx(ref fInfo);
        }
        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Detener todos los temporizadores
            foreach (var timer in receptionTimers.Values)
            {
                timer?.Stop();
                timer?.Dispose();
            }

            // Limpiar recepciones activas
            foreach (var writer in activeWriters.Values)
            {
                writer?.Flush();
                writer?.Dispose();
            }
            foreach (var stream in activeReceptions.Values)
            {
                stream?.Dispose();
            }

            keyboardHook?.Dispose();

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
            endTxSound?.Dispose();

            base.OnFormClosing(e);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Puedes agregar inicializaciones adicionales aquí si es necesario
        }
    }
}