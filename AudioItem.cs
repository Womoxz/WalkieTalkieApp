using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace WalkieTalkieApp
{
    public enum AudioDirection
    {
        Recibido,
        Enviado
    }

    public class AudioItem
    {
        public string FilePath { get; set; } = string.Empty;
        /// <summary>Quien envió (recibidos) o a quien se envió (enviados).</summary>
        public string Contact { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public AudioDirection Direction { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Unread { get; set; }

        /// <summary>
        /// Todos los destinatarios cuando el mensaje se envió a varios a la vez.
        /// El archivo es uno solo: aparece en la conversación de cada uno.
        /// </summary>
        public List<string> Recipients { get; set; } = new();

        /// <summary>En los recibidos: el emisor hablaba a varias personas, no solo a ti.</summary>
        public bool EsGrupo { get; set; }

        public bool EnviadoAVarios => Recipients.Count > 1;

        /// <summary>Texto del destinatario: "Daniel" o "Daniel +2".</summary>
        public string RecipientsText => Recipients.Count switch
        {
            0 => Contact,
            1 => Recipients[0],
            _ => $"{Contact} +{Recipients.Count - 1}"
        };

        public string TimeText => Timestamp.Date == DateTime.Today
            ? Timestamp.ToString("HH:mm", CultureInfo.CurrentCulture)
            : Timestamp.ToString("dd/MM HH:mm", CultureInfo.CurrentCulture);

        public string DurationText => Duration > TimeSpan.Zero
            ? $"{(int)Duration.TotalSeconds}s"
            : string.Empty;

        public string DisplayText =>
            $"{(Direction == AudioDirection.Recibido ? "◀" : "▶")}  {Contact} · {TimeText} {DurationText}".Trim();

        public override string ToString() => DisplayText;

        // Nombre de archivo: Contacto_yyyyMMdd_HHmmss.wav
        // El contacto puede llevar guiones bajos, así que anclamos al final.
        private static readonly Regex FileNamePattern =
            new(@"^(?<name>.+)_(?<stamp>\d{8}_\d{6})$", RegexOptions.Compiled);

        /// <summary>
        /// Antes esto hacía Split('_') y parseaba solo parts[1] ("20250708") con el
        /// formato "yyyyMMdd_HHmmss": fallaba siempre y el historial nunca cargaba.
        ///
        /// Un mensaje enviado a varios se guarda una sola vez, con los nombres unidos
        /// por "+", y devuelve una entrada por destinatario apuntando al mismo archivo:
        /// así aparece en la conversación de cada uno sin duplicar el audio en disco.
        /// </summary>
        public static List<AudioItem> FromFile(string filePath, AudioDirection direction)
        {
            var result = new List<AudioItem>();

            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                var match = FileNamePattern.Match(fileName);

                string namePart;
                DateTime timestamp;

                if (match.Success &&
                    DateTime.TryParseExact(match.Groups["stamp"].Value, "yyyyMMdd_HHmmss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
                {
                    namePart = match.Groups["name"].Value;
                }
                else
                {
                    // Archivo con otro nombre: no lo descartamos, usamos la fecha del archivo.
                    namePart = fileName;
                    timestamp = File.GetLastWriteTime(filePath);
                }

                var recipients = new List<string>(
                    namePart.Split('+', StringSplitOptions.RemoveEmptyEntries));

                if (recipients.Count == 0) recipients.Add(namePart);

                TimeSpan duration = ReadDuration(filePath);

                foreach (string contact in recipients)
                {
                    result.Add(new AudioItem
                    {
                        FilePath = filePath,
                        Contact = contact.Trim(),
                        Timestamp = timestamp,
                        Direction = direction,
                        Duration = duration,
                        Recipients = recipients.Count > 1 ? new List<string>(recipients) : new List<string>()
                    });
                }
            }
            catch
            {
                // Archivo ilegible: se ignora en vez de tumbar la carga del historial.
            }

            return result;
        }

        /// <summary>Lee la duración de la cabecera del WAV (no carga el audio).</summary>
        public static TimeSpan ReadDuration(string filePath)
        {
            try
            {
                using var reader = new NAudio.Wave.WaveFileReader(filePath);
                return reader.TotalTime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        public static string BuildFileName(string contact, DateTime when) =>
            BuildFileName(new[] { contact }, when);

        /// <summary>Nombre para un mensaje con uno o varios destinatarios.</summary>
        public static string BuildFileName(IEnumerable<string> contacts, DateTime when)
        {
            var limpios = new List<string>();

            foreach (string original in contacts)
            {
                string nombre = original;

                // Los caracteres inválidos romperían File.Create con una excepción
                // críptica; el "+" separa destinatarios, así que tampoco puede ir dentro.
                foreach (char c in Path.GetInvalidFileNameChars())
                    nombre = nombre.Replace(c, '-');
                nombre = nombre.Replace('+', '-');

                if (nombre.Length > 0) limpios.Add(nombre);
            }

            if (limpios.Count == 0) limpios.Add("Desconocido");

            string parte = string.Join("+", limpios);

            // Windows corta las rutas en 260 caracteres: con muchos destinatarios el
            // nombre se sustituye por uno genérico en vez de fallar al guardar.
            if (parte.Length > 120) parte = $"Varios({limpios.Count})";

            return $"{parte}_{when:yyyyMMdd_HHmmss}.wav";
        }
    }
}
