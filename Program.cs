using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    static class Program
    {
        private static Mutex? instanceMutex;

        /// <summary>Mensaje para traer al frente la instancia que ya está abierta.</summary>
        public static readonly int ShowMeMessage =
            RegisterWindowMessage("WalkieTalkieApp.ShowMe.7B2F");

        [DllImport("user32.dll")]
        private static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static readonly IntPtr HWND_BROADCAST = (IntPtr)0xFFFF;

        [STAThread]
        static void Main()
        {
            // Dos copias abiertas peleaban por el puerto UDP y por el hook de teclado.
            instanceMutex = new Mutex(true, @"Global\WalkieTalkieApp.SingleInstance", out bool isFirst);
            if (!isFirst)
            {
                PostMessage(HWND_BROADCAST, ShowMeMessage, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            // Sin esto la ventana se ve borrosa en pantallas escaladas al 125% o más.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) => HandleCrash(e.Exception, "UI");
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                HandleCrash(e.ExceptionObject as Exception, "AppDomain");

            AppConfig.Load();

            string? user = ResolveUser();
            if (string.IsNullOrWhiteSpace(user)) return;

            try
            {
                Application.Run(new MainForm(user));
            }
            finally
            {
                instanceMutex.ReleaseMutex();
                instanceMutex.Dispose();
            }
        }

        /// <summary>Devuelve el usuario de la sesión, pidiéndolo si hace falta.</summary>
        private static string? ResolveUser()
        {
            try
            {
                if (File.Exists(AppPaths.UserFile))
                {
                    string saved = File.ReadAllText(AppPaths.UserFile).Trim();
                    if (!string.IsNullOrWhiteSpace(saved))
                    {
                        using var welcome = new WelcomeForm(saved);
                        if (welcome.ShowDialog() == DialogResult.OK)
                            return saved;

                        // "No soy yo": se pide de nuevo en vez de cerrar la app.
                    }
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.CrashLog, $"Lectura de user.txt: {ex.Message}");
            }

            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK) return null;

            try
            {
                File.WriteAllText(AppPaths.UserFile, login.UserName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo recordar el usuario para la próxima vez:\n{ex.Message}",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return login.UserName;
        }

        private static void HandleCrash(Exception? ex, string source)
        {
            if (ex == null) return;

            AppPaths.Log(AppPaths.CrashLog, $"{source}: {ex}");

            try
            {
                MessageBox.Show(
                    $"Se produjo un error inesperado:\n\n{ex.Message}\n\n" +
                    $"Se ha guardado el detalle en:\n{AppPaths.CrashLog}",
                    "Walkie Talkie", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Si ni siquiera se puede mostrar el diálogo, ya está registrado en el log.
            }
        }
    }
}
