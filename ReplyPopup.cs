#pragma warning disable CA1416 // API sólo de Windows

using System;
using System.Drawing;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Aviso emergente al recibir un audio, con un botón para contestar a esa
    /// persona directamente.
    ///
    /// Sustituye al comportamiento antiguo, que cambiaba solo el contacto
    /// seleccionado en la lista al recibir audio: era cómodo para responder, pero
    /// si estabas grabando te cambiaba el destinatario a media frase y el mensaje
    /// acababa en otra persona. Aquí se responde sin tocar la selección.
    /// </summary>
    public class ReplyPopup : Form
    {
        private const int Ancho = 340;
        private const int Alto = 148;
        private const int Margen = 12;

        private readonly Label lblNombre = new();
        private readonly Label lblEstado = new();
        private readonly Button btnResponder = new();
        private readonly Button btnPlay = new();
        private readonly Button btnCerrar = new();
        private readonly PictureBox picAvatar = new();
        private readonly System.Windows.Forms.Timer cierreTimer = new();
        private readonly System.Windows.Forms.Timer parpadeoTimer = new();

        private bool parpadeoOn;

        /// <summary>Ya se contestó: el aviso se cierra en cuanto pase el segundo.</summary>
        public bool Respondido { get; private set; }

        /// <summary>Este es el aviso al que contesta la tecla de hablar.</summary>
        public bool EsElActivo { get; private set; }

        public string Contact { get; }
        public AudioItem? Item { get; private set; }

        /// <summary>El usuario mantiene pulsado el botón de responder.</summary>
        public event EventHandler? ReplyPressed;
        public event EventHandler? ReplyReleased;
        public event EventHandler<AudioItem>? PlayRequested;
        public event EventHandler<ReplyPopup>? Closed2;

        public ReplyPopup(string contact, int segundosVisible)
        {
            Contact = contact;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(Ancho, Alto);
            BackColor = Theme.Surface;
            Font = Theme.FontBase;

            ConstruirUi(contact);

            // El aviso NO se cierra solo: permanece hasta que se conteste o se
            // pulse la X, aunque se haga clic en otras ventanas. Este
            // temporizador solo lo retira un segundo después de responder.
            cierreTimer.Interval = 1000;
            cierreTimer.Tick += (s, e) =>
            {
                cierreTimer.Stop();
                CerrarSuave();
            };

            parpadeoTimer.Interval = 500;
            parpadeoTimer.Tick += (s, e) =>
            {
                parpadeoOn = !parpadeoOn;
                lblEstado.ForeColor = parpadeoOn ? Theme.Danger : Theme.TextMuted;
            };
        }

        private void ConstruirUi(string contact)
        {
            // Borde de acento a la izquierda para que se distinga del fondo.
            var barra = new Panel
            {
                Dock = DockStyle.Left,
                Width = 4,
                BackColor = Theme.Incoming
            };

            picAvatar.Location = new Point(16, 14);
            picAvatar.Size = new Size(44, 44);
            picAvatar.SizeMode = PictureBoxSizeMode.Zoom;
            picAvatar.Image = Theme.LoadAvatar(contact, 44);

            lblNombre.Location = new Point(70, 14);
            lblNombre.Size = new Size(Ancho - 110, 24);
            lblNombre.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblNombre.ForeColor = Theme.Text;
            lblNombre.Text = contact;

            lblEstado.Location = new Point(72, 38);
            lblEstado.Size = new Size(Ancho - 110, 20);
            lblEstado.Font = Theme.FontSmall;
            lblEstado.ForeColor = Theme.TextMuted;
            lblEstado.Text = "te está hablando...";

            btnCerrar.Location = new Point(Ancho - 32, 8);
            btnCerrar.Size = new Size(24, 24);
            btnCerrar.Text = "✕";
            btnCerrar.Font = new Font("Segoe UI", 8f);
            Theme.StyleButton(btnCerrar, Theme.Surface);
            btnCerrar.ForeColor = Theme.TextMuted;
            btnCerrar.FlatAppearance.MouseOverBackColor = Theme.SurfaceAlt;
            btnCerrar.Click += (s, e) => CerrarSuave();

            btnResponder.Location = new Point(16, 70);
            btnResponder.Size = new Size(Ancho - 100, 60);
            btnResponder.Text = "MANTENER PARA RESPONDER";
            btnResponder.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            Theme.StyleButton(btnResponder, Theme.Accent, Theme.AccentHover);
            btnResponder.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReplyPressed?.Invoke(this, EventArgs.Empty);
            };
            btnResponder.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReplyReleased?.Invoke(this, EventArgs.Empty);
            };

            btnPlay.Location = new Point(Ancho - 76, 70);
            btnPlay.Size = new Size(60, 60);
            btnPlay.Text = "▶";
            btnPlay.Font = new Font("Segoe UI Symbol", 14f);
            btnPlay.Enabled = false;
            Theme.StyleSecondaryButton(btnPlay);
            btnPlay.Click += (s, e) =>
            {
                if (Item != null) PlayRequested?.Invoke(this, Item);
            };

            Controls.AddRange(new Control[]
            {
                barra, picAvatar, lblNombre, lblEstado, btnCerrar, btnResponder, btnPlay
            });
        }

        /// <summary>Llega audio nuevo del mismo contacto: se reinicia el aviso.</summary>
        public void MarcarHablando()
        {
            Item = null;
            btnPlay.Enabled = false;
            lblEstado.Text = "te está hablando...";
            parpadeoTimer.Start();
        }

        public void MarcarRecibido(AudioItem? item)
        {
            Item = item;
            parpadeoTimer.Stop();
            lblEstado.ForeColor = Theme.TextMuted;

            if (item != null)
            {
                btnPlay.Enabled = true;
                string duracion = item.DurationText;
                lblEstado.Text = string.IsNullOrEmpty(duracion)
                    ? $"te envió un audio · {item.TimeText}"
                    : $"te envió un audio de {duracion} · {item.TimeText}";
            }
            else
            {
                lblEstado.Text = "te ha hablado";
            }
        }

        public void MarcarRespondiendo(bool activo)
        {
            if (activo)
            {
                btnResponder.Text = $"SUELTA PARA ENVIAR A {Contact.ToUpperInvariant()}";
                btnResponder.BackColor = Theme.Danger;
                btnResponder.FlatAppearance.MouseOverBackColor = Theme.DangerHover;
                cierreTimer.Stop();
            }
            else
            {
                btnResponder.Text = "MANTENER PARA RESPONDER";
                btnResponder.BackColor = Theme.Accent;
                btnResponder.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            }
        }

        /// <summary>
        /// Se ha contestado: el aviso se retira solo un segundo después, para
        /// que dé tiempo a ver que el mensaje salió.
        /// </summary>
        public void MarcarRespondido()
        {
            if (Respondido) return;
            Respondido = true;

            parpadeoTimer.Stop();
            btnResponder.Enabled = false;
            btnResponder.Text = "RESPUESTA ENVIADA";
            btnResponder.BackColor = Theme.Online;
            lblEstado.ForeColor = Theme.Online;
            lblEstado.Text = "respondido";

            cierreTimer.Start();   // se cierra en 1 segundo
        }

        /// <summary>
        /// Marca si es el aviso al que responde la tecla de hablar. Los demás
        /// quedan en espera hasta que este se conteste.
        /// </summary>
        public void MarcarActivo(bool activo, string tecla)
        {
            EsElActivo = activo;

            if (Respondido) return;

            if (activo)
            {
                BackColor = Theme.Surface;
                btnResponder.Enabled = true;
                btnResponder.BackColor = Theme.Accent;
                btnResponder.Text = $"MANTENER PARA RESPONDER  ·  {tecla}";
            }
            else
            {
                // En espera: se ve que existe, pero no responde a la tecla.
                BackColor = Theme.Background;
                btnResponder.Enabled = false;
                btnResponder.BackColor = Theme.SurfaceAlt;
                btnResponder.Text = "EN ESPERA";
            }
            Invalidate();
        }

        private void CerrarSuave()
        {
            cierreTimer.Stop();
            parpadeoTimer.Stop();
            Closed2?.Invoke(this, this);
            Close();
        }

        public void Colocar(int indice)
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
            int x = area.Right - Ancho - Margen;
            int y = area.Bottom - (Alto + Margen) * (indice + 1);
            Location = new Point(x, Math.Max(area.Top + Margen, y));
        }

        // No robar el foco al aparecer: si estás escribiendo en otro programa, un
        // aviso que se lleva el teclado es intolerable.
        protected override bool ShowWithoutActivation => true;

        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var borde = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(borde, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                cierreTimer.Dispose();
                parpadeoTimer.Dispose();
                picAvatar.Image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
