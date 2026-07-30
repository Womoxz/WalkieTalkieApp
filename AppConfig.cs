using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Configuración de la app. Se lee y se ESCRIBE en appsettings.json
    /// (antes solo se leía y los contactos había que editarlos a mano).
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Nota para quien abra el archivo a mano. Se conserva al guardar; si no
        /// estuviera en el modelo, el primer guardado la borraría.
        /// </summary>
        [JsonPropertyName("_comentario")]
        public string? Comentario { get; set; } =
            "Los contactos se descubren solos en la red: no hace falta escribir IPs aquí.";

        public Dictionary<string, string> Contactos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public AudioConfig Audio { get; set; } = new();
        public GeneralConfig General { get; set; } = new();

        [JsonIgnore]
        public static AppConfig Current { get; private set; } = new();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            // Sin esto los acentos se guardan como \u00F3 y el archivo queda ilegible.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(AppPaths.ConfigFile))
                {
                    string json = File.ReadAllText(AppPaths.ConfigFile);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                    if (cfg != null)
                    {
                        // El diccionario deserializado no conserva el comparador.
                        cfg.Contactos = new Dictionary<string, string>(
                            cfg.Contactos ?? new Dictionary<string, string>(),
                            StringComparer.OrdinalIgnoreCase);
                        cfg.Audio ??= new AudioConfig();
                        cfg.General ??= new GeneralConfig();
                        Current = cfg;
                        return cfg;
                    }
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log(AppPaths.CrashLog, $"Error leyendo configuración: {ex.Message}");
                MessageBox.Show(
                    $"No se pudo leer appsettings.json:\n\n{ex.Message}\n\nSe usarán valores predeterminados.",
                    "Configuración", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Current = new AppConfig();
            return Current;
        }

        public bool Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, JsonOpts);

                // Copia de seguridad antes de sobrescribir: la lista de contactos es
                // el dato más valioso del archivo y un descuido en el diálogo de
                // configuración la deja irrecuperable.
                if (File.Exists(AppPaths.ConfigFile))
                {
                    try { File.Copy(AppPaths.ConfigFile, AppPaths.ConfigFile + ".bak", true); }
                    catch { /* la copia es un extra, no debe impedir guardar */ }
                }

                // Escritura atómica: si se corta la corriente a mitad, el archivo
                // original sigue intacto en lugar de quedar truncado.
                string temp = AppPaths.ConfigFile + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, AppPaths.ConfigFile, true);

                Current = this;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo guardar la configuración:\n\n{ex.Message}",
                    "Configuración", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>Contactos distintos del usuario actual, ordenados alfabéticamente.</summary>
        public IEnumerable<KeyValuePair<string, string>> ContactosExternos(string userName)
        {
            foreach (var c in Contactos)
            {
                if (!string.Equals(c.Key, userName, StringComparison.OrdinalIgnoreCase))
                    yield return c;
            }
        }

        public string? BuscarNombrePorIp(string ip)
        {
            foreach (var c in Contactos)
            {
                if (c.Value == ip) return c.Key;
            }
            return null;
        }
    }

    public class AudioConfig
    {
        /// <summary>
        /// 16 kHz es más que suficiente para voz. Con 44100 cada transmisión
        /// gastaba 88 KB/s y generaba paquetes UDP fragmentados.
        /// </summary>
        public int SampleRate { get; set; } = 16000;

        /// <summary>
        /// A 16 kHz/16 bits, 40 ms = 1280 bytes de audio: cabe en un solo
        /// datagrama sin fragmentación IP (MTU 1500).
        /// </summary>
        public int BufferMilliseconds { get; set; } = 40;

        public int PlaybackLatencyMs { get; set; } = 120;

        /// <summary>-1 = dispositivo predeterminado de Windows.</summary>
        public int InputDevice { get; set; } = -1;
        public int OutputDevice { get; set; } = -1;

        /// <summary>0-100.</summary>
        public int Volumen { get; set; } = 100;

        /// <summary>
        /// Mantener el micrófono abierto evita perder el primer medio segundo de
        /// cada frase (abrir el dispositivo tarda ~100 ms) y permite el medidor de nivel.
        /// </summary>
        public bool MantenerMicrofonoAbierto { get; set; } = true;

        public bool SonidosDeAviso { get; set; } = true;
    }

    public class GeneralConfig
    {
        public int Puerto { get; set; } = 5000;

        public string TeclaPTT { get; set; } = "F7";

        /// <summary>
        /// Si es true, la tecla PTT deja de funcionar en el resto de Windows.
        /// Antes estaba forzado a true y F7 quedaba inutilizable en Excel, navegadores, etc.
        /// </summary>
        public bool SuprimirTeclaGlobal { get; set; } = false;

        /// <summary>Corta la transmisión si la tecla se queda "pegada" (Alt+Tab con PTT pulsado).</summary>
        public int MaxSegundosTransmision { get; set; } = 60;

        /// <summary>0 = no borrar nunca.</summary>
        public int DiasRetencionAudios { get; set; } = 30;

        public bool MinimizarABandeja { get; set; } = true;

        /// <summary>Ignora audio de equipos que no estén en la lista de contactos.</summary>
        public bool SoloContactosConocidos { get; set; } = true;

        /// <summary>
        /// Busca compañeros en la red automáticamente: no hace falta escribir
        /// ninguna IP a mano ni actualizarlas cuando el DHCP las cambia.
        /// </summary>
        public bool DescubrimientoAutomatico { get; set; } = true;

        /// <summary>Puerto del broadcast de descubrimiento (distinto del de audio).</summary>
        public int PuertoDescubrimiento { get; set; } = 5001;

        /// <summary>Conserva en appsettings.json los contactos encontrados.</summary>
        public bool GuardarContactosDescubiertos { get; set; } = true;

        [JsonIgnore]
        public Keys TeclaPTTKey
        {
            get => Enum.TryParse<Keys>(TeclaPTT, true, out var k) ? k : Keys.F7;
            set => TeclaPTT = value.ToString();
        }
    }
}
