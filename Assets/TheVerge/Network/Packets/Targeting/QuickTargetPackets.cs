using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Targeting
{
    /// <summary>
    /// Быстрые цели 0xBF от клиента — как их читает сервер (Projects/UOContent/Network/Packets/IncomingExtendedCommandPackets.cs):
    /// 0x2C BandageTarget (uint бинт, uint цель), 0x2D TargetedSpell (short номер с 1, uint цель), 0x2E TargetedSkillUse
    /// (short навык, uint цель), 0x30 TargetByResourceMacro (uint предмет, short ресурс). Заголовок — 0xBF, длина, подкоманда.
    /// </summary>
    public static class QuickTargetPackets
    {
        public const byte PacketId = 0xBF;
        public const ushort BandageSubCommand = 0x2C;
        public const ushort SpellSubCommand = 0x2D;
        public const ushort SkillSubCommand = 0x2E;
        public const ushort ResourceSubCommand = 0x30;
        private const int HeaderLength = 1 + 2 + 2;

        /// <summary>Заголовок, два серийника.</summary>
        public const int BandageLength = HeaderLength + 4 + 4;

        /// <summary>Заголовок, short и серийник.</summary>
        public const int ShortAndSerialLength = HeaderLength + 2 + 4;

        public static void WriteBandage(ref PacketWriter writer, Serial bandage, Serial target)
        {
            Header(ref writer, BandageSubCommand, BandageLength);
            writer.Write(bandage.Value);
            writer.Write(target.Value);
        }

        /// <summary>0x2D и 0x2E: short номер, затем серийник цели.</summary>
        public static void WriteNumberAndTarget(ref PacketWriter writer, ushort subCommand, int number, Serial target)
        {
            Header(ref writer, subCommand, ShortAndSerialLength);
            writer.Write((short)number);
            writer.Write(target.Value);
        }

        public static void WriteResource(ref PacketWriter writer, Serial item, int resourceType)
        {
            Header(ref writer, ResourceSubCommand, ShortAndSerialLength);
            writer.Write(item.Value);
            writer.Write((short)resourceType);
        }

        private static void Header(ref PacketWriter writer, ushort subCommand, int length)
        {
            writer.Write(PacketId);
            writer.Write((ushort)length);
            writer.Write(subCommand);
        }
    }
}
