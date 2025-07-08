using System;
using System.IO;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string userFile = "user.txt";

            // Verificar si ya hay un usuario guardado
            if (File.Exists(userFile))
            {
                string savedUser = File.ReadAllText(userFile).Trim();
                if (!string.IsNullOrWhiteSpace(savedUser))
                {
                    // Mostrar pantalla de bienvenida
                    using (var welcome = new WelcomeForm(savedUser))
                    {
                        if (welcome.ShowDialog() == DialogResult.OK)
                        {
                            Application.Run(new MainForm(savedUser));
                            return;
                        }
                    }
                }
            }

            // Si no hay usuario guardado o se canceló, mostrar el LoginForm
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(userFile, login.UserName);
                    Application.Run(new MainForm(login.UserName));
                    return;
                }
            }

            // Si ninguna condición se cumplió, mostrar aviso y salir
            MessageBox.Show("No se inició sesión. La aplicación se cerrará.", "Salir", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
