using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0xF8 Create Character (клиенты 7.0.16+). Сервер: Projects/UOContent/Network/Packets/IncomingAccountPackets.cs,
    /// CreateCharacter — пропускает 9 байт, ReadUTF8(30) имя (наше расширение E1), пропускает 2, ReadInt32 флаги карт,
    /// пропускает 8, байт профессии, пропускает 15, байт пола и расы, 3 байта статов, 4 пары (навык, значение)
    /// (NewCharacterCreation), ushort цвет кожи, short причёска, цвет, борода, цвет, 1 байт, байт города, пропускает 8
    /// (слот и IP), short цвет рубашки и штанов; длина 106 (Register(0xF8, …, 106)).
    /// </summary>
    public static class CreateCharacterPacket
    {
        public const byte PacketId = 0xF8;

        private const int NameLength = 30;

        /// <summary>Пол и раса для клиентов 7.0+: человек — 2 (мужчина) и 3 (женщина); нечётное — женщина.</summary>
        private const byte HumanMale = 2;
        private const byte HumanFemale = 3;

        /// <summary>Профессия 0 — «своя»: навыки выбраны вручную.</summary>
        private const byte CustomProfession = 0;

        public static void Write(ref PacketWriter writer, NewCharacter c, int slot)
        {
            writer.Write(PacketId);
            writer.Write(0xEDEDEDEDu); // три поля, которые сервер пропускает: 0xEDEDEDED, 0xFFFFFFFF, 0
            writer.Write(0xFFFFFFFFu);
            writer.WriteZeros(1);
            writer.WriteUtf8(c.Name, NameLength);
            writer.WriteZeros(2);
            writer.Write(ClientMapFlags.FeluccaOnly);
            writer.WriteZeros(8);
            writer.Write(CustomProfession);
            writer.WriteZeros(15);
            writer.Write(c.Female ? HumanFemale : HumanMale);
            writer.Write((byte)c.Strength);
            writer.Write((byte)c.Dexterity);
            writer.Write((byte)c.Intelligence);
            WriteSkills(ref writer, c.Skills);
            WriteLooks(ref writer, c);
            writer.WriteZeros(1);
            writer.Write((byte)c.CityIndex);
            writer.Write(slot);
            writer.WriteZeros(4); // IP клиента
            writer.Write((short)c.ShirtHue);
            writer.Write((short)c.PantsHue);
        }

        private static void WriteSkills(ref PacketWriter writer, (int Skill, int Value)[] skills)
        {
            foreach (var (skill, value) in skills)
            {
                writer.Write((byte)skill);
                writer.Write((byte)value);
            }
        }

        private static void WriteLooks(ref PacketWriter writer, NewCharacter c)
        {
            writer.Write((ushort)c.SkinHue);
            writer.Write((short)c.HairStyle);
            writer.Write((short)c.HairHue);
            writer.Write((short)c.BeardStyle);
            writer.Write((short)c.BeardHue);
        }
    }
}
