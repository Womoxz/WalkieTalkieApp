using System;
using System.Drawing;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Pantalla de bienvenida. El archivo anterior estaba guardado en ANSI y el
    /// saludo se veía literalmente como "�Hola de nuevo, Jose!".
    /// </summary>
    public class WelcomeForm : Form
    {
        public WelcomeForm(string user)
        {
            Text = "Bienvenido";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.FontBase;
            ClientSize = new Size(400, 210);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;

            var pic = new PictureBox
            {
                Size = new Size(72, 72),
                Location = new Point(28, 34),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = Theme.LoadAvatar(user, 72)
            };

            // AutoSize + ventana elástica: con el ancho fijo anterior, un nombre
            // largo se cortaba y el saludo quedaba en "¡Hola de nuevo,".
            var lblHola = new Label
            {
                Text = $"¡Hola de nuevo, {user}!",
                Font = Theme.FontTitle,
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(116, 44),
                MaximumSize = new Size(520, 0)
            };

            var lblSub = new Label
            {
                Text = "Pulsa Continuar para conectarte.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(118, 74),
                Size = new Size(260, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnOk = new Button
            {
                Text = "Continuar",
                DialogResult = DialogResult.OK,
                Size = new Size(140, 36),
                Location = new Point(212, 140)
            };
            Theme.StyleButton(btnOk, Theme.Accent, Theme.AccentHover);
            btnOk.Font = Theme.FontButton;

            // Antes no había forma de cambiar de usuario sin borrar user.txt a mano.
            var btnOther = new Button
            {
                Text = "No soy yo",
                DialogResult = DialogResult.Cancel,
                Size = new Size(140, 36),
                Location = new Point(56, 140)
            };
            Theme.StyleSecondaryButton(btnOther);

            Controls.AddRange(new Control[] { pic, lblHola, lblSub, btnOk, btnOther });

            // Ensanchar la ventana si el saludo lo pide y recolocar los botones.
            int anchoNecesario = lblHola.Right + 28;
            if (anchoNecesario > ClientSize.Width)
                ClientSize = new Size(anchoNecesario, ClientSize.Height);

            btnOk.Location = new Point(ClientSize.Width - btnOk.Width - 28, 140);
            btnOther.Location = new Point(btnOk.Left - btnOther.Width - 16, 140);

            AcceptButton = btnOk;
            CancelButton = btnOther;

            try
            {
                string ico = System.IO.Path.Combine(AppPaths.ResourcesDir, "VW_Talk_Logo.ico");
                if (System.IO.File.Exists(ico)) Icon = new Icon(ico);
            }
            catch { }
        }
    }
}
