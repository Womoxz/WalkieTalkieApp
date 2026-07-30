using System;
using System.Text;

namespace WalkieTalkieApp
{
    public enum PacketType : byte
    {
        /// <summary>Mensaje dirigido a una sola persona.</summary>
        Audio = 1,

        /// <summary>
        /// El mismo mensaje va a varios destinatarios. Se marca para que a quien lo
        /// recibe le quede claro que no le hablaban solo a él.
        /// </summary>
        AudioGrupo = 2

        // La presencia viaja por el canal de descubrimiento (DiscoveryService),
        // no por el de voz.
    }

    /// <summary>
    /// Antes se enviaba PCM crudo sin cabecera y al receptor solo le quedaba la IP
    /// para saber quién hablaba: si el DHCP cambiaba una IP, aparecía el número pelado.
    /// Ahora cada datagrama lleva quién lo envía y de qué tipo es.
    ///
    /// Formato: [ 'W' 'T' 0x02 ][ tipo:1 ][ largoNombre:1 ][ nombre UTF-8 ][ payload ]
    /// </summary>
    public static class AudioProtocol
    {
        private const byte Magic0 = (byte)'W';
        private const byte Magic1 = (byte)'T';
        private const byte Version = 2;

        public const int MaxNameBytes = 64;

        public static byte[] BuildAudio(string sender, byte[] pcm, int count, bool esGrupo = false)
        {
            byte[] name = Encoding.UTF8.GetBytes(sender);
            if (name.Length > MaxNameBytes) Array.Resize(ref name, MaxNameBytes);

            byte[] packet = new byte[4 + 1 + name.Length + count];
            packet[0] = Magic0;
            packet[1] = Magic1;
            packet[2] = Version;
            packet[3] = (byte)(esGrupo ? PacketType.AudioGrupo : PacketType.Audio);
            packet[4] = (byte)name.Length;
            Buffer.BlockCopy(name, 0, packet, 5, name.Length);
            Buffer.BlockCopy(pcm, 0, packet, 5 + name.Length, count);
            return packet;
        }

        public static bool TryParse(byte[] data, int length,
            out PacketType type, out string sender, out int payloadOffset, out int payloadLength)
        {
            type = default;
            sender = string.Empty;
            payloadOffset = 0;
            payloadLength = 0;

            if (length < 5) return false;
            if (data[0] != Magic0 || data[1] != Magic1 || data[2] != Version) return false;

            byte rawType = data[3];
            if (rawType != (byte)PacketType.Audio && rawType != (byte)PacketType.AudioGrupo)
                return false;
            type = (PacketType)rawType;

            int nameLen = data[4];
            if (nameLen > MaxNameBytes || 5 + nameLen > length) return false;

            sender = Encoding.UTF8.GetString(data, 5, nameLen);
            payloadOffset = 5 + nameLen;
            payloadLength = length - payloadOffset;
            return true;
        }
    }
}
