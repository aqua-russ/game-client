using System;
using System.Buffers.Binary;
using System.Text;
using TheVerge.Core;

namespace TheVerge.Network.Processors
{
    /// <summary>
    /// Процессор сетевых пакетов характеристик и статуса персонажа (0x11, 0xA1, 0xA2, 0xA3, 0x2D, 0x34).
    /// Чистый C#-класс без UnityEngine.
    /// </summary>
    public sealed class StatsPacketProcessor
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly Action<byte[]> _sendRawAction;

        public PlayerStatusDto PlayerStatus { get; private set; } = PlayerStatusDto.CreateDefault(new Serial(0x00010001));

        public event Action<PlayerStatusDto> OnPlayerStatusUpdated;
        public event Action<Serial, ushort, ushort> OnMobileHitsUpdated;

        public StatsPacketProcessor(IMainThreadDispatcher dispatcher, Action<byte[]> sendRawAction)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _sendRawAction = sendRawAction ?? throw new ArgumentNullException(nameof(sendRawAction));
        }

        public void SetPlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var cur = PlayerStatus;
            var updated = new PlayerStatusDto(
                cur.Serial, name, cur.Hits, cur.MaxHits, cur.Mana, cur.MaxMana,
                cur.Stamina, cur.MaxStamina, cur.Str, cur.Dex, cur.Int, cur.Gold,
                cur.ArmorRating, cur.Weight, cur.MaxWeight);

            _dispatcher.Enqueue(() =>
            {
                PlayerStatus = updated;
                OnPlayerStatusUpdated?.Invoke(PlayerStatus);
            });
        }

        public void ParseMobileStatus(ReadOnlySpan<byte> packet, Serial localPlayerSerial)
        {
            if (packet.Length < 43) return;
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(3, 4));
            string name = ReadFixedString(packet.Slice(7, 30));
            ushort hits = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(37, 2));
            ushort maxHits = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(39, 2));
            byte version = packet[42];

            if (serial == localPlayerSerial.Value && version >= 1 && packet.Length >= 66)
            {
                var dto = BuildPlayerStatusDto(packet, localPlayerSerial, name, hits, maxHits, version);
                _dispatcher.Enqueue(() =>
                {
                    PlayerStatus = dto;
                    OnPlayerStatusUpdated?.Invoke(dto);
                    OnMobileHitsUpdated?.Invoke(localPlayerSerial, hits, maxHits);
                });
            }
            else
            {
                _dispatcher.Enqueue(() => OnMobileHitsUpdated?.Invoke(new Serial(serial), hits, maxHits));
            }
        }

        public void ParseHitsDelta(ReadOnlySpan<byte> packet, Serial localPlayerSerial)
        {
            if (packet.Length < 9) return;
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort maxH = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            ushort curH = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(7, 2));

            _dispatcher.Enqueue(() =>
            {
                if (serial == localPlayerSerial.Value)
                {
                    var c = PlayerStatus;
                    PlayerStatus = new PlayerStatusDto(
                        localPlayerSerial, c.Name, curH, maxH, c.Mana, c.MaxMana,
                        c.Stamina, c.MaxStamina, c.Str, c.Dex, c.Int, c.Gold,
                        c.ArmorRating, c.Weight, c.MaxWeight);
                    OnPlayerStatusUpdated?.Invoke(PlayerStatus);
                }
                OnMobileHitsUpdated?.Invoke(new Serial(serial), curH, maxH);
            });
        }

        public void ParseManaDelta(ReadOnlySpan<byte> packet, Serial localPlayerSerial)
        {
            if (packet.Length < 9) return;
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort maxM = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            ushort curM = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(7, 2));

            if (serial != localPlayerSerial.Value) return;
            _dispatcher.Enqueue(() =>
            {
                var c = PlayerStatus;
                PlayerStatus = new PlayerStatusDto(
                    localPlayerSerial, c.Name, c.Hits, c.MaxHits, curM, maxM,
                    c.Stamina, c.MaxStamina, c.Str, c.Dex, c.Int, c.Gold,
                    c.ArmorRating, c.Weight, c.MaxWeight);
                OnPlayerStatusUpdated?.Invoke(PlayerStatus);
            });
        }

        public void ParseStaminaDelta(ReadOnlySpan<byte> packet, Serial localPlayerSerial)
        {
            if (packet.Length < 9) return;
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort maxS = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            ushort curS = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(7, 2));

            if (serial != localPlayerSerial.Value) return;
            _dispatcher.Enqueue(() =>
            {
                var c = PlayerStatus;
                PlayerStatus = new PlayerStatusDto(
                    localPlayerSerial, c.Name, c.Hits, c.MaxHits, c.Mana, c.MaxMana,
                    curS, maxS, c.Str, c.Dex, c.Int, c.Gold,
                    c.ArmorRating, c.Weight, c.MaxWeight);
                OnPlayerStatusUpdated?.Invoke(PlayerStatus);
            });
        }

        public void ParseMobileAttributes(ReadOnlySpan<byte> packet, Serial localPlayerSerial)
        {
            if (packet.Length < 17) return;
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort maxHits = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            ushort hits = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(7, 2));
            ushort maxMana = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(9, 2));
            ushort mana = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(11, 2));
            ushort maxStam = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(13, 2));
            ushort stam = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(15, 2));

            _dispatcher.Enqueue(() =>
            {
                if (serial == localPlayerSerial.Value)
                {
                    var c = PlayerStatus;
                    PlayerStatus = new PlayerStatusDto(
                        localPlayerSerial, c.Name, hits, maxHits, mana, maxMana, stam, maxStam,
                        c.Str, c.Dex, c.Int, c.Gold, c.ArmorRating, c.Weight, c.MaxWeight);
                    OnPlayerStatusUpdated?.Invoke(PlayerStatus);
                }
                OnMobileHitsUpdated?.Invoke(new Serial(serial), hits, maxHits);
            });
        }

        public void RequestStats(Serial mobileSerial)
        {
            byte[] packet = new byte[10];
            packet[0] = 0x34;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(1, 4), 0xEDEDEDED);
            packet[5] = 0x04;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(6, 4), mobileSerial.Value);
            _sendRawAction(packet);
        }

        private PlayerStatusDto BuildPlayerStatusDto(
            ReadOnlySpan<byte> packet, Serial serial, string name, ushort hits, ushort maxHits, byte version)
        {
            ushort str = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(44, 2));
            ushort dex = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(46, 2));
            ushort intel = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(48, 2));
            ushort stam = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(50, 2));
            ushort maxStam = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(52, 2));
            ushort mana = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(54, 2));
            ushort maxMana = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(56, 2));
            uint gold = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(58, 4));
            ushort armor = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(62, 2));
            ushort weight = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(64, 2));

            ushort maxWeight = (version >= 5 && packet.Length >= 68)
                ? BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(66, 2))
                : (ushort)(40 + (int)(str * 3.5f));
            if (maxWeight == 0) maxWeight = (ushort)(40 + (int)(str * 3.5f));

            string validName = (!string.IsNullOrWhiteSpace(name) && name != "Hero") ? name : PlayerStatus.Name;
            return new PlayerStatusDto(
                serial, validName, hits, maxHits, mana, maxMana, stam, maxStam,
                str, dex, intel, gold, armor, weight, maxWeight);
        }

        private static string ReadFixedString(ReadOnlySpan<byte> span)
        {
            int len = span.IndexOf((byte)0);
            if (len < 0) len = span.Length;
            return len > 0 ? Encoding.UTF8.GetString(span.Slice(0, len)).Trim() : string.Empty;
        }
    }
}
