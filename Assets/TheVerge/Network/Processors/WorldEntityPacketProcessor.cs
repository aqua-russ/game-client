using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using TheVerge.Core;

namespace TheVerge.Network.Processors
{
    public sealed class WorldEntityPacketProcessor
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly Dictionary<Serial, string> _knownMobileNames = new Dictionary<Serial, string>();

        public event Action<MobileStateDto> OnMobileSpawned;
        public event Action<MobileStateDto> OnMobileMoved;
        public event Action<MobileStateDto> OnMobileUpdated;
        public event Action<Serial, string> OnMobileNameUpdated;
        public event Action<ItemStateDto> OnWorldItemSpawned;
        public event Action<Serial> OnEntityDespawned;
        public event Action<Serial, ItemStateDto> OnItemEquipped;
        public event Action<Serial> OnBackpackSerialDiscovered;
        public event Action<Direction, Point3D, bool> OnLocalPlayerUpdatedFromNetwork;

        public WorldEntityPacketProcessor(IMainThreadDispatcher dispatcher) =>
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        public void ParseMobileIncoming(ReadOnlySpan<byte> packet, Serial localPlayerSerial)
        {
            if (packet.Length < 23) return;
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(3, 4));
            ushort body = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(7, 2));
            short x = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(9, 2));
            short y = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(11, 2));
            sbyte z = (sbyte)packet[13];
            var dir = (Direction)(packet[14] & 0x07);
            ushort hue = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(15, 2));
            byte flags = packet[17], notoriety = packet[18];

            ParseEquippedLayersInIncoming(packet, serial, localPlayerSerial);
            if (serial == localPlayerSerial.Value) return;

            var mobSerial = new Serial(serial);
            string knownName = _knownMobileNames.TryGetValue(mobSerial, out var n) ? n : GetDefaultName(body, notoriety);
            var dto = new MobileStateDto(mobSerial, body, hue, new Point3D(x, y, z), dir, notoriety, knownName, 100, 100, (flags & 0x40) != 0);
            _dispatcher.Enqueue(() => OnMobileSpawned?.Invoke(dto));
        }

        private void ParseEquippedLayersInIncoming(ReadOnlySpan<byte> packet, uint serial, Serial localPlayerSerial)
        {
            int eqOffset = 19;
            while (eqOffset + 7 <= packet.Length)
            {
                uint itemSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(eqOffset, 4));
                if (itemSerial == 0) break;
                ushort rawGraphic = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(eqOffset + 4, 2));
                byte layerByte = packet[eqOffset + 6];
                bool hasHue = (rawGraphic & 0x8000) != 0;
                ushort cleanGraphic = (ushort)(rawGraphic & 0x7FFF), itemHue = 0;
                eqOffset += 7;
                if (hasHue && eqOffset + 2 <= packet.Length)
                {
                    itemHue = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(eqOffset, 2));
                    eqOffset += 2;
                }
                if (serial == localPlayerSerial.Value)
                {
                    var layer = (Layer)layerByte;
                    if (layer == Layer.Backpack) OnBackpackSerialDiscovered?.Invoke(new Serial(itemSerial));
                    else if (layer == Layer.OneHanded || layer == Layer.TwoHanded)
                    {
                        var eqDto = new ItemStateDto(new Serial(itemSerial), cleanGraphic, itemHue, 1, Point3D.Zero, localPlayerSerial, layer);
                        _dispatcher.Enqueue(() => OnItemEquipped?.Invoke(localPlayerSerial, eqDto));
                    }
                }
            }
        }

        public void ParseMobileMoving(ReadOnlySpan<byte> packet, Serial localPlayerSerial)
        {
            if (packet.Length < 17) return;
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            if (serial == localPlayerSerial.Value) return;

            ushort body = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            short x = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(7, 2));
            short y = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(9, 2));
            sbyte z = (sbyte)packet[11];
            var dir = (Direction)(packet[12] & 0x07);
            ushort hue = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(13, 2));
            byte flags = packet[15], notoriety = packet[16];

            var mobSerial = new Serial(serial);
            string knownName = _knownMobileNames.TryGetValue(mobSerial, out var n) ? n : GetDefaultName(body, notoriety);
            var dto = new MobileStateDto(mobSerial, body, hue, new Point3D(x, y, z), dir, notoriety, knownName, 100, 100, (flags & 0x40) != 0);
            _dispatcher.Enqueue(() => OnMobileMoved?.Invoke(dto));
        }

        public void ParseMobileUpdate(ReadOnlySpan<byte> packet, Serial localPlayerSerial)
        {
            if (packet.Length < 19) return;
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort body = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            ushort hue = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(8, 2));
            byte flags = packet[10];
            short x = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(11, 2));
            short y = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(13, 2));
            var dir = (Direction)(packet[17] & 0x07);
            sbyte z = (sbyte)packet[18];
            bool war = (flags & 0x40) != 0;

            if (serial == localPlayerSerial.Value)
            {
                _dispatcher.Enqueue(() => OnLocalPlayerUpdatedFromNetwork?.Invoke(dir, new Point3D(x, y, z), war));
                return;
            }

            var mobSerial = new Serial(serial);
            string knownName = _knownMobileNames.TryGetValue(mobSerial, out var n) ? n : GetDefaultName(body, 1);
            var dto = new MobileStateDto(mobSerial, body, hue, new Point3D(x, y, z), dir, 1, knownName, 100, 100, war);
            _dispatcher.Enqueue(() => OnMobileUpdated?.Invoke(dto));
        }

        public void ParseWorldItem(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 24) return;
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(4, 4));
            ushort graphic = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(8, 2));
            ushort amount = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(11, 2));
            short x = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(15, 2));
            short y = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(17, 2));
            sbyte z = (sbyte)packet[19];
            ushort hue = packet.Length >= 23 ? BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(21, 2)) : (ushort)0;

            var item = new ItemStateDto(new Serial(serial), graphic, hue, amount, new Point3D(x, y, z), Serial.Zero, Layer.Invalid);
            _dispatcher.Enqueue(() => OnWorldItemSpawned?.Invoke(item));
        }

        public void ParseWorldItem0x1A(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 9) return;
            uint rawSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(3, 4));
            bool hasAmount = (rawSerial & 0x80000000) != 0;
            uint serial = rawSerial & 0x7FFFFFFF;
            ushort rawGraphic = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(7, 2));
            ushort graphic = (ushort)(rawGraphic & 0x3FFF), amount = 1;
            int offset = 9;
            if (hasAmount && offset + 2 <= packet.Length)
            {
                amount = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset, 2));
                offset += 2;
            }

            if (offset + 5 > packet.Length) return;
            ushort rawX = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset, 2));
            ushort rawY = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset + 2, 2));
            sbyte z = (sbyte)packet[offset + 4];
            offset += 5;

            bool hasDirection = (rawX & 0x8000) != 0, hasHue = (rawY & 0x8000) != 0;
            short x = (short)(rawX & 0x7FFF), y = (short)(rawY & 0x3FFF);
            if (hasDirection && offset < packet.Length) offset++;

            ushort hue = 0;
            if (hasHue && offset + 2 <= packet.Length)
            {
                hue = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset, 2));
            }

            var item = new ItemStateDto(new Serial(serial), graphic, hue, amount, new Point3D(x, y, z), Serial.Zero, Layer.Invalid);
            _dispatcher.Enqueue(() => OnWorldItemSpawned?.Invoke(item));
        }

        public void ParseDeleteObject(ReadOnlySpan<byte> packet)
        {
            uint delSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            _dispatcher.Enqueue(() => OnEntityDespawned?.Invoke(new Serial(delSerial)));
        }

        public void ParseMobileName(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 37) return;
            uint nameSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            int len = packet.Slice(5, 30).IndexOf((byte)0);
            if (len < 0) len = 30;
            string mobName = len > 0 ? Encoding.UTF8.GetString(packet.Slice(5, len)).Trim() : string.Empty;
            if (!string.IsNullOrEmpty(mobName))
            {
                var ser = new Serial(nameSerial);
                _knownMobileNames[ser] = mobName;
                _dispatcher.Enqueue(() => OnMobileNameUpdated?.Invoke(ser, mobName));
            }
        }

        private static string GetDefaultName(ushort body, byte notoriety) =>
            (body == 50 || body == 56 || (notoriety >= 3 && notoriety <= 6))
                ? (body == 56 ? "Костяной Рудокоп" : "Страж Шахты")
                : "Добрыня (Кузнец)";
    }
}
