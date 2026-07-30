using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Elección de usuario. Antes era un cuadro de texto libre: si alguien escribía
    /// mal su nombre, nadie podía enviarle audio y no había ninguna pista de por qué.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly ComboBox cmbUsuario = new();
        private readonly TextBox txtOtro = new();
        private readonly Label lblIp = new();
        private readonly Label lblAviso = new();

        private const string OtroTexto = "Otro nombre...";

        public string UserName { get; private set; } = string.Empty;
        public string LocalIp { get; }

        public LoginForm()
        {
            LocalIp = GetLocalIPAddress();

            Text = "Configuración inicial";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.FontBase;
            ClientSize = new Size(400, 300);
            MaximizeBox = false;
            MinimizeBox = false;

            var lblTitulo = new Label
            {
                Text = "¿Quién eres?",
                Font = Theme.FontTitle,
                ForeColor = Theme.Text,
                Location = new Point(24, 22),
                Size = new Size(340, 32)
            };

            var lblAyuda = new Label
            {
                Text = "Así te verán tus compañeros. Los demás equipos te encontrarán solos.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                Location = new Point(26, 54),
                Size = new Size(350, 18)
            };

            cmbUsuario.Location = new Point(24, 84);
            cmbUsuario.Size = new Size(352, 26);
            cmbUsuario.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbUsuario.BackColor = Theme.SurfaceAlt;
            cmbUsuario.ForeColor = Theme.Text;
            cmbUsuario.FlatStyle = FlatStyle.Flat;
            cmbUsuario.SelectedIndexChanged += (s, e) => ActualizarEstado();

            txtOtro.Location = new Point(24, 116);
            txtOtro.Size = new Size(352, 26);
            txtOtro.Visible = false;
            txtOtro.PlaceholderText = "Escribe tu nombre";
            Theme.StyleTextBox(txtOtro);
            txtOtro.TextChanged += (s, e) => ActualizarEstado();

            var lblIpTitulo = new Label
            {
                Text = "Tu dirección en esta red",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                Location = new Point(26, 156),
                Size = new Size(350, 18)
            };

            lblIp.Text = LocalIp;
            lblIp.Font = new Font("Consolas", 11f);
            lblIp.ForeColor = Theme.Text;
            lblIp.Location = new Point(24, 176);
            lblIp.Size = new Size(352, 22);

            lblAviso.Location = new Point(24, 202);
            lblAviso.Size = new Size(352, 34);
            lblAviso.Font = Theme.FontSmall;
            lblAviso.ForeColor = Theme.TextMuted;

            var btnStart = new Button
            {
                Text = "Iniciar",
                Location = new Point(236, 246),
                Size = new Size(140, 38)
            };
            Theme.StyleButton(btnStart, Theme.Accent, Theme.AccentHover);
            btnStart.Font = Theme.FontButton;
            btnStart.Click += BtnStart_Click;

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(24, 246),
                Size = new Size(110, 38),
                DialogResult = DialogResult.Cancel
            };
            Theme.StyleSecondaryButton(btnCancel);

            Controls.AddRange(new Control[]
            {
                lblTitulo, lblAyuda, cmbUsuario, txtOtro,
                lblIpTitulo, lblIp, lblAviso, btnStart, btnCancel
            });

            AcceptButton = btnStart;
            CancelButton = btnCancel;

            CargarUsuarios();

            try
            {
                string ico = System.IO.Path.Combine(AppPaths.ResourcesDir, "VW_Talk_Logo.ico");
                if (System.IO.File.Exists(ico)) Icon = new Icon(ico);
            }
            catch { }
        }

        private void CargarUsuarios()
        {
            var config = AppConfig.Current;

            foreach (var c in config.Contactos.Keys.OrderBy(k => k))
                cmbUsuario.Items.Add(c);

            cmbUsuario.Items.Add(OtroTexto);

            // Preseleccionar el contacto cuya IP coincide con la de este equipo.
            string? porIp = config.BuscarNombrePorIp(LocalIp);
            if (porIp != null)
                cmbUsuario.SelectedItem = porIp;
            else
                cmbUsuario.SelectedIndex = 0;

            // Sin lista previa (lo normal con el descubrimiento automático) se pasa
            // directamente a escribir el nombre en vez de mostrar un desplegable vacío.
            if (config.Contactos.Count == 0)
            {
                cmbUsuario.SelectedItem = OtroTexto;
                cmbUsuario.Visible = false;
                txtOtro.Location = new Point(24, 84);
            }

            ActualizarEstado();
        }

        private string NombreElegido =>
            (cmbUsuario.SelectedItem?.ToString() == OtroTexto)
                ? txtOtro.Text.Trim()
                : cmbUsuario.SelectedItem?.ToString() ?? string.Empty;

        private void ActualizarEstado()
        {
            bool otro = cmbUsuario.SelectedItem?.ToString() == OtroTexto;
            txtOtro.Visible = otro;

            string nombre = NombreElegido;
            var config = AppConfig.Current;

            if (string.IsNullOrWhiteSpace(nombre))
            {
                lblAviso.Text = string.Empty;
                return;
            }

            if (config.Contactos.TryGetValue(nombre, out string? ipConfigurada))
            {
                if (ipConfigurada != LocalIp)
                {
                    lblAviso.ForeColor = Color.FromArgb(230, 190, 60);
                    lblAviso.Text = $"En la lista, «{nombre}» figura con la IP {ipConfigurada}.\n" +
                                    "Al iniciar se corregirá con la IP de este equipo.";
                }
                else
                {
                    lblAviso.ForeColor = Theme.Online;
                    lblAviso.Text = "Todo correcto: la IP coincide con la configurada.";
                }
            }
            else
            {
                lblAviso.ForeColor = Theme.TextMuted;
                lblAviso.Text = "Los demás equipos te detectarán automáticamente\n" +
                                "en cuanto entres.";
            }
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            string nombre = NombreElegido;

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show(this, "Elige o escribe un nombre de usuario.", "Usuario",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var config = AppConfig.Current;

            // Registrar/corregir la propia IP: así el resto de equipos que compartan
            // el archivo no se quedan apuntando a una IP vieja del DHCP.
            bool cambio = !config.Contactos.TryGetValue(nombre, out string? actual) || actual != LocalIp;
            if (cambio && LocalIp != "127.0.0.1")
            {
                config.Contactos[nombre] = LocalIp;
                config.Save();
            }

            UserName = nombre;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                // Truco fiable: se abre un socket UDP "hacia" una IP externa (no envía
                // nada) y se mira qué interfaz elige el sistema. Evita quedarse con
                // adaptadores virtuales de VPN, Hyper-V o VirtualBox.
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                    return endPoint.Address.ToString();
            }
            catch
            {
                // Sin salida a internet se prueba con las interfaces activas.
            }

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            return ip.Address.ToString();
                    }
                }
            }
            catch
            {
                // Se devuelve loopback como último recurso.
            }

            return "127.0.0.1";
        }
    }
}
