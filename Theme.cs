using System.Drawing;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Paleta única para toda la app. Antes MainForm era oscuro y
    /// LoginForm/WelcomeForm eran blancos.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Background = Color.FromArgb(32, 33, 36);
        public static readonly Color Surface = Color.FromArgb(45, 46, 50);
        public static readonly Color SurfaceAlt = Color.FromArgb(55, 56, 61);
        public static readonly Color Border = Color.FromArgb(70, 71, 77);

        public static readonly Color Text = Color.FromArgb(240, 240, 242);
        public static readonly Color TextMuted = Color.FromArgb(150, 152, 158);

        public static readonly Color Accent = Color.FromArgb(0, 122, 204);
        public static readonly Color AccentHover = Color.FromArgb(25, 145, 225);
        public static readonly Color Danger = Color.FromArgb(220, 40, 60);
        public static readonly Color DangerHover = Color.FromArgb(240, 60, 80);
        public static readonly Color Online = Color.FromArgb(60, 200, 110);
        public static readonly Color Offline = Color.FromArgb(110, 112, 118);
        public static readonly Color Incoming = Color.FromArgb(90, 200, 250);
        public static readonly Color Outgoing = Color.FromArgb(160, 200, 130);

        public static readonly Font FontBase = new("Segoe UI", 9.75f);
        public static readonly Font FontSmall = new("Segoe UI", 8.25f);
        public static readonly Font FontSemibold = new("Segoe UI Semibold", 9.75f);
        public static readonly Font FontTitle = new("Segoe UI", 15f, FontStyle.Bold);
        public static readonly Font FontButton = new("Segoe UI", 10f, FontStyle.Bold);

        /// <summary>
        /// Aplica el estilo plano de verdad. Antes se configuraba FlatAppearance.BorderColor
        /// con FlatStyle.Standard, donde no tiene ningún efecto: los botones se veían con
        /// borde 3D clásico sobre el fondo oscuro.
        /// </summary>
        public static void StyleButton(Button btn, Color back, Color? hover = null)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hover ?? Lighten(back, 0.12f);
            btn.FlatAppearance.MouseDownBackColor = Lighten(back, -0.10f);
            btn.BackColor = back;
            btn.ForeColor = Text;
            btn.UseVisualStyleBackColor = false;
            btn.Cursor = Cursors.Hand;
        }

        public static void StyleSecondaryButton(Button btn)
        {
            StyleButton(btn, SurfaceAlt);
            btn.ForeColor = Text;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Border;
        }

        public static void StyleTextBox(TextBox box)
        {
            box.BackColor = SurfaceAlt;
            box.ForeColor = Text;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = FontBase;
        }

        public static Color Lighten(Color c, float amount)
        {
            int Adjust(int v) => System.Math.Clamp((int)(v + 255 * amount), 0, 255);
            return Color.FromArgb(c.A, Adjust(c.R), Adjust(c.G), Adjust(c.B));
        }

        /// <summary>Avatar de reserva cuando no existe resources\{usuario}.png.</summary>
        public static Image CreateInitialsAvatar(string name, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Color estable derivado del nombre.
                int hash = 0;
                foreach (char ch in name.ToLowerInvariant()) hash = hash * 31 + ch;
                var hue = System.Math.Abs(hash) % 360;
                using var brush = new SolidBrush(FromHsv(hue, 0.45f, 0.55f));
                g.FillEllipse(brush, 0, 0, size - 1, size - 1);

                string initials = string.IsNullOrWhiteSpace(name)
                    ? "?"
                    : name.Trim().Substring(0, 1).ToUpperInvariant();

                using var font = new Font("Segoe UI", size * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(initials, font, Brushes.White, new RectangleF(0, 0, size, size), sf);
            }
            return bmp;
        }

        private static Color FromHsv(double hue, double sat, double val)
        {
            int hi = (int)(hue / 60) % 6;
            double f = hue / 60 - System.Math.Floor(hue / 60);
            int v = (int)(val * 255);
            int p = (int)(val * (1 - sat) * 255);
            int q = (int)(val * (1 - f * sat) * 255);
            int t = (int)(val * (1 - (1 - f) * sat) * 255);

            return hi switch
            {
                0 => Color.FromArgb(v, t, p),
                1 => Color.FromArgb(q, v, p),
                2 => Color.FromArgb(p, v, t),
                3 => Color.FromArgb(p, q, v),
                4 => Color.FromArgb(t, p, v),
                _ => Color.FromArgb(v, p, q)
            };
        }

        /// <summary>Carga el avatar de un usuario sin dejar bloqueado el archivo.</summary>
        public static Image LoadAvatar(string userName, int size)
        {
            try
            {
                // Antes había un switch con los nombres a fuego; "Facturación" nunca
                // coincidía por la tilde y añadir gente exigía recompilar.
                string path = System.IO.Path.Combine(AppPaths.ResourcesDir, $"{userName.ToLowerInvariant()}.png");
                if (!System.IO.File.Exists(path))
                {
                    string sinTildes = RemoveDiacritics(userName.ToLowerInvariant());
                    path = System.IO.Path.Combine(AppPaths.ResourcesDir, $"{sinTildes}.png");
                }

                if (System.IO.File.Exists(path))
                {
                    // Image.FromFile deja el archivo bloqueado mientras viva la imagen.
                    using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open,
                        System.IO.FileAccess.Read, System.IO.FileShare.Read);
                    using var original = Image.FromStream(fs);
                    return new Bitmap(original, size, size);
                }
            }
            catch
            {
                // Si la imagen está corrupta, seguimos con las iniciales.
            }

            return CreateInitialsAvatar(userName, size);
        }

        public static string RemoveDiacritics(string text)
        {
            string normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (char c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
