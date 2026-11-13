using System.Globalization;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>IPv4 строкой «a.b.c.d» из четырёх байт адреса в обычном порядке (протокол UO знает только IPv4).</summary>
    internal static class IPv4Text
    {
        public const int Length = 4;

        public static string Format(byte a, byte b, byte c, byte d) =>
            a.ToString(CultureInfo.InvariantCulture) + "." + b.ToString(CultureInfo.InvariantCulture) + "." +
            c.ToString(CultureInfo.InvariantCulture) + "." + d.ToString(CultureInfo.InvariantCulture);
    }
}
