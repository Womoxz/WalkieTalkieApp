#if !WINDOWS
#error Esta aplicación solo es compatible con Windows.
#endif

#pragma warning disable CA1416 // API sólo de Windows

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    public class ContactEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public bool Online { get; set; }
        public bool Talking { get; set; }
        public int Unread { get; set; }

        /// <summary>Fila especial "Todos": habla con toda la lista de una vez.</summary>
        public bool EsTodos { get; set; }

        public override string ToString() => Name;
    }

    public partial class MainForm : Form
    {
        private readonly AppConfig config;
        private readonly string userName;
        private AudioEngine engine = null!;
        private DiscoveryService? discovery;
        private GlobalHotKeyManager? hotKeyManager;

        private readonly Dictionary<string, List<AudioItem>> history =
            new(StringComparer.OrdinalIgnoreCase);

        private string selectedContact = string.Empty;
        private bool actualizandoSeleccion;
        private bool pttFromMouse;
        private bool pttFromKey;
        private bool exitRequested;
        private bool trayHintShown;
        private int volumeBeforeMute = 100;
        private DateTime lastLevelUpdate = DateTime.MinValue;
        private readonly System.Windows.Forms.Timer airBlinkTimer;
        private bool airOn;
        private Label lblVacio = null!;

        public MainForm(string userName)
        {
            this.userName = userName;
            this.config = AppConfig.Current;

            InitializeComponent();
            ApplyTheme();

            this.Text = $"Walkie Talkie — {userName}";
            this.lblUserName.Text = userName;
            this.picUser.Image = Theme.LoadAvatar(userName, 48);
            LoadAppIcon();

            airBlinkTimer = new System.Windows.Forms.Timer { Interval = 500 };
            airBlinkTimer.Tick += (s, e) =>
            {
                airOn = !airOn;
                lblAir.ForeColor = airOn ? Theme.Danger : Theme.Surface;
            };

            AppPaths.EnsureAudioDirs(userName);
            AudioEngine.PurgeOldAudios(userName, config.General.DiasRetencionAudios);

            BuildTrayMenu();
            LoadContacts();
            LoadHistory();
            StartEngine();
            InstallHotKey();

            UpdateStatus();
        }

        // ------------------------------------------------------------------
        // Arranque
        // ------------------------------------------------------------------

        private void LoadAppIcon()
        {
            try
            {
                string icoPath = Path.Combine(AppPaths.ResourcesDir, "VW_Talk_Logo.ico");
                if (File.Exists(icoPath))
                {
                    var icon = new Icon(icoPath);
                    this.Icon = icon;
                    trayIcon.Icon = icon;
                }
                else
                {
                    this.Icon = SystemIcons.Application;
                    trayIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                trayIcon.Icon = SystemIcons.Application;
            }
            trayIcon.Text = $"Walkie Talkie — {userName}";
        }

        private void ApplyTheme()
        {
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Text;
            this.Font = Theme.FontBase;

            lblUserName.Font = Theme.FontTitle;
            lblUserName.ForeColor = Theme.Text;
            lblUserStatus.Font = Theme.FontSmall;
            lblUserStatus.ForeColor = Theme.TextMuted;

            foreach (var b in new[] { btnSettings, btnMute })
            {
                Theme.StyleSecondaryButton(b);
                b.Font = new Font("Segoe UI Symbol", 12f);
            }

            splitMain.BackColor = Theme.Background;
            splitMain.Panel1.BackColor = Theme.Background;
            splitMain.Panel2.BackColor = Theme.Background;

            foreach (var l in new[] { lblContactos, lblHistorial })
            {
                l.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
                l.ForeColor = Theme.TextMuted;
            }

            foreach (var lb in new[] { lstContactos, lstHistorial })
            {
                lb.BackColor = Theme.Surface;
                lb.ForeColor = Theme.Text;
            }

            lblMicHint.Font = Theme.FontSmall;
            lblMicHint.ForeColor = Theme.TextMuted;
            vuMeter.BackColor = Theme.Surface;

            Theme.StyleButton(btnRecord, Theme.Accent, Theme.AccentHover);
            btnRecord.Font = new Font("Segoe UI", 11f, FontStyle.Bold);

            Theme.StyleSecondaryButton(btnPlay);
            btnPlay.Font = new Font("Segoe UI Symbol", 14f);

            lblStatus.Font = Theme.FontSmall;
            lblStatus.ForeColor = Theme.TextMuted;
            lblAir.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            lblAir.ForeColor = Theme.Surface;

            // Un ListBox vacío es un rectángulo mudo: mejor decir qué falta hacer.
            lblVacio = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontBase,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            splitMain.Panel2.Controls.Add(lblVacio);
        }

        private void UpdateEmptyState()
        {
            var destinos = Destinatarios();

            if (lstContactos.Items.Count == 0)
            {
                lblVacio.Text = "No hay contactos configurados.\n\nAbre ⚙ Configuración para añadirlos.";
            }
            else if (destinos.Count > 1)
            {
                // Con varios elegidos no hay una conversación única que mostrar:
                // se enseña a quién le va a llegar el mensaje.
                string lista = string.Join(", ", destinos);
                lblVacio.Text = $"Tu mensaje llegará a {destinos.Count} contactos:\n\n{lista}\n\n" +
                                $"Mantén pulsado {config.General.TeclaPTT} para hablar.";
            }
            else if (string.IsNullOrEmpty(selectedContact))
            {
                lblVacio.Text = "Elige un contacto de la lista.\n\n" +
                                "Con Ctrl puedes marcar varios a la vez.";
            }
            else
            {
                lblVacio.Text = $"Todavía no hay audios con {selectedContact}.\n\n" +
                                $"Mantén pulsado {config.General.TeclaPTT} para hablar.";
            }

            // Se oculta la lista en lugar de superponer el cartel: dos controles con
            // Dock=Fill en el mismo panel dependen del orden Z y el mensaje se quedaba
            // detrás de la lista vacía.
            bool vacio = lstHistorial.Items.Count == 0 || destinos.Count > 1;

            lblVacio.Visible = vacio;
            lstHistorial.Visible = !vacio;

            if (vacio) lblVacio.BringToFront();
        }

        private void BuildTrayMenu()
        {
            trayMenu.BackColor = Theme.Surface;
            trayMenu.ForeColor = Theme.Text;
            trayMenu.RenderMode = ToolStripRenderMode.System;

            var open = new ToolStripMenuItem("Abrir", null, (s, e) => RestoreFromTray());
            var settings = new ToolStripMenuItem("Configuración...", null, (s, e) => OpenSettings());
            var exit = new ToolStripMenuItem("Salir", null, (s, e) =>
            {
                exitRequested = true;
                Close();
            });

            trayMenu.Items.AddRange(new ToolStripItem[]
            {
                open, settings, new ToolStripSeparator(), exit
            });
        }

        private void StartEngine()
        {
            engine = new AudioEngine(config, userName);
            engine.TransmissionStarted += (s, destinos) => UiInvoke(() => OnTransmissionStarted(destinos));
            engine.TransmissionEnded += (s, items) => UiInvoke(() => OnTransmissionEnded(items));
            engine.ReceptionStarted += (s, e) => UiInvoke(() => OnReceptionStarted(e.Contact));
            engine.ReceptionEnded += (s, e) => UiInvoke(() => OnReceptionEnded(e));
            engine.InputLevel += (s, level) => OnInputLevel(level);
            engine.PlaybackFinished += (s, e) => UiInvoke(ResetPlayButton);
            engine.EngineError += (s, msg) => UiInvoke(() => ShowError(msg));

            engine.Start();
            volumeBeforeMute = config.Audio.Volumen;
            UpdateMuteButton();

            if (!engine.MicrophoneReady && config.Audio.MantenerMicrofonoAbierto)
            {
                lblMicHint.Text = $"Micrófono no disponible: {engine.MicrophoneError}";
                lblMicHint.ForeColor = Theme.Danger;
            }

            StartDiscovery();
        }

        private void StartDiscovery()
        {
            discovery?.Dispose();
            discovery = null;

            if (!config.General.DescubrimientoAutomatico) return;

            discovery = new DiscoveryService(config, userName);
            discovery.ContactDiscovered += (s, e) => UiInvoke(() => OnContactDiscovered(e));
            discovery.PresenceChanged += (s, e) => UiInvoke(() => OnPresenceChanged(e));
            discovery.Start();
        }

        /// <summary>
        /// Un equipo se ha anunciado en la red: si es nuevo entra en la lista solo,
        /// y si le cambió la IP se corrige sin tocar nada a mano.
        /// </summary>
        private void OnContactDiscovered(ContactDiscoveredEventArgs e)
        {
            if (e.IsNew)
            {
                LoadContacts(); // reconstruye la lista conservando la selección
                lblStatus.Text = $"Nuevo contacto encontrado en la red: {e.Name}";
            }

            var entry = FindContact(e.Name);
            if (entry != null)
            {
                entry.Ip = e.Ip;
                entry.Online = true;
                lstContactos.Invalidate();
            }

            UpdateStatus();
        }

        private void InstallHotKey()
        {
            hotKeyManager?.Dispose();
            hotKeyManager = new GlobalHotKeyManager(
                config.General.TeclaPTTKey,
                HandlePttKeyDown,
                HandlePttKeyUp,
                config.General.SuprimirTeclaGlobal);

            btnRecord.Text = $"MANTÉN PULSADO PARA HABLAR ({config.General.TeclaPTT})";

            if (!hotKeyManager.IsInstalled)
            {
                lblStatus.Text = "Aviso: no se pudo activar la tecla global; usa el botón.";
            }
        }

        // ------------------------------------------------------------------
        // Contactos e historial
        // ------------------------------------------------------------------

        private void LoadContacts()
        {
            string? previous = selectedContact;

            actualizandoSeleccion = true;
            lstContactos.BeginUpdate();
            lstContactos.Items.Clear();

            var externos = config.ContactosExternos(userName).OrderBy(c => c.Key).ToList();

            // Fila fija arriba para hablarle a toda la lista de una vez.
            if (externos.Count > 1)
                lstContactos.Items.Add(new ContactEntry { Name = "Todos", EsTodos = true });

            foreach (var c in externos)
            {
                lstContactos.Items.Add(new ContactEntry
                {
                    Name = c.Key,
                    Ip = c.Value,
                    // Al reconstruir la lista hay que recuperar el estado, o los
                    // contactos ya conectados aparecerían como desconectados.
                    Online = discovery?.IsOnline(c.Key) ?? false
                });
            }
            lstContactos.EndUpdate();
            actualizandoSeleccion = false;

            if (lstContactos.Items.Count == 0)
            {
                selectedContact = string.Empty;
                lblHistorial.Text = "HISTORIAL";
                lstHistorial.Items.Clear();
                UpdateRecordButtonEnabled();
                UpdateEmptyState();
                return;
            }

            int index = 0;
            if (!string.IsNullOrEmpty(previous))
            {
                for (int i = 0; i < lstContactos.Items.Count; i++)
                {
                    if (((ContactEntry)lstContactos.Items[i]!).Name.Equals(previous, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }
            lstContactos.SelectedIndex = index;
        }

        private void LoadHistory()
        {
            history.Clear();

            foreach (var (dir, direction) in new[]
            {
                (AppPaths.InboxDir(userName), AudioDirection.Recibido),
                (AppPaths.SentDir, AudioDirection.Enviado)
            })
            {
                if (!Directory.Exists(dir)) continue;

                foreach (string file in Directory.GetFiles(dir, "*.wav"))
                {
                    // Un envío a varios devuelve una entrada por destinatario.
                    foreach (var item in AudioItem.FromFile(file, direction))
                        AddToHistory(item, refreshUi: false);
                }
            }

            // Antes los elementos se insertaban en el orden en que los devolvía el
            // sistema de archivos; ahora la conversación queda cronológica.
            foreach (var list in history.Values)
                list.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

            ShowHistory();
        }

        private void AddToHistory(AudioItem item, bool refreshUi = true)
        {
            if (!history.TryGetValue(item.Contact, out var list))
            {
                list = new List<AudioItem>();
                history[item.Contact] = list;
            }
            list.Insert(0, item);

            if (!refreshUi) return;

            if (item.Contact.Equals(selectedContact, StringComparison.OrdinalIgnoreCase))
            {
                lstHistorial.Items.Insert(0, item);
                item.Unread = false;
                UpdateEmptyState();
            }
            else if (item.Unread)
            {
                var entry = FindContact(item.Contact);
                if (entry != null)
                {
                    entry.Unread++;
                    lstContactos.Invalidate();
                }
            }
        }

        private void ShowHistory()
        {
            lstHistorial.BeginUpdate();
            lstHistorial.Items.Clear();

            if (!string.IsNullOrEmpty(selectedContact) &&
                history.TryGetValue(selectedContact, out var items))
            {
                foreach (var item in items)
                {
                    item.Unread = false;
                    lstHistorial.Items.Add(item);
                }
            }
            lstHistorial.EndUpdate();

            int destinos = Destinatarios().Count;

            lblHistorial.Text = destinos > 1
                ? $"HABLARÁS CON {destinos} CONTACTOS"
                : string.IsNullOrEmpty(selectedContact)
                    ? "HISTORIAL"
                    : $"CONVERSACIÓN CON {selectedContact.ToUpperInvariant()}";

            UpdateEmptyState();
        }

        private ContactEntry? FindContact(string name)
        {
            foreach (ContactEntry entry in lstContactos.Items)
            {
                if (entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return entry;
            }
            return null;
        }

        private void lstContactos_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (actualizandoSeleccion) return;
            if (lstContactos.SelectedItem is not ContactEntry entry) return;

            // "Todos" es excluyente: mezclarlo con contactos sueltos no significa nada.
            bool todosSeleccionado = lstContactos.SelectedItems
                .Cast<ContactEntry>().Any(c => c.EsTodos);

            if (todosSeleccionado && lstContactos.SelectedItems.Count > 1)
            {
                actualizandoSeleccion = true;
                for (int i = 0; i < lstContactos.Items.Count; i++)
                {
                    bool esTodos = ((ContactEntry)lstContactos.Items[i]!).EsTodos;
                    lstContactos.SetSelected(i, esTodos);
                }
                actualizandoSeleccion = false;
            }

            var destinos = Destinatarios();

            // Para el historial se usa un solo contacto: con varios elegidos se
            // muestra el resumen del grupo en lugar de una conversación concreta.
            selectedContact = destinos.Count == 1 ? destinos[0] : string.Empty;

            foreach (ContactEntry c in lstContactos.SelectedItems)
                c.Unread = 0;

            ShowHistory();
            UpdateRecordButtonEnabled();
            lstContactos.Invalidate();
        }

        /// <summary>Contactos a los que se enviaría el audio ahora mismo.</summary>
        private List<string> Destinatarios()
        {
            var seleccion = lstContactos.SelectedItems.Cast<ContactEntry>().ToList();

            if (seleccion.Any(c => c.EsTodos))
            {
                return lstContactos.Items.Cast<ContactEntry>()
                    .Where(c => !c.EsTodos)
                    .Select(c => c.Name)
                    .ToList();
            }

            return seleccion.Select(c => c.Name).ToList();
        }

        private void UpdateRecordButtonEnabled()
        {
            int destinos = Destinatarios().Count;
            bool ready = destinos > 0;

            btnRecord.Enabled = ready;
            btnRecord.BackColor = ready ? Theme.Accent : Theme.SurfaceAlt;

            if (!engine?.IsTransmitting ?? true)
            {
                btnRecord.Text = destinos > 1
                    ? $"MANTÉN PULSADO PARA HABLAR CON {destinos} CONTACTOS ({config.General.TeclaPTT})"
                    : $"MANTÉN PULSADO PARA HABLAR ({config.General.TeclaPTT})";
            }
        }

        // ------------------------------------------------------------------
        // Dibujo de las listas
        // ------------------------------------------------------------------

        private void lstContactos_DrawItem(object? sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= lstContactos.Items.Count) return;
            if (lstContactos.Items[e.Index] is not ContactEntry entry) return;

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var bg = new SolidBrush(selected ? Theme.Accent : Theme.Surface))
                g.FillRectangle(bg, e.Bounds);

            if (entry.EsTodos)
            {
                using var iconBrush = new SolidBrush(selected ? Color.White : Theme.Accent);
                g.DrawString("📢", Theme.FontBase, iconBrush, e.Bounds.Left + 6, e.Bounds.Top + 10);

                using var todosBrush = new SolidBrush(selected ? Color.White : Theme.Text);
                g.DrawString("Todos", Theme.FontSemibold, todosBrush,
                    e.Bounds.Left + 28, e.Bounds.Top + 4);

                int cuantos = lstContactos.Items.Count - 1;
                using var todosSub = new SolidBrush(selected ? Color.FromArgb(220, 235, 255) : Theme.TextMuted);
                g.DrawString($"hablar con los {cuantos}", Theme.FontSmall, todosSub,
                    e.Bounds.Left + 28, e.Bounds.Top + 21);

                // Separador para despegarla de los contactos reales.
                using var linea = new Pen(Theme.Border);
                g.DrawLine(linea, e.Bounds.Left + 8, e.Bounds.Bottom - 1,
                    e.Bounds.Right - 8, e.Bounds.Bottom - 1);
                return;
            }

            // Punto de estado: verde = en línea (recibe latidos), gris = sin señal.
            var dotColor = entry.Talking ? Theme.Danger : (entry.Online ? Theme.Online : Theme.Offline);
            using (var dot = new SolidBrush(dotColor))
                g.FillEllipse(dot, e.Bounds.Left + 10, e.Bounds.Top + e.Bounds.Height / 2 - 4, 9, 9);

            using var nameBrush = new SolidBrush(selected ? Color.White : Theme.Text);
            g.DrawString(entry.Name, Theme.FontSemibold, nameBrush,
                e.Bounds.Left + 28, e.Bounds.Top + 4);

            string sub = entry.Talking ? "hablando..." : (entry.Online ? "en línea" : "sin conexión");
            using var subBrush = new SolidBrush(selected ? Color.FromArgb(220, 235, 255) : Theme.TextMuted);
            g.DrawString(sub, Theme.FontSmall, subBrush, e.Bounds.Left + 28, e.Bounds.Top + 21);

            // Globo con el número de audios sin escuchar.
            if (entry.Unread > 0)
            {
                string badge = entry.Unread > 9 ? "9+" : entry.Unread.ToString();
                var rect = new Rectangle(e.Bounds.Right - 32, e.Bounds.Top + 11, 20, 18);
                using (var badgeBrush = new SolidBrush(Theme.Danger))
                    g.FillEllipse(badgeBrush, rect);
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(badge, Theme.FontSmall, Brushes.White, rect, sf);
            }
        }

        private void lstHistorial_DrawItem(object? sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= lstHistorial.Items.Count) return;
            if (lstHistorial.Items[e.Index] is not AudioItem item) return;

            var g = e.Graphics;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var bg = new SolidBrush(selected ? Theme.SurfaceAlt : Theme.Surface))
                g.FillRectangle(bg, e.Bounds);

            bool incoming = item.Direction == AudioDirection.Recibido;
            var accent = incoming ? Theme.Incoming : Theme.Outgoing;

            // Barra de color a la izquierda: antes enviados y recibidos se veían igual
            // y en los enviados el nombre mostrado era en realidad el destinatario.
            using (var bar = new SolidBrush(accent))
                g.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top + 6, 3, e.Bounds.Height - 12);

            using (var arrow = new SolidBrush(accent))
                g.DrawString(incoming ? "◀" : "▶", Theme.FontSmall, arrow, e.Bounds.Left + 10, e.Bounds.Top + 9);

            // En los enviados a varios se ve a cuánta gente fue; en los recibidos,
            // si te hablaban solo a ti o al grupo.
            string title = incoming
                ? (item.EsGrupo ? $"{item.Contact}  (a varios)" : item.Contact)
                : $"Tú → {item.RecipientsText}";

            using (var titleBrush = new SolidBrush(Theme.Text))
                g.DrawString(title, Theme.FontBase, titleBrush, e.Bounds.Left + 30, e.Bounds.Top + 8);

            string meta = string.IsNullOrEmpty(item.DurationText)
                ? item.TimeText
                : $"{item.TimeText} · {item.DurationText}";

            using var metaBrush = new SolidBrush(Theme.TextMuted);
            using var sf2 = new StringFormat { Alignment = StringAlignment.Far };
            g.DrawString(meta, Theme.FontSmall, metaBrush,
                new RectangleF(e.Bounds.Right - 130, e.Bounds.Top + 10, 120, 20), sf2);
        }

        // ------------------------------------------------------------------
        // Pulsar para hablar
        // ------------------------------------------------------------------

        private void HandlePttKeyDown()
        {
            UiInvoke(() =>
            {
                if (pttFromKey || pttFromMouse) return;
                pttFromKey = true;
                if (!StartTalking()) pttFromKey = false;
            });
        }

        private void HandlePttKeyUp()
        {
            UiInvoke(() =>
            {
                if (!pttFromKey) return;
                pttFromKey = false;
                engine.StopTransmit();
            });
        }

        private void btnRecord_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (pttFromKey || pttFromMouse) return;

            pttFromMouse = true;
            if (!StartTalking()) pttFromMouse = false;
        }

        private void btnRecord_MouseUp(object? sender, MouseEventArgs e)
        {
            if (!pttFromMouse) return;
            pttFromMouse = false;
            engine.StopTransmit();
        }

        private bool StartTalking()
        {
            var destinos = Destinatarios();

            if (destinos.Count == 0)
            {
                lblStatus.Text = "Selecciona un contacto antes de hablar.";
                return false;
            }

            if (!engine.StartTransmit(destinos))
            {
                lblStatus.Text = engine.MicrophoneReady
                    ? "No se pudo iniciar la transmisión."
                    : $"Micrófono no disponible: {engine.MicrophoneError}";
                return false;
            }
            return true;
        }

        private void OnTransmissionStarted(IReadOnlyList<string> destinos)
        {
            string quien = destinos.Count == 1
                ? destinos[0].ToUpperInvariant()
                : $"{destinos.Count} CONTACTOS";

            btnRecord.Text = $"SUELTA PARA TERMINAR — HABLANDO CON {quien}";
            btnRecord.BackColor = Theme.Danger;
            btnRecord.FlatAppearance.MouseOverBackColor = Theme.DangerHover;

            lblAir.Text = "● AL AIRE";
            airBlinkTimer.Start();

            lblStatus.Text = destinos.Count == 1
                ? $"Transmitiendo a {destinos[0]}..."
                : $"Transmitiendo a {string.Join(", ", destinos)}...";
        }

        private void OnTransmissionEnded(IReadOnlyList<AudioItem> items)
        {
            btnRecord.BackColor = Destinatarios().Count == 0 ? Theme.SurfaceAlt : Theme.Accent;
            btnRecord.FlatAppearance.MouseOverBackColor = Theme.AccentHover;

            airBlinkTimer.Stop();
            lblAir.Text = string.Empty;
            lblAir.ForeColor = Theme.Surface;

            // Una entrada en la conversación de cada destinatario (mismo archivo).
            foreach (var item in items) AddToHistory(item);

            UpdateRecordButtonEnabled();
            UpdateStatus();

            if (items.Count > 1)
                lblStatus.Text = $"Mensaje enviado a {items.Count} contactos.";
        }

        private void OnReceptionStarted(string contact)
        {
            var entry = FindContact(contact);
            if (entry != null)
            {
                entry.Talking = true;
                entry.Online = true;
                lstContactos.Invalidate();
            }

            lblStatus.Text = $"Recibiendo de {contact}...";

            if (!IsForeground())
            {
                FlashTaskbar();
                if (config.General.MinimizarABandeja && !Visible)
                {
                    trayIcon.ShowBalloonTip(2500, "Walkie Talkie",
                        $"{contact} te está hablando", ToolTipIcon.Info);
                }
            }
        }

        private void OnReceptionEnded(ReceptionEventArgs e)
        {
            var entry = FindContact(e.Contact);
            if (entry != null)
            {
                entry.Talking = false;
                lstContactos.Invalidate();
            }

            // Antes se cambiaba solo el contacto seleccionado al recibir audio: si
            // estabas grabando, tu mensaje se iba a otra persona.
            if (e.Item != null) AddToHistory(e.Item);
            UpdateStatus();
        }

        private void OnPresenceChanged(PresenceEventArgs e)
        {
            var entry = FindContact(e.Contact);
            if (entry != null)
            {
                entry.Online = e.Online;
                lstContactos.Invalidate();
            }
            UpdateStatus();
        }

        private void OnInputLevel(float level)
        {
            // Llega ~25 veces por segundo desde el hilo de audio: se limita para
            // no saturar la cola de mensajes de la interfaz.
            var now = DateTime.UtcNow;
            if ((now - lastLevelUpdate).TotalMilliseconds < 45) return;
            lastLevelUpdate = now;

            UiInvoke(() => vuMeter.SetLevel(level));
        }

        private void UpdateStatus()
        {
            if (engine == null) return;

            int online = lstContactos.Items.Cast<ContactEntry>().Count(c => c.Online);
            int total = lstContactos.Items.Count;
            bool buscando = discovery?.IsRunning ?? false;

            lblUserStatus.Text = total == 0
                ? (buscando ? "Buscando equipos en la red..." : "Sin contactos configurados")
                : $"{online} de {total} en línea · puerto {config.General.Puerto}";

            if (!engine.IsTransmitting)
                lblStatus.Text = total == 0
                    ? (buscando
                        ? "Buscando compañeros automáticamente. Abre la app en otro equipo para que aparezca aquí."
                        : "Añade contactos desde ⚙ Configuración")
                    : "Listo";
        }

        // ------------------------------------------------------------------
        // Reproducción
        // ------------------------------------------------------------------

        private void btnPlay_Click(object? sender, EventArgs e) => TogglePlay();

        private void lstHistorial_DoubleClick(object? sender, EventArgs e) => TogglePlay();

        private void lstHistorial_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                TogglePlay();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedAudio();
                e.Handled = true;
            }
        }

        private void TogglePlay()
        {
            if (engine.IsPlayingFile)
            {
                engine.StopPlayback();
                return;
            }

            if (lstHistorial.SelectedItem is not AudioItem item)
            {
                lblStatus.Text = "Selecciona un audio del historial para reproducirlo.";
                return;
            }

            if (!File.Exists(item.FilePath))
            {
                ShowError("El archivo de audio ya no existe.");
                lstHistorial.Items.Remove(item);
                if (history.TryGetValue(item.Contact, out var list)) list.Remove(item);
                UpdateEmptyState();
                return;
            }

            if (engine.PlayFile(item.FilePath))
            {
                btnPlay.Text = "■";
                btnPlay.ForeColor = Theme.Danger;
                lblStatus.Text = $"Reproduciendo audio de {item.Contact} ({item.TimeText})";
            }
        }

        private void ResetPlayButton()
        {
            btnPlay.Text = "▶";
            btnPlay.ForeColor = Theme.Text;
            UpdateStatus();
        }

        private void DeleteSelectedAudio()
        {
            if (lstHistorial.SelectedItem is not AudioItem item) return;

            var answer = MessageBox.Show(
                $"¿Eliminar el audio de {item.Contact} ({item.TimeText})?",
                "Eliminar audio", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes) return;

            try
            {
                if (engine.IsPlayingFile) engine.StopPlayback();
                if (File.Exists(item.FilePath)) File.Delete(item.FilePath);

                lstHistorial.Items.Remove(item);
                if (history.TryGetValue(item.Contact, out var list)) list.Remove(item);
                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                ShowError($"No se pudo eliminar: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Barra superior
        // ------------------------------------------------------------------

        private void btnMute_Click(object? sender, EventArgs e)
        {
            bool muting = config.Audio.Volumen > 0;

            if (muting)
            {
                volumeBeforeMute = config.Audio.Volumen;
                config.Audio.Volumen = 0;
            }
            else
            {
                config.Audio.Volumen = volumeBeforeMute > 0 ? volumeBeforeMute : 100;
            }

            engine.ApplyVolume(config.Audio.Volumen);
            config.Save();
            UpdateMuteButton();
        }

        private void UpdateMuteButton()
        {
            bool muted = config.Audio.Volumen == 0;
            btnMute.Text = muted ? "🔇" : "🔊";
            btnMute.ForeColor = muted ? Theme.Danger : Theme.Text;
            toolTip.SetToolTip(btnMute, muted ? "Activar sonido" : "Silenciar");
        }

        private readonly ToolTip toolTip = new();

        private void btnSettings_Click(object? sender, EventArgs e) => OpenSettings();

        private void OpenSettings()
        {
            using var dlg = new SettingsForm(config, userName);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            config.Save();

            if (dlg.CambiarUsuarioSolicitado)
            {
                try { File.Delete(AppPaths.UserFile); } catch { }
                exitRequested = true;
                Application.Restart();
                return;
            }

            LoadContacts();
            InstallHotKey();
            engine.ApplyVolume(config.Audio.Volumen);
            UpdateMuteButton();

            if (dlg.RequiereReinicioDescubrimiento) StartDiscovery();

            if (dlg.RequiereReinicioAudio)
            {
                engine.RestartAudio();
                lblMicHint.Text = engine.MicrophoneReady ? "Micrófono" : $"Micrófono no disponible: {engine.MicrophoneError}";
                lblMicHint.ForeColor = engine.MicrophoneReady ? Theme.TextMuted : Theme.Danger;
            }

            if (dlg.RequiereReinicioRed)
            {
                MessageBox.Show(this,
                    "El cambio de puerto se aplicará al reiniciar la aplicación.",
                    "Configuración", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            UpdateStatus();
        }

        // ------------------------------------------------------------------
        // Bandeja del sistema
        // ------------------------------------------------------------------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Sin esto el foco caía en el botón de configuración y un Enter
            // despistado abría el diálogo.
            if (lstContactos.Items.Count > 0) ActiveControl = lstContactos;
        }

        private void trayIcon_DoubleClick(object? sender, EventArgs e) => RestoreFromTray();

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (config.General.MinimizarABandeja && WindowState == FormWindowState.Minimized)
            {
                Hide();
                if (!trayHintShown)
                {
                    trayHintShown = true;
                    trayIcon.ShowBalloonTip(2000, "Walkie Talkie",
                        "Sigo funcionando aquí. La tecla para hablar sigue activa.",
                        ToolTipIcon.Info);
                }
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);

            // Si se pierde el foco con el botón pulsado, el MouseUp nunca llega
            // y el micrófono se quedaba transmitiendo.
            if (pttFromMouse)
            {
                pttFromMouse = false;
                engine.StopTransmit();
            }
        }

        // ------------------------------------------------------------------
        // Utilidades
        // ------------------------------------------------------------------

        private void UiInvoke(Action action)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;

                if (InvokeRequired)
                {
                    // BeginInvoke y no Invoke: con Invoke el hilo de red se quedaba
                    // esperando a la interfaz en cada paquete recibido.
                    BeginInvoke(new Action(() =>
                    {
                        try { action(); }
                        catch (Exception ex) { AppPaths.Log(AppPaths.CrashLog, $"UI: {ex}"); }
                    }));
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.CrashLog, $"UiInvoke: {ex}");
            }
        }

        private void ShowError(string message)
        {
            lblStatus.Text = message;
            AppPaths.Log(AppPaths.CrashLog, message);
        }

        /// <summary>
        /// Si alguien vuelve a abrir el ejecutable estando ya en marcha, la segunda
        /// instancia manda este mensaje y aquí se restaura la ventana.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.ShowMeMessage && m.Msg != 0)
            {
                RestoreFromTray();
            }
            base.WndProc(ref m);
        }

        private bool IsForeground() => GetForegroundWindow() == this.Handle;

        private void FlashTaskbar()
        {
            var info = new FLASHWINFO
            {
                cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(FLASHWINFO))),
                hwnd = this.Handle,
                dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
                uCount = 3,
                dwTimeout = 0
            };
            FlashWindowEx(ref info);
        }

        private const uint FLASHW_TRAY = 2;
        private const uint FLASHW_TIMERNOFG = 12;

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exitRequested &&
                config.General.MinimizarABandeja &&
                e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();

                if (!trayHintShown)
                {
                    trayHintShown = true;
                    trayIcon.ShowBalloonTip(2500, "Walkie Talkie",
                        "La aplicación sigue activa aquí. Usa Salir en este icono para cerrarla.",
                        ToolTipIcon.Info);
                }
                return;
            }

            airBlinkTimer.Stop();
            airBlinkTimer.Dispose();
            hotKeyManager?.Dispose();
            discovery?.Dispose();
            engine?.Dispose();

            trayIcon.Visible = false;

            base.OnFormClosing(e);
        }
    }
}
