using System;
using System.Text;
using TheVerge.Core;

namespace TheVerge.Network
{
    /// <summary>
    /// Билдер пакета 0xF8 (Character Creation) протокола Ultima Online (106 байт).
    /// Передаёт серверу имя, пол, характеристики и 3 стартовых навыка (50/50/10).
    /// </summary>
    public static class CharacterCreationPacketBuilder
    {
        public const int PacketSize = 106;

        public static byte[] Build(CharacterCreationArgsDto args)
        {
            byte[] packet = new byte[PacketSize];
            packet[0] = 0xF8;

            // Имя персонажа (30 байт в кодировке UTF-8, смещение 10)
            byte[] nameBytes = Encoding.UTF8.GetBytes(args.CharacterName);
            int nameLen = Math.Min(nameBytes.Length, 29);
            Array.Copy(nameBytes, 0, packet, 10, nameLen);

            // Пол и раса (0 = Human Male, 1 = Human Female)
            packet[70] = (byte)(args.Female ? 1 : 0);

            // Стартовые характеристики (STR, DEX, INT)
            packet[71] = args.Strength;
            packet[72] = args.Dexterity;
            packet[73] = args.Intelligence;

            // Стартовые навыки: 4 слота по 2 байта (Id, Value)
            packet[74] = (byte)args.Skill1;
            packet[75] = args.Skill1Value;
            packet[76] = (byte)args.Skill2;
            packet[77] = args.Skill2Value;
            packet[78] = (byte)args.Skill3;
            packet[79] = args.Skill3Value;
            packet[80] = 0;
            packet[81] = 0;

            // Цвета кожи и стартовой одежды (Big-Endian)
            NetByteConverter.WriteUInt16(packet, 82, args.SkinHue);
            packet[93] = 0; // City Index (Minoc)
            NetByteConverter.WriteUInt16(packet, 102, args.ShirtHue);
            NetByteConverter.WriteUInt16(packet, 104, args.PantsHue);

            return packet;
        }
    }
}
