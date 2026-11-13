using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0xA9 Character List: слоты персонажей и стартовые города. Сервер: Projects/Server/Network/Packets/
    /// OutgoingAccountPackets.cs, SendCharacterList — формат 7.0.13.0+ (NewCharacterList, наш 7.0.95.0): byte число слотов,
    /// на слот имя UTF-8 30 байт (наше расширение E1) и 30 байт пустого пароля; byte число городов, на город byte номер,
    /// город и здание WriteLatin1(32), int X, Y, Z, номер карты, номер описания (cliloc), int 0; int флаги списка; short -1.
    /// </summary>
    public sealed class CharacterListHandler
    {
        public const byte PacketId = 0xA9;

        /// <summary>Имя UTF-8 (30) и пустой пароль (30).</summary>
        private const int SlotLength = NameLength + PasswordLength;
        private const int NameLength = 30;
        private const int PasswordLength = 30;
        private const int CityTextLength = 32;

        /// <summary>int 0 в конце города: сервер всегда пишет 0.</summary>
        private const int CityUnusedLength = 4;

        /// <summary>Номер (1), город и здание (2 × 32), X, Y, Z, карта, описание, 0 (6 × 4).</summary>
        private const int CityLength = 1 + 2 * CityTextLength + 6 * 4;

        /// <summary>После городов: int флаги и short -1.</summary>
        private const int TailLength = 4 + 2;

        private readonly LoginProgress _progress;
        private readonly CharacterSelection _characters;

        public CharacterListHandler(LoginProgress progress, CharacterSelection characters)
        {
            _progress = progress;
            _characters = characters;
        }

        public void Handle(ref PacketReader reader)
        {
            var slotCount = reader.Remaining > 0 ? reader.ReadByte() : -1;
            if (slotCount < 0 || reader.Remaining < slotCount * SlotLength + 1)
            {
                _progress.FailProtocol("0xA9: слотов " + slotCount + " — больше, чем помещается в пакет (" + reader.Length + " байт)");
                return;
            }

            var slots = new CharacterSlot[slotCount];
            for (var i = 0; i < slotCount; i++)
            {
                slots[i] = new CharacterSlot(i, reader.ReadUtf8(NameLength));
                reader.Skip(PasswordLength);
            }

            var cityCount = reader.ReadByte();
            if (reader.Remaining != cityCount * CityLength + TailLength)
            {
                _progress.FailProtocol("0xA9: городов " + cityCount + ", а данных после них " + reader.Remaining + " байт");
                return;
            }

            var cities = new StartingCity[cityCount];
            for (var i = 0; i < cityCount; i++)
            {
                cities[i] = ReadCity(ref reader);
            }

            _characters.OnCharacterList(slots, cities, reader.ReadInt32());
        }

        private static StartingCity ReadCity(ref PacketReader reader)
        {
            var index = reader.ReadByte();
            var city = reader.ReadLatin1(CityTextLength);
            var building = reader.ReadLatin1(CityTextLength);
            // Сервер пишет координаты int, но в UO они short (X, Y) и sbyte (Z): CityInfo хранит Point3D.
            var location = new Point3D((short)reader.ReadInt32(), (short)reader.ReadInt32(), (sbyte)reader.ReadInt32());
            var map = reader.ReadInt32();
            var description = reader.ReadInt32();
            reader.Skip(CityUnusedLength);
            return new StartingCity(index, city, building, location, map, description);
        }
    }
}
