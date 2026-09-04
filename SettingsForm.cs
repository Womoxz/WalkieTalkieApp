#pragma warning disable CA1416 // API sólo de Windows

using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Configuración dentro de la app: antes había que editar appsettings.json a
    /// mano y borrar user.txt para cambiar de usuario.
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly AppConfig config;
        private readonly string userName;

        public bool CambiarUsuarioSolicitado { get; private set; }
        public bool RequiereReinicioAudio { get; private set; }
        public bool RequiereReinicioRed { get; private set; }
        public bool RequiereReinicioDescubrimiento { get; private set; }

        /// <summary>El usuario pidió comprobar actualizaciones en ese momento.</summary>
        public bool BuscarActualizacionSolicitado { get; private set; }

        private readonly bool originalDescubrir;
        private readonly int originalPuertoDesc;
        private readonly int originalSampleRate;
        private readonly int originalInput;
        private readonly int originalOutput;
        private readonly int originalBuffer;
        private readonly int originalPort;
        private readonly bool originalKeepMic;

        private ListView lstContactos = null!;
        private ComboBox cmbInput = null!;
        private ComboBox cmbOutput = null!;
        private ComboBox cmbCalidad = null!;
        private TrackBar trkVolumen = null!;
        private Label lblVolumenValor = null!;
        private CheckBox chkSonidos = null!;
        private CheckBox chkMicAbierto = null!;
        private ComboBox cmbTecla = null!;
        private CheckBox chkSuprimir = null!;
        private CheckBox chkBandeja = null!;
        private CheckBox chkSoloConocidos = null!;
        private CheckBox chkDescubrir = null!;
        private Button btnBuscarActualizacion = null!;
        private CheckBox chkActualizar = null!;
        private CheckBox chkInstalarAlCerrar = null!;
        private CheckBox chkVentanaRespuesta = null!;
        private CheckBox chkTeclaUltimo = null!;
        private CheckBox chkGuardarDescubiertos = null!;
        private NumericUpDown numPuertoDesc = null!;
        private NumericUpDown numPuerto = null!;
        private NumericUpDown numRetencion = null!;
        private NumericUpDown numMaxTx = null!;

        public SettingsForm(AppConfig config, string userName)
        {
            this.config = config;
            this.userName = userName;

            originalSampleRate = config.Audio.SampleRate;
            originalInput = config.Audio.InputDevice;
            originalOutput = config.Audio.OutputDevice;
            originalBuffer = config.Audio.BufferMilliseconds;
            originalPort = config.General.Puerto;
            originalKeepMic = config.Audio.MantenerMicrofonoAbierto;
            originalDescubrir = config.General.DescubrimientoAutomatico;
            originalPuertoDesc = config.General.PuertoDescubrimiento;

            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            Text = "Configuración";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(540, 560);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.FontBase;

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                ItemSize = new Size(120, 30),
                SizeMode = TabSizeMode.Fixed,
                Padding = new Point(0, 0)
            };
            tabs.DrawItem += Tabs_DrawItem;

            tabs.TabPages.Add(BuildContactsTab());
            tabs.TabPages.Add(BuildAudioTab());
            tabs.TabPages.Add(BuildGeneralTab());

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Theme.Background,
                Padding = new Padding(12)
            };

            var btnCancel = new Button { Text = "Cancelar", Size = new Size(100, 32), DialogResult = DialogResult.Cancel };
            btnCancel.Location = new Point(footer.Width - 112, 12);
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Theme.StyleSecondaryButton(btnCancel);

            var btnOk = new Button { Text = "Guardar", Size = new Size(100, 32) };
            btnOk.Location = new Point(footer.Width - 220, 12);
            btnOk.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Theme.StyleButton(btnOk, Theme.Accent, Theme.AccentHover);
            btnOk.Click += Guardar;

            var btnCambiarUsuario = new Button { Text = "Cambiar usuario...", Size = new Size(150, 32) };
            btnCambiarUsuario.Location = new Point(12, 12);
            Theme.StyleSecondaryButton(btnCambiarUsuario);
            btnCambiarUsuario.Click += (s, e) =>
            {
                var answer = MessageBox.Show(this,
                    $"Ahora estás conectado como «{userName}».\n\n" +
                    "Se cerrará la sesión y la aplicación se reiniciará para elegir otro usuario.\n" +
                    "El historial de audios se conserva.",
                    "Cambiar usuario", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

                if (answer == DialogResult.OK)
                {
                    CambiarUsuarioSolicitado = true;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            footer.Controls.AddRange(new Control[] { btnCambiarUsuario, btnOk, btnCancel });

            Controls.Add(tabs);
            Controls.Add(footer);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tabs = (TabControl)sender!;
            var page = tabs.TabPages[e.Index];
            bool selected = tabs.SelectedIndex == e.Index;

            using var bg = new SolidBrush(selected ? Theme.Surface : Theme.Background);
            e.Graphics.FillRectangle(bg, e.Bounds);

            if (selected)
            {
                using var accent = new SolidBrush(Theme.Accent);
                e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Bottom - 3, e.Bounds.Width, 3);
            }

            using var text = new SolidBrush(selected ? Theme.Text : Theme.TextMuted);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            e.Graphics.DrawString(page.Text, Theme.FontBase, text, e.Bounds, sf);
        }

        private TabPage NewPage(string title) => new(title)
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Padding = new Padding(16)
        };

        // ------------------------------------------------------------------
        // Pestaña Contactos
        // ------------------------------------------------------------------

        private TabPage BuildContactsTab()
        {
            var page = NewPage("Contactos");

            lstContactos = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(16, 16),
                Size = new Size(388, 330),
                MultiSelect = false
            };
            lstContactos.Columns.Add("Nombre", 180);
            lstContactos.Columns.Add("Dirección IP", 190);
            lstContactos.DoubleClick += (s, e) => EditarContacto();

            var btnAdd = new Button { Text = "Añadir", Location = new Point(414, 16), Size = new Size(104, 30) };
            var btnEdit = new Button { Text = "Editar", Location = new Point(414, 52), Size = new Size(104, 30) };
            var btnDel = new Button { Text = "Quitar", Location = new Point(414, 88), Size = new Size(104, 30) };

            Theme.StyleButton(btnAdd, Theme.Accent, Theme.AccentHover);
            Theme.StyleSecondaryButton(btnEdit);
            Theme.StyleSecondaryButton(btnDel);

            btnAdd.Click += (s, e) => AgregarContacto();
            btnEdit.Click += (s, e) => EditarContacto();
            btnDel.Click += (s, e) => QuitarContacto();

            chkDescubrir = Check("Buscar equipos en la red automáticamente", 16, 356);
            chkDescubrir.Size = new Size(476, 22);

            chkGuardarDescubiertos = Check("Recordar los equipos encontrados", 36, 382);
            chkGuardarDescubiertos.Size = new Size(456, 22);

            var hint = new Label
            {
                Text = "Con la búsqueda automática no hace falta escribir ninguna IP: los equipos "
                     + "se encuentran solos y se corrigen si el router les cambia la dirección.",
                Location = new Point(16, 410),
                Size = new Size(496, 40),
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            };

            page.Controls.AddRange(new Control[]
            {
                lstContactos, btnAdd, btnEdit, btnDel, chkDescubrir, chkGuardarDescubiertos, hint
            });
            return page;
        }

        private void AgregarContacto()
        {
            using var dlg = new ContactDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (config.Contactos.ContainsKey(dlg.ContactName))
            {
                MessageBox.Show(this, "Ya existe un contacto con ese nombre.", "Contactos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            config.Contactos[dlg.ContactName] = dlg.ContactIp;
            RefrescarContactos();
        }

        private void EditarContacto()
        {
            if (lstContactos.SelectedItems.Count == 0) return;

            var item = lstContactos.SelectedItems[0];
            string oldName = item.Tag as string ?? item.Text;

            using var dlg = new ContactDialog(oldName, item.SubItems[1].Text);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            config.Contactos.Remove(oldName);
            config.Contactos[dlg.ContactName] = dlg.ContactIp;
            RefrescarContactos();
        }

        private void QuitarContacto()
        {
            if (lstContactos.SelectedItems.Count == 0) return;

            var selected = lstContactos.SelectedItems[0];
            string name = selected.Tag as string ?? selected.Text;

            var answer = MessageBox.Show(this, $"¿Quitar a «{name}» de la lista de contactos?",
                "Contactos", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes) return;

            config.Contactos.Remove(name);
            RefrescarContactos();
        }

        private void RefrescarContactos()
        {
            lstContactos.BeginUpdate();
            lstContactos.Items.Clear();

            foreach (var c in config.Contactos.OrderBy(c => c.Key))
            {
                bool esYo = string.Equals(c.Key, userName, StringComparison.OrdinalIgnoreCase);

                var item = new ListViewItem(esYo ? $"{c.Key}   (tú)" : c.Key)
                {
                    // El texto visible lleva el sufijo; el nombre real va en Tag
                    // para que editar y quitar sigan funcionando.
                    Tag = c.Key
                };
                item.SubItems.Add(c.Value);
                if (esYo) item.ForeColor = Theme.Accent;

                lstContactos.Items.Add(item);
            }
            lstContactos.EndUpdate();
        }

        // ------------------------------------------------------------------
        // Pestaña Audio
        // ------------------------------------------------------------------

        private TabPage BuildAudioTab()
        {
            var page = NewPage("Audio");
            int y = 16;

            page.Controls.Add(Etiqueta("Micrófono", 16, y));
            cmbInput = Combo(16, y + 20, 470);
            page.Controls.Add(cmbInput);
            y += 60;

            page.Controls.Add(Etiqueta("Altavoces", 16, y));
            cmbOutput = Combo(16, y + 20, 470);
            page.Controls.Add(cmbOutput);
            y += 60;

            page.Controls.Add(Etiqueta("Calidad", 16, y));
            cmbCalidad = Combo(16, y + 20, 470);
            cmbCalidad.Items.AddRange(new object[]
            {
                "Radio — 8 kHz (mínimo consumo de red)",
                "Voz — 16 kHz (recomendado)",
                "Alta — 22 kHz",
                "Máxima — 44 kHz (mucho ancho de banda)"
            });
            page.Controls.Add(cmbCalidad);
            y += 60;

            page.Controls.Add(Etiqueta("Volumen de recepción", 16, y));
            trkVolumen = new TrackBar
            {
                Location = new Point(14, y + 18),
                Size = new Size(420, 40),
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 10,
                BackColor = Theme.Surface
            };
            lblVolumenValor = new Label
            {
                Location = new Point(440, y + 24),
                Size = new Size(50, 20),
                ForeColor = Theme.TextMuted
            };
            trkVolumen.ValueChanged += (s, e) => lblVolumenValor.Text = $"{trkVolumen.Value}%";
            page.Controls.Add(trkVolumen);
            page.Controls.Add(lblVolumenValor);
            y += 66;

            chkSonidos = Check("Reproducir sonidos de aviso", 16, y);
            page.Controls.Add(chkSonidos);
            y += 28;

            chkMicAbierto = Check("Mantener el micrófono abierto (evita cortar el inicio de cada frase)", 16, y);
            page.Controls.Add(chkMicAbierto);

            return page;
        }

        // ------------------------------------------------------------------
        // Pestaña General
        // ------------------------------------------------------------------

        private TabPage BuildGeneralTab()
        {
            var page = NewPage("General");
            int y = 16;

            page.Controls.Add(Etiqueta("Tecla para hablar", 16, y));
            cmbTecla = Combo(16, y + 20, 200);
            for (int i = 1; i <= 12; i++) cmbTecla.Items.Add($"F{i}");
            cmbTecla.Items.AddRange(new object[] { "Insert", "Home", "End", "PageUp", "PageDown", "Pause" });
            page.Controls.Add(cmbTecla);
            y += 56;

            chkSuprimir = Check("Bloquear esa tecla en el resto de programas", 16, y);
            chkSuprimir.Size = new Size(470, 22);
            page.Controls.Add(chkSuprimir);
            y += 24;

            page.Controls.Add(new Label
            {
                Text = "Si lo activas, la tecla dejará de funcionar en Excel, el navegador, etc.",
                Location = new Point(36, y),
                Size = new Size(450, 18),
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            });
            y += 32;

            page.Controls.Add(Etiqueta("Puerto de voz", 16, y));
            numPuerto = Numero(16, y + 20, 1024, 65535);
            page.Controls.Add(numPuerto);

            page.Controls.Add(Etiqueta("Puerto de búsqueda", 150, y));
            numPuertoDesc = Numero(150, y + 20, 1024, 65535);
            page.Controls.Add(numPuertoDesc);

            page.Controls.Add(Etiqueta("Corte seguridad (s)", 290, y));
            numMaxTx = Numero(290, y + 20, 5, 600);
            page.Controls.Add(numMaxTx);
            y += 56;

            page.Controls.Add(Etiqueta("Borrar audios con más de (días, 0 = nunca)", 16, y));
            numRetencion = Numero(16, y + 20, 0, 3650);
            page.Controls.Add(numRetencion);
            y += 60;

            chkBandeja = Check("Minimizar a la bandeja del sistema al cerrar", 16, y);
            chkBandeja.Size = new Size(470, 22);
            page.Controls.Add(chkBandeja);
            y += 28;

            chkSoloConocidos = Check("Aceptar audio solo de los contactos de la lista", 16, y);
            chkSoloConocidos.Size = new Size(470, 22);
            page.Controls.Add(chkSoloConocidos);
            y += 30;

            chkVentanaRespuesta = Check("Avisar con una ventana para responder cuando alguien te hable", 16, y);
            chkVentanaRespuesta.Size = new Size(470, 22);
            page.Controls.Add(chkVentanaRespuesta);
            y += 26;

            chkTeclaUltimo = Check("Con esa ventana abierta, la tecla responde a quien acaba de hablar", 36, y);
            chkTeclaUltimo.Size = new Size(450, 22);
            page.Controls.Add(chkTeclaUltimo);

            chkVentanaRespuesta.CheckedChanged += (s, e) =>
                chkTeclaUltimo.Enabled = chkVentanaRespuesta.Checked;
            y += 32;

            chkActualizar = Check("Buscar actualizaciones y descargarlas automáticamente", 16, y);
            chkActualizar.Size = new Size(470, 22);
            page.Controls.Add(chkActualizar);
            y += 26;

            chkInstalarAlCerrar = Check("Instalarlas al cerrar la aplicación", 36, y);
            chkInstalarAlCerrar.Size = new Size(450, 22);
            page.Controls.Add(chkInstalarAlCerrar);

            chkActualizar.CheckedChanged += (s, e) =>
            {
                chkInstalarAlCerrar.Enabled = chkActualizar.Checked;
                btnBuscarActualizacion.Enabled = chkActualizar.Checked;
            };
            y += 28;

            btnBuscarActualizacion = new Button
            {
                Text = "Buscar ahora",
                Location = new Point(36, y),
                Size = new Size(130, 28)
            };
            Theme.StyleSecondaryButton(btnBuscarActualizacion);
            btnBuscarActualizacion.Click += (s, e) =>
            {
                // Se cierra la ventana para que se vea la barra de aviso detrás.
                BuscarActualizacionSolicitado = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            page.Controls.Add(btnBuscarActualizacion);

            var lblVersion = new Label
            {
                Text = $"Versión instalada: {UpdateService.VersionActual}",
                Location = new Point(176, y + 6),
                Size = new Size(300, 20),
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            };
            page.Controls.Add(lblVersion);

            return page;
        }

        // ------------------------------------------------------------------
        // Helpers de construcción
        // ------------------------------------------------------------------

        private Label Etiqueta(string text, int x, int y) => new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(300, 18),
            ForeColor = Theme.TextMuted,
            Font = Theme.FontSmall
        };

        private ComboBox Combo(int x, int y, int width) => new()
        {
            Location = new Point(x, y),
            Size = new Size(width, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.SurfaceAlt,
            ForeColor = Theme.Text,
            FlatStyle = FlatStyle.Flat
        };

        private CheckBox Check(string text, int x, int y) => new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(440, 22),
            ForeColor = Theme.Text,
            FlatStyle = FlatStyle.Flat
        };

        private NumericUpDown Numero(int x, int y, int min, int max) => new()
        {
            Location = new Point(x, y),
            Size = new Size(120, 24),
            Minimum = min,
            Maximum = max,
            BackColor = Theme.SurfaceAlt,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };

        // ------------------------------------------------------------------
        // Carga y guardado
        // ------------------------------------------------------------------

        private void LoadValues()
        {
            RefrescarContactos();

            cmbInput.Items.Add("Predeterminado de Windows");
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                try { cmbInput.Items.Add(WaveInEvent.GetCapabilities(i).ProductName); }
                catch { cmbInput.Items.Add($"Dispositivo {i}"); }
            }
            cmbInput.SelectedIndex = (config.Audio.InputDevice >= 0 &&
                                      config.Audio.InputDevice < cmbInput.Items.Count - 1)
                ? config.Audio.InputDevice + 1 : 0;

            cmbOutput.Items.Add("Predeterminado de Windows");
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                try { cmbOutput.Items.Add(WaveOut.GetCapabilities(i).ProductName); }
                catch { cmbOutput.Items.Add($"Dispositivo {i}"); }
            }
            cmbOutput.SelectedIndex = (config.Audio.OutputDevice >= 0 &&
                                       config.Audio.OutputDevice < cmbOutput.Items.Count - 1)
                ? config.Audio.OutputDevice + 1 : 0;

            cmbCalidad.SelectedIndex = config.Audio.SampleRate switch
            {
                8000 => 0,
                16000 => 1,
                22050 => 2,
                44100 => 3,
                _ => 1
            };

            trkVolumen.Value = Math.Clamp(config.Audio.Volumen, 0, 100);
            lblVolumenValor.Text = $"{trkVolumen.Value}%";
            chkSonidos.Checked = config.Audio.SonidosDeAviso;
            chkMicAbierto.Checked = config.Audio.MantenerMicrofonoAbierto;

            string tecla = config.General.TeclaPTT;
            int idx = cmbTecla.Items.IndexOf(tecla);
            cmbTecla.SelectedIndex = idx >= 0 ? idx : cmbTecla.Items.IndexOf("F7");

            chkSuprimir.Checked = config.General.SuprimirTeclaGlobal;
            chkBandeja.Checked = config.General.MinimizarABandeja;
            chkSoloConocidos.Checked = config.General.SoloContactosConocidos;
            chkActualizar.Checked = config.General.ActualizacionAutomatica;
            chkInstalarAlCerrar.Checked = config.General.InstalarActualizacionAlCerrar;
            chkInstalarAlCerrar.Enabled = chkActualizar.Checked;
            btnBuscarActualizacion.Enabled = chkActualizar.Checked;
            chkVentanaRespuesta.Checked = config.General.VentanaDeRespuesta;
            chkTeclaUltimo.Checked = config.General.TeclaRespondeAlUltimo;
            chkTeclaUltimo.Enabled = chkVentanaRespuesta.Checked;
            chkDescubrir.Checked = config.General.DescubrimientoAutomatico;
            chkGuardarDescubiertos.Checked = config.General.GuardarContactosDescubiertos;
            chkGuardarDescubiertos.Enabled = chkDescubrir.Checked;
            chkDescubrir.CheckedChanged += (s, e) =>
                chkGuardarDescubiertos.Enabled = chkDescubrir.Checked;

            numPuertoDesc.Value = Math.Clamp(config.General.PuertoDescubrimiento, 1024, 65535);
            numPuerto.Value = Math.Clamp(config.General.Puerto, 1024, 65535);
            numRetencion.Value = Math.Clamp(config.General.DiasRetencionAudios, 0, 3650);
            numMaxTx.Value = Math.Clamp(config.General.MaxSegundosTransmision, 5, 600);
        }

        private void Guardar(object? sender, EventArgs e)
        {
            config.Audio.InputDevice = cmbInput.SelectedIndex - 1;
            config.Audio.OutputDevice = cmbOutput.SelectedIndex - 1;
            config.Audio.SampleRate = cmbCalidad.SelectedIndex switch
            {
                0 => 8000,
                1 => 16000,
                2 => 22050,
                3 => 44100,
                _ => 16000
            };
            config.Audio.Volumen = trkVolumen.Value;
            config.Audio.SonidosDeAviso = chkSonidos.Checked;
            config.Audio.MantenerMicrofonoAbierto = chkMicAbierto.Checked;

            config.General.TeclaPTT = cmbTecla.SelectedItem?.ToString() ?? "F7";
            config.General.SuprimirTeclaGlobal = chkSuprimir.Checked;
            config.General.MinimizarABandeja = chkBandeja.Checked;
            config.General.SoloContactosConocidos = chkSoloConocidos.Checked;
            config.General.ActualizacionAutomatica = chkActualizar.Checked;
            config.General.InstalarActualizacionAlCerrar = chkInstalarAlCerrar.Checked;
            config.General.VentanaDeRespuesta = chkVentanaRespuesta.Checked;
            config.General.TeclaRespondeAlUltimo = chkTeclaUltimo.Checked;
            config.General.DescubrimientoAutomatico = chkDescubrir.Checked;
            config.General.GuardarContactosDescubiertos = chkGuardarDescubiertos.Checked;
            config.General.PuertoDescubrimiento = (int)numPuertoDesc.Value;
            config.General.Puerto = (int)numPuerto.Value;
            config.General.DiasRetencionAudios = (int)numRetencion.Value;
            config.General.MaxSegundosTransmision = (int)numMaxTx.Value;

            RequiereReinicioAudio =
                config.Audio.SampleRate != originalSampleRate ||
                config.Audio.InputDevice != originalInput ||
                config.Audio.OutputDevice != originalOutput ||
                config.Audio.BufferMilliseconds != originalBuffer ||
                config.Audio.MantenerMicrofonoAbierto != originalKeepMic;

            RequiereReinicioRed = config.General.Puerto != originalPort;

            RequiereReinicioDescubrimiento =
                config.General.DescubrimientoAutomatico != originalDescubrir ||
                config.General.PuertoDescubrimiento != originalPuertoDesc;

            if (config.Audio.SampleRate != originalSampleRate)
            {
                MessageBox.Show(this,
                    "Has cambiado la calidad de audio.\n\n" +
                    "Todos los equipos deben usar la MISMA calidad o se escucharán " +
                    "acelerados o distorsionados.",
                    "Calidad de audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Alta y edición de un contacto.</summary>
    public class ContactDialog : Form
    {
        private readonly TextBox txtName = new();
        private readonly TextBox txtIp = new();

        public string ContactName => txtName.Text.Trim();
        public string ContactIp => txtIp.Text.Trim();

        public ContactDialog(string name = "", string ip = "")
        {
            Text = string.IsNullOrEmpty(name) ? "Nuevo contacto" : "Editar contacto";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(340, 180);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.FontBase;

            var lblName = new Label
            {
                Text = "Nombre (debe coincidir en todos los equipos)",
                Location = new Point(20, 18),
                Size = new Size(300, 18),
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            };
            txtName.Location = new Point(20, 38);
            txtName.Size = new Size(300, 24);
            txtName.Text = name;
            Theme.StyleTextBox(txtName);

            var lblIp = new Label
            {
                Text = "Dirección IP",
                Location = new Point(20, 74),
                Size = new Size(300, 18),
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            };
            txtIp.Location = new Point(20, 94);
            txtIp.Size = new Size(300, 24);
            txtIp.Text = ip;
            Theme.StyleTextBox(txtIp);

            var btnOk = new Button { Text = "Aceptar", Location = new Point(128, 132), Size = new Size(90, 30) };
            Theme.StyleButton(btnOk, Theme.Accent, Theme.AccentHover);
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(ContactName))
                {
                    MessageBox.Show(this, "Escribe un nombre.", "Contacto",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!IPAddress.TryParse(ContactIp, out _))
                {
                    MessageBox.Show(this, "La dirección IP no es válida.\nEjemplo: 192.168.0.25",
                        "Contacto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(228, 132),
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel
            };
            Theme.StyleSecondaryButton(btnCancel);

            Controls.AddRange(new Control[] { lblName, txtName, lblIp, txtIp, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
