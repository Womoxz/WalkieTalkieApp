#pragma warning disable CA1416 // API sólo de Windows

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Parte de MainForm encargada de las actualizaciones automáticas: comprueba
    /// si hay versión nueva, la descarga en segundo plano y avisa con una barra
    /// en la parte superior. La instalación ocurre cuando el usuario acepta o al
    /// cerrar la aplicación, nunca a mitad de una conversación.
    /// </summary>
    public partial class MainForm
    {
        private Panel? barraUpdate;
        private Label? lblUpdate;
        private Button? btnUpdateAhora;
        private Button? btnUpdateLuego;

        private void IniciarActualizaciones()
        {
            if (!config.General.ActualizacionAutomatica) return;

            updates = new UpdateService(config);
            UpdateService.LimpiarDescargasViejas();

            // La primera comprobación se retrasa para no competir con el arranque
            // (micrófono, red y descubrimiento se están iniciando a la vez).
            updateTimer = new System.Windows.Forms.Timer { Interval = 20_000 };
            updateTimer.Tick += async (s, e) =>
            {
                // Tras la primera vez, se espacia según la configuración.
                int horas = Math.Max(1, config.General.HorasEntreComprobaciones);
                updateTimer.Interval = (int)Math.Min(int.MaxValue, horas * 3600_000L);

                await ComprobarActualizacionAsync();
            };
            updateTimer.Start();
        }

        /// <summary>Comprueba y, si hay novedad, la descarga y avisa.</summary>
        private async Task ComprobarActualizacionAsync(bool avisarSiNoHay = false)
        {
            if (updates == null) return;

            var info = await updates.BuscarAsync();

            if (info == null)
            {
                if (avisarSiNoHay)
                {
                    MessageBox.Show(this,
                        $"Ya tienes la versión más reciente ({UpdateService.VersionActual}).",
                        "Actualizaciones", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            if (actualizacionAvisada && updates.InstaladorListo != null) return;
            actualizacionAvisada = true;

            MostrarBarraActualizacion($"Versión {info.Version} disponible · descargando...", false);

            var avance = new Progress<int>(p =>
            {
                if (lblUpdate != null && p < 100)
                    lblUpdate.Text = $"Descargando la versión {info.Version}... {p}%";
            });

            string? archivo = await updates.DescargarAsync(info, avance);

            if (archivo == null)
            {
                MostrarBarraActualizacion(
                    $"Hay una versión nueva ({info.Version}) pero no se pudo descargar.", false);
                return;
            }

            string tamano = string.IsNullOrEmpty(info.TamanoTexto) ? "" : $" · {info.TamanoTexto}";
            MostrarBarraActualizacion(
                $"Versión {info.Version} lista para instalar{tamano}", true);

            // Si la ventana está oculta en la bandeja, se avisa por ahí.
            if (!Visible || WindowState == FormWindowState.Minimized)
            {
                trayIcon.ShowBalloonTip(4000, "Walkie Talkie",
                    $"La versión {info.Version} está lista. Se instalará al cerrar la aplicación.",
                    ToolTipIcon.Info);
            }
        }

        /// <summary>Barra de aviso en la parte superior, debajo de la cabecera.</summary>
        private void MostrarBarraActualizacion(string texto, bool listaParaInstalar)
        {
            if (barraUpdate == null)
            {
                barraUpdate = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 44,
                    BackColor = Theme.Accent,
                    Padding = new Padding(14, 0, 10, 0)
                };

                lblUpdate = new Label
                {
                    Dock = DockStyle.Fill,
                    ForeColor = Color.White,
                    Font = Theme.FontSemibold,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                btnUpdateLuego = new Button
                {
                    Text = "Más tarde",
                    Dock = DockStyle.Right,
                    Width = 96,
                    Margin = new Padding(6)
                };
                Theme.StyleButton(btnUpdateLuego, Theme.AccentHover);
                btnUpdateLuego.Click += (s, e) => OcultarBarraActualizacion();

                btnUpdateAhora = new Button
                {
                    Text = "Actualizar ahora",
                    Dock = DockStyle.Right,
                    Width = 140,
                    Visible = false
                };
                Theme.StyleButton(btnUpdateAhora, Color.White);
                btnUpdateAhora.ForeColor = Theme.Accent;
                btnUpdateAhora.Click += (s, e) => InstalarActualizacionAhora();

                // El orden importa: los Dock=Right se apilan de derecha a izquierda.
                barraUpdate.Controls.Add(lblUpdate);
                barraUpdate.Controls.Add(btnUpdateLuego);
                barraUpdate.Controls.Add(btnUpdateAhora);

                Controls.Add(barraUpdate);
                barraUpdate.BringToFront();
                topPanel.BringToFront();
            }

            lblUpdate!.Text = texto;
            btnUpdateAhora!.Visible = listaParaInstalar;
            barraUpdate.Visible = true;
        }

        private void OcultarBarraActualizacion()
        {
            if (barraUpdate != null) barraUpdate.Visible = false;

            if (updates?.InstaladorListo != null && config.General.InstalarActualizacionAlCerrar)
            {
                lblStatus.Text = "La actualización se instalará al cerrar la aplicación.";
            }
        }

        private void InstalarActualizacionAhora()
        {
            if (updates?.InstaladorListo == null) return;

            if (engine != null && engine.IsTransmitting)
            {
                MessageBox.Show(this, "Termina de hablar antes de actualizar.",
                    "Actualizaciones", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var respuesta = MessageBox.Show(this,
                "La aplicación se cerrará para instalar la versión nueva y volverá a abrirse.\n\n" +
                "Tus contactos, ajustes e historial se conservan.",
                "Actualizar", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

            if (respuesta != DialogResult.OK) return;

            if (updates.Instalar())
            {
                // Salida real: el instalador espera a que se suelte el mutex.
                exitRequested = true;
                Close();
            }
            else
            {
                MessageBox.Show(this, "No se pudo iniciar el instalador.",
                    "Actualizaciones", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Comprobación manual desde la ventana de configuración.</summary>
        public async void ComprobarActualizacionManual()
        {
            if (updates == null)
            {
                updates = new UpdateService(config);
            }

            lblStatus.Text = "Buscando actualizaciones...";
            actualizacionAvisada = false;
            await ComprobarActualizacionAsync(avisarSiNoHay: true);
            UpdateStatus();
        }
    }
}
