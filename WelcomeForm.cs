using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    public partial class WelcomeForm : Form
    {
        public WelcomeForm(string user)
        {
            // No llamar a InitializeComponent, todo es por código
            this.Text = "Bienvenido";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.Size = new Size(370, 180);
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Panel para centrar contenido
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                AutoScroll = false // Asegurarse de que el panel use el posicionamiento manual
            };
            this.Controls.Add(panel);

            // PictureBox para la imagen del usuario
            var pic = new PictureBox
            {
                Size = new Size(72, 72),
                Location = new Point(30, 35),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.LightGray // Color para diferenciar si no hay imagen
            };

            string imgPath = $"resources\\{user.ToLower()}.png";
            if (File.Exists(imgPath))
                pic.Image = Image.FromFile(imgPath);
            else
                pic.Image = null;

            // Label para el mensaje con tamaño ajustado
            var lbl = new Label
            {
                Text = $"¡Hola de nuevo, {user}!",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 45, 48),
                AutoSize = false,
                Size = new Size(210, 60),
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(120, 30) // Ubicación ajustada para mayor visibilidad
            };

            // Botón aceptar, centrado horizontalmente
            var btn = new Button
            {
                Text = "Aceptar",
                DialogResult = DialogResult.OK,
                Size = new Size(100, 32),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point((this.ClientSize.Width - 100) / 2, 100)
            };

            // Agregar controles al panel
            panel.Controls.Add(pic);
            panel.Controls.Add(lbl);
            panel.Controls.Add(btn);
            this.AcceptButton = btn;
        }
    }
}