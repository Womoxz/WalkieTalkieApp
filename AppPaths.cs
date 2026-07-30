using System;
using System.IO;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Todas las rutas de la aplicación. Antes se usaba el directorio de trabajo
    /// (Directory.GetCurrentDirectory), lo que hacía fallar la app al abrirla desde
    /// un acceso directo con "Iniciar en" distinto.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>Carpeta donde está el .exe (nunca cambia).</summary>
        public static string BaseDir => AppContext.BaseDirectory;

        public static string ConfigFile => Path.Combine(BaseDir, "appsettings.json");
        public static string UserFile => Path.Combine(BaseDir, "user.txt");
        public static string SoundsDir => Path.Combine(BaseDir, "sounds");
        public static string ResourcesDir => Path.Combine(BaseDir, "resources");
        public static string AudioDir => Path.Combine(BaseDir, "audios");

        /// <summary>Los logs van a %APPDATA% para no ensuciar Archivos de programa.</summary>
        public static string LogDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WalkieTalkie", "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string CrashLog => Path.Combine(LogDir, "crash.log");
        public static string NetworkLog => Path.Combine(LogDir, "network.log");

        public static string InboxDir(string userName) => Path.Combine(AudioDir, userName);
        public static string SentDir => Path.Combine(AudioDir, "Enviados");

        public static string Sound(string fileName) => Path.Combine(SoundsDir, fileName);

        public static void EnsureAudioDirs(string userName)
        {
            Directory.CreateDirectory(AudioDir);
            Directory.CreateDirectory(InboxDir(userName));
            Directory.CreateDirectory(SentDir);
        }

        public static void Log(string file, string message)
        {
            try
            {
                File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Nunca dejar que el logging tumbe la app.
            }
        }
    }
}
