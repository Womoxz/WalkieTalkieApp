#pragma warning disable CA1416 // API sólo de Windows

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WalkieTalkieApp
{
    /// <summary>Datos de una versión publicada más nueva que la instalada.</summary>
    public class UpdateInfo
    {
        public Version Version { get; init; } = new(0, 0, 0);
        public string Nombre { get; init; } = string.Empty;
        public string Notas { get; init; } = string.Empty;
        public string UrlDescarga { get; init; } = string.Empty;
        public long Tamano { get; init; }

        public string TamanoTexto => Tamano > 0 ? $"{Tamano / 1024d / 1024d:0.#} MB" : "";
    }

    /// <summary>
    /// Actualizaciones automáticas.
    ///
    /// Consulta las versiones publicadas, descarga el instalador en segundo plano
    /// y lo ejecuta en silencio. El instalador ya sabe cerrar la aplicación en
    /// marcha (usa el mismo mutex de instancia única) y conserva appsettings.json,
    /// así que al actualizar no se pierden contactos ni ajustes.
    /// </summary>
    public class UpdateService
    {
        private readonly AppConfig config;
        private static readonly HttpClient http = CrearCliente();

        /// <summary>Ruta del instalador ya descargado y listo para ejecutarse.</summary>
        public string? InstaladorListo { get; private set; }
        public UpdateInfo? Disponible { get; private set; }

        public UpdateService(AppConfig config)
        {
            this.config = config;
        }

        private static HttpClient CrearCliente()
        {
            var cliente = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            // GitHub rechaza las peticiones que no se identifican.
            cliente.DefaultRequestHeaders.Add("User-Agent", "WalkieTalkieApp-Updater");
            cliente.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            return cliente;
        }

        public static Version VersionActual
        {
            get
            {
                var v = typeof(UpdateService).Assembly.GetName().Version;
                return v is null ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, v.Build);
            }
        }

        /// <summary>
        /// Busca una versión más nueva. Devuelve null si ya está al día o si no se
        /// pudo consultar (sin internet, por ejemplo): nunca lanza.
        /// </summary>
        public async Task<UpdateInfo?> BuscarAsync(CancellationToken ct = default)
        {
            if (!config.General.ActualizacionAutomatica) return null;

            try
            {
                string url = config.General.UrlActualizaciones;
                if (string.IsNullOrWhiteSpace(url)) return null;

                string json = await http.GetStringAsync(url, ct);

                var nueva = LeerVersiones(json)
                    .Where(u => u.Version > VersionActual)
                    .OrderByDescending(u => u.Version)
                    .FirstOrDefault();

                Disponible = nueva;
                return nueva;
            }
            catch (Exception ex)
            {
                // Sin conexión o servicio caído: no debe molestar al usuario.
                AppPaths.Log(AppPaths.NetworkLog, $"Comprobación de actualizaciones: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Interpreta la respuesta de la API de GitHub. Admite tanto la lista de
        /// versiones como una sola. Se ignoran las publicaciones antiguas cuya
        /// etiqueta no es un número de versión y las que no traen instalador.
        /// </summary>
        internal static List<UpdateInfo> LeerVersiones(string json)
        {
            var lista = new List<UpdateInfo>();

            using var doc = JsonDocument.Parse(json);
            var elementos = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { doc.RootElement };

            foreach (var r in elementos)
            {
                if (r.ValueKind != JsonValueKind.Object) continue;
                if (r.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) continue;
                if (r.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True) continue;

                string tag = r.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
                var version = LeerVersion(tag);
                if (version is null) continue;

                // De los archivos adjuntos interesa el instalador de Windows.
                string urlExe = "";
                long tamano = 0;

                if (r.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        string nombre = a.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                        if (!nombre.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                        urlExe = a.TryGetProperty("browser_download_url", out var u) ? (u.GetString() ?? "") : "";
                        tamano = a.TryGetProperty("size", out var s) && s.TryGetInt64(out long v) ? v : 0;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(urlExe)) continue;   // sin instalador no sirve

                lista.Add(new UpdateInfo
                {
                    Version = version,
                    Nombre = r.TryGetProperty("name", out var nm) ? (nm.GetString() ?? tag) : tag,
                    Notas = r.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "",
                    UrlDescarga = urlExe,
                    Tamano = tamano
                });
            }

            return lista;
        }

        /// <summary>Convierte "v2.3.0" o "2.3" en un número de versión.</summary>
        internal static Version? LeerVersion(string etiqueta)
        {
            if (string.IsNullOrWhiteSpace(etiqueta)) return null;

            string limpia = etiqueta.Trim().TrimStart('v', 'V');
            var numeros = new List<int>();

            foreach (var parte in limpia.Split('.', '-', '+'))
            {
                if (!int.TryParse(parte, out int n)) break;
                numeros.Add(n);
                if (numeros.Count == 3) break;
            }

            if (numeros.Count == 0) return null;
            while (numeros.Count < 3) numeros.Add(0);

            return new Version(numeros[0], numeros[1], numeros[2]);
        }

        /// <summary>Descarga el instalador a la carpeta temporal. Devuelve la ruta o null.</summary>
        public async Task<string?> DescargarAsync(UpdateInfo info, IProgress<int>? avance = null,
                                                  CancellationToken ct = default)
        {
            try
            {
                string carpeta = Path.Combine(Path.GetTempPath(), "WalkieTalkieUpdate");
                Directory.CreateDirectory(carpeta);

                string destino = Path.Combine(carpeta, $"WalkieTalkie_{info.Version}_Setup.exe");

                // Si ya se descargó antes y el tamaño cuadra, no se vuelve a bajar.
                if (File.Exists(destino) && info.Tamano > 0 &&
                    new FileInfo(destino).Length == info.Tamano)
                {
                    InstaladorListo = destino;
                    avance?.Report(100);
                    return destino;
                }

                string parcial = destino + ".part";

                using (var respuesta = await http.GetAsync(info.UrlDescarga,
                           HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    respuesta.EnsureSuccessStatusCode();

                    long total = respuesta.Content.Headers.ContentLength ?? info.Tamano;
                    using var origen = await respuesta.Content.ReadAsStreamAsync(ct);
                    using var salida = File.Create(parcial);

                    var bufer = new byte[81920];
                    long leidos = 0;
                    int n;

                    while ((n = await origen.ReadAsync(bufer, ct)) > 0)
                    {
                        await salida.WriteAsync(bufer.AsMemory(0, n), ct);
                        leidos += n;
                        if (total > 0) avance?.Report((int)(leidos * 100 / total));
                    }
                }

                if (File.Exists(destino)) File.Delete(destino);
                File.Move(parcial, destino);

                InstaladorListo = destino;
                return destino;
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.NetworkLog, $"Descarga de la actualización: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lanza el instalador. La aplicación debe cerrarse justo después: el
        /// instalador espera a que suelte el mutex de instancia única.
        /// </summary>
        public bool Instalar(bool silencioso = true)
        {
            if (string.IsNullOrEmpty(InstaladorListo) || !File.Exists(InstaladorListo)) return false;

            try
            {
                // /SILENT deja ver la barra de progreso, para que se note que
                // está actualizando; /VERYSILENT no mostraría nada.
                string args = silencioso ? "/SILENT /NORESTART /SUPPRESSMSGBOXES" : "/NORESTART";

                Process.Start(new ProcessStartInfo
                {
                    FileName = InstaladorListo,
                    Arguments = args,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.CrashLog, $"No se pudo lanzar el instalador: {ex.Message}");
                return false;
            }
        }

        /// <summary>Borra instaladores descargados de versiones ya instaladas.</summary>
        public static void LimpiarDescargasViejas()
        {
            try
            {
                string carpeta = Path.Combine(Path.GetTempPath(), "WalkieTalkieUpdate");
                if (!Directory.Exists(carpeta)) return;

                foreach (string archivo in Directory.GetFiles(carpeta))
                {
                    var v = LeerVersion(Path.GetFileNameWithoutExtension(archivo)
                        .Replace("WalkieTalkie_", "").Replace("_Setup", ""));

                    if (v is null || v <= VersionActual)
                    {
                        try { File.Delete(archivo); } catch { }
                    }
                }
            }
            catch
            {
                // La limpieza es un extra: si falla, no pasa nada.
            }
        }
    }
}
