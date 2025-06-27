using Microsoft.Extensions.Configuration;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    public partial class MainForm : Form
    {
        private Dictionary<string, string> contactos;
        private string miNombre;
        private UdpClient udpSender;
        private UdpClient udpReceiver;
        private BufferedWaveProvider waveProvider;
        private WaveOut outputPlayer;
        private WaveInEvent waveIn;
        private bool isRecording = false;
        private string selectedContactName = "";
        private string audioDirectory = "audios";
        private const int Port = 5000;

        public MainForm()
        {
            InitializeComponent();

            // Cargar configuración desde JSON
            CargarConfiguracion();

            // Configurar ComboBox
            ConfigurarInterfaz();

            Directory.CreateDirectory(audioDirectory);
            CargarHistorial();
            IniciarServidorUDP();
            InicializarAudioTiempoReal();
        }

        private void CargarConfiguracion()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();

                // Usar ConfigurationBinder para obtener el diccionario
                contactos = new Dictionary<string, string>();
                var contactosSection = config.GetSection("Contactos");
                foreach (var child in contactosSection.GetChildren())
                {
                    contactos[child.Key] = child.Value;
                }

                miNombre = config["MiNombre"];
            }
            catch (Exception ex)
            {
                // Manejo de errores mejorado
                contactos = new Dictionary<string, string>();
                miNombre = Dns.GetHostName();

                MessageBox.Show($"Error cargando configuración: {ex.Message}\n" +
                                "Usando valores predeterminados.",
                                "Error de Configuración",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }

        private void ConfigurarInterfaz()
        {
            cmbContactos.Items.Clear();

            // Filtrar contactos excluyéndose a sí mismo
            var contactosExternos = contactos
                .Where(c => c.Key != miNombre)
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

            this.Text = $"Walkie Talkie - {miNombre}";
        }

        private string ObtenerMiIP()
        {
            try
            {
                // Primero intenta con el método más confiable
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
                }
            }
            catch
            {
                try
                {
                    // Método alternativo para redes complejas
                    return NetworkInterface.GetAllNetworkInterfaces()
                        .Where(n => n.OperationalStatus == OperationalStatus.Up)
                        .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
                }
                catch
                {
                    return "127.0.0.1";
                }
            }
        }

        private void InicializarAudioTiempoReal()
        {
            waveProvider = new BufferedWaveProvider(new WaveFormat(44100, 1));
            outputPlayer = new WaveOut();
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

                    // Buscar nombre del contacto por IP
                    string senderName = contactos.FirstOrDefault(c => c.Value == senderIP).Key ?? senderIP;

                    GuardarAudioRecibido(senderName, data);

                    this.Invoke((Action)(() => {
                        waveProvider.AddSamples(data, 0, data.Length);
                        if (outputPlayer.PlaybackState != PlaybackState.Playing)
                        {
                            outputPlayer.Play();
                        }
                    }));
                }
                catch { }
            }
        }

        private void GuardarAudioRecibido(string senderName, byte[] audioData)
        {
            string folderPath = Path.Combine(audioDirectory, senderName);
            Directory.CreateDirectory(folderPath);
            string fileName = $"recibido_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            string filePath = Path.Combine(folderPath, fileName);

            using (var writer = new WaveFileWriter(filePath, new WaveFormat(44100, 1)))
            {
                writer.Write(audioData, 0, audioData.Length);
            }

            this.Invoke((Action)(() => {
                if (senderName == selectedContactName)
                {
                    lstHistorial.Items.Insert(0, fileName);
                }
            }));
        }

        private void btnRecord_MouseDown(object sender, MouseEventArgs e)
        {
            if (!isRecording && !string.IsNullOrEmpty(selectedContactName))
            {
                isRecording = true;
                udpSender = new UdpClient();
                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(44100, 1),
                    BufferMilliseconds = 50
                };

                waveIn.DataAvailable += (s, args) =>
                {
                    try
                    {
                        // Obtener IP del contacto seleccionado
                        if (contactos.TryGetValue(selectedContactName, out string ipDestino))
                        {
                            udpSender.Send(args.Buffer, args.BytesRecorded, ipDestino, Port);
                        }
                    }
                    catch { }
                };

                waveIn.StartRecording();
                btnRecord.Text = "SOLTAR PARA DEJAR DE HABLAR";
                btnRecord.BackColor = System.Drawing.Color.LightGreen;
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
                btnRecord.Text = "MANTENER PARA HABLAR";
                btnRecord.BackColor = System.Drawing.SystemColors.Control;
            }
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (lstHistorial.SelectedItem != null && !string.IsNullOrEmpty(selectedContactName))
            {
                string fileName = lstHistorial.SelectedItem.ToString();
                string folderPath = Path.Combine(audioDirectory, selectedContactName);
                string filePath = Path.Combine(folderPath, fileName);

                if (File.Exists(filePath))
                {
                    try
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
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al reproducir: {ex.Message}");
                    }
                }
            }
        }

        private void CargarHistorial()
        {
            lstHistorial.Items.Clear();
            if (!string.IsNullOrEmpty(selectedContactName))
            {
                string folderPath = Path.Combine(audioDirectory, selectedContactName);
                if (Directory.Exists(folderPath))
                {
                    try
                    {
                        var files = Directory.GetFiles(folderPath, "*.wav")
                            .OrderByDescending(f => File.GetLastWriteTime(f))
                            .ToArray();

                        foreach (string file in files)
                        {
                            lstHistorial.Items.Add(Path.GetFileName(file));
                        }
                    }
                    catch { }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (isRecording)
            {
                waveIn?.StopRecording();
                udpSender?.Close();
            }
            udpReceiver?.Close();
            outputPlayer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}