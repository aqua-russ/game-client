using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Status
{
    /// <summary>
    /// 0x11 Mobile Status. Сервер: Projects/Server/Network/Packets/OutgoingMobilePackets.cs, CreateMobileStatus — Serial,
    /// имя UTF-8 30 байт, здоровье «текущее, максимум» (у краткого — долей от 100), переименовать ли, версия; дальше при
    /// версии больше 0 — пол, сила, ловкость, интеллект, выносливость, её максимум, мана, её максимум (short), золото (int),
    /// броня, вес (short), [версия 5+: максимум веса, раса], предел статов (short), питомцы, их максимум (byte), [4+: ещё].
    /// Длина пакета обязана совпасть с версией — иначе ошибка пакета, состояние не меняется.
    /// </summary>
    public sealed class MobileStatusHandler
    {
        public const byte PacketId = 0x11;
        private const int NameLength = 30;

        private readonly MobileStatusUpdates _updates;

        public MobileStatusHandler(MobileStatusUpdates updates)
        {
            _updates = updates;
        }

        public void Handle(ref PacketReader reader)
        {
            var total = reader.Remaining + 3;
            var serial = new Serial(reader.ReadUInt32());
            var name = reader.ReadUtf8(NameLength);
            var hits = new Vital(reader.ReadInt16(), reader.ReadInt16());
            var canBeRenamed = reader.ReadBoolean();
            var version = reader.ReadByte();
            if (total != LengthOf(version))
            {
                throw new PacketOutOfRangeException("0x11: длина " + total + " не сходится с версией статуса " + version + ".");
            }

            if (version == 0)
            {
                _updates.OnCompactStatus(serial, name, hits, canBeRenamed);
                return;
            }

            _updates.OnFullStatus(serial, name, hits, canBeRenamed, version, ReadSheet(ref reader, version));
        }

        // Длины по версиям — MobileStatusCompactLength, MobileStatusLength, …AOSLength, …MLLength, …HSLength сервера.
        private static int LengthOf(int version) =>
            version == 0 ? 43 : version <= 3 ? 70 : version == 4 ? 88 : version == 5 ? 91 : version == 6 ? 121 : -1;

        private static StatusSheet ReadSheet(ref PacketReader reader, int version)
        {
            var female = reader.ReadBoolean();
            int str = reader.ReadInt16(), dex = reader.ReadInt16(), intel = reader.ReadInt16();
            var stam = new Vital(reader.ReadInt16(), reader.ReadInt16());
            var mana = new Vital(reader.ReadInt16(), reader.ReadInt16());
            var gold = reader.ReadInt32();
            int armor = reader.ReadInt16(), weight = reader.ReadInt16();
            if (version >= 5)
            {
                reader.ReadInt16(); // максимум веса — в UOR не приходит
                reader.ReadByte(); // раса
            }

            int statCap = reader.ReadInt16();
            int followers = reader.ReadByte(), followersMax = reader.ReadByte();
            return new StatusSheet(female, stam, mana, new StatusAttributes(str, dex, intel, gold, armor, weight, statCap), followers, followersMax);
        }
    }
}
