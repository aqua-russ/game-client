using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Effects
{
    /// <summary>
    /// Общая голова эффектов 0x70, 0xC0, 0xC7 (Projects/Server/Network/Packets/OutgoingEffectPackets.cs, CreateHuedEffect):
    /// byte вид, от, к, short графика, точка «от» (short X, Y, sbyte Z), точка «к», byte скорость, длительность, 0, 0,
    /// bool неподвижное направление, взрыв. 27 байт после номера.
    /// </summary>
    internal ref struct EffectHead
    {
        public EffectKind Kind;
        public Serial Source;
        public Serial Target;
        public int ItemId;
        public Point3D From;

        public static EffectHead Read(ref PacketReader reader)
        {
            var head = new EffectHead
            {
                Kind = (EffectKind)reader.ReadByte(),
                Source = new Serial(reader.ReadUInt32()),
                Target = new Serial(reader.ReadUInt32()),
                ItemId = reader.ReadUInt16(),
                From = new Point3D(reader.ReadInt16(), reader.ReadInt16(), reader.ReadSByte()),
            };
            reader.Skip(5 + 6);
            return head;
        }
    }
}
