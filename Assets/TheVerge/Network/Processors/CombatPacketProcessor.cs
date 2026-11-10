using System;
using System.Buffers.Binary;
using TheVerge.Core;

namespace TheVerge.Network.Processors
{
    /// <summary>
    /// Процессор сетевых пакетов боевых действий и эффектов (0x0B, 0x2F, 0x6E, 0x72, 0x6C, 0x05).
    /// Чистый C#-класс без UnityEngine.
    /// </summary>
    public sealed class CombatPacketProcessor
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly Action<byte[]> _sendRawAction;

        public bool IsWarMode { get; set; }

        public event Action<CombatDamageDto> OnDamageDealt;
        public event Action<Serial, Serial> OnCombatSwing;
        public event Action<Serial, int> OnMobileAnimation;
        public event Action<bool> OnWarModeChanged;
        public event Action<TargetCursorRequestDto> OnTargetCursorRequested;

        public CombatPacketProcessor(IMainThreadDispatcher dispatcher, Action<byte[]> sendRawAction)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _sendRawAction = sendRawAction ?? throw new ArgumentNullException(nameof(sendRawAction));
        }

        public void ParseCombatDamage(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 7) return;
            uint dmgTarget = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort dmgAmt = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            _dispatcher.Enqueue(() => OnDamageDealt?.Invoke(new CombatDamageDto(new Serial(dmgTarget), dmgAmt)));
        }

        public void ParseCombatSwing(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 10) return;
            uint attacker = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(2, 4));
            uint defender = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(6, 4));
            _dispatcher.Enqueue(() => OnCombatSwing?.Invoke(new Serial(attacker), new Serial(defender)));
        }

        public void ParseMobileAnimation(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 7) return;
            uint animMob = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            short actionId = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(5, 2));
            _dispatcher.Enqueue(() => OnMobileAnimation?.Invoke(new Serial(animMob), actionId));
        }

        public void ParseWarMode(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            bool war = packet[1] != 0;
            IsWarMode = war;
            _dispatcher.Enqueue(() => OnWarModeChanged?.Invoke(war));
        }

        public void ParseTargetCursor(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 7) return;
            bool allowGround = packet[1] != 0;
            uint targetId = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(2, 4));
            byte flags = packet[6];
            _dispatcher.Enqueue(() => OnTargetCursorRequested?.Invoke(new TargetCursorRequestDto(targetId, allowGround, flags)));
        }

        public void RequestWarMode(bool warMode)
        {
            IsWarMode = warMode;
            _dispatcher.Enqueue(() => OnWarModeChanged?.Invoke(warMode));
            byte[] packet = new byte[5] { 0x72, (byte)(warMode ? 1 : 0), 0x00, 0x32, 0x00 };
            _sendRawAction(packet);
        }

        public void RequestAttack(Serial targetMobile)
        {
            if (!IsWarMode) RequestWarMode(true);
            byte[] packet = new byte[5];
            packet[0] = 0x05;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(1, 4), targetMobile.Value);
            _sendRawAction(packet);
        }

        public void SendTargetResponseGround(uint targetId, Point3D position, ushort staticGraphic = 0)
        {
            byte[] packet = new byte[19];
            packet[0] = 0x6C;
            packet[1] = 1;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(2, 4), targetId);
            BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(11, 2), position.X);
            BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(13, 2), position.Y);
            packet[16] = (byte)position.Z;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(17, 2), staticGraphic);
            _sendRawAction(packet);
        }

        public void SendTargetResponseEntity(uint targetId, Serial entitySerial, Point3D position, ushort graphicId = 0)
        {
            byte[] packet = new byte[19];
            packet[0] = 0x6C;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(2, 4), targetId);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(7, 4), entitySerial.Value);
            BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(11, 2), position.X);
            BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(13, 2), position.Y);
            packet[16] = (byte)position.Z;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(17, 2), graphicId);
            _sendRawAction(packet);
        }
    }
}
