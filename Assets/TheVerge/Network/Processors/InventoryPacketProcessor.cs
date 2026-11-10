using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using TheVerge.Core;

namespace TheVerge.Network.Processors
{
    public sealed class InventoryPacketProcessor
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly Action<byte[]> _sendRawAction;

        public Serial PlayerBackpackSerial { get; set; } = new Serial(0x40000100);

        public event Action<Serial, ushort> OnContainerOpened;
        public event Action<Serial> OnContainerClosed;
        public event Action<Serial, IReadOnlyList<ItemStateDto>> OnContainerContentReceived;
        public event Action<ItemStateDto> OnItemAddedToContainer;
        public event Action<Serial, Serial> OnItemRemovedFromContainer;
        public event Action<Serial, ItemStateDto> OnItemEquipped;
        public event Action<Serial, Layer> OnItemUnequipped;
        public event Action<byte> OnLiftRejected;

        public InventoryPacketProcessor(IMainThreadDispatcher dispatcher, Action<byte[]> sendRawAction)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _sendRawAction = sendRawAction ?? throw new ArgumentNullException(nameof(sendRawAction));
        }

        public void NotifyObjectDeleted(Serial deletedSerial)
        {
            _dispatcher.Enqueue(() =>
            {
                OnItemRemovedFromContainer?.Invoke(PlayerBackpackSerial, deletedSerial);
            });
        }

        public void ParseOpenContainer(ReadOnlySpan<byte> packet)
        {
            uint containerSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort gumpId = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            if (gumpId == 0x003C)
            {
                PlayerBackpackSerial = new Serial(containerSerial);
            }
            _dispatcher.Enqueue(() => OnContainerOpened?.Invoke(new Serial(containerSerial), gumpId));
        }

        public void ParseContainerContents(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 5) return;
            ushort itemCount = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(3, 2));
            var list = new List<ItemStateDto>(itemCount);
            uint containerSerial = 0;

            int stride = (itemCount > 0 && (packet.Length - 5) / itemCount >= 20) ? 20 : 19;
            int containerOffset = stride == 20 ? 14 : 13;
            int hueOffset = stride == 20 ? 18 : 17;

            int itemOffset = 5;
            for (int i = 0; i < itemCount && itemOffset + stride <= packet.Length; i++)
            {
                uint s = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(itemOffset, 4));
                ushort g = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(itemOffset + 4, 2));
                ushort amt = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(itemOffset + 7, 2));
                containerSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(itemOffset + containerOffset, 4));
                ushort hue = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(itemOffset + hueOffset, 2));

                list.Add(new ItemStateDto(new Serial(s), g, hue, amt, Point3D.Zero, new Serial(containerSerial), Layer.Invalid));
                itemOffset += stride;
            }

            if (containerSerial != 0)
            {
                _dispatcher.Enqueue(() => OnContainerContentReceived?.Invoke(new Serial(containerSerial), list));
            }
        }

        public void ParseAddItem(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 19) return;
            uint s = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort g = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            ushort amt = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(8, 2));
            uint c = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(15, 4));

            var item = new ItemStateDto(new Serial(s), g, 0, amt, Point3D.Zero, new Serial(c), Layer.Invalid);
            _dispatcher.Enqueue(() => OnItemAddedToContainer?.Invoke(item));
        }

        public void ParseEquipItem(ReadOnlySpan<byte> packet, Serial localPlayerSerial)
        {
            if (packet.Length < 14) return;
            uint eqItemSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort eqGraphic = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(5, 2));
            var eqLayer = (Layer)packet[8];
            uint eqMobileSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(9, 4));

            if (eqMobileSerial == localPlayerSerial.Value && eqLayer == Layer.Backpack)
            {
                PlayerBackpackSerial = new Serial(eqItemSerial);
                UseItem(PlayerBackpackSerial);
            }
            var equippedDto = new ItemStateDto(new Serial(eqItemSerial), eqGraphic, 0, 1, Point3D.Zero, new Serial(eqMobileSerial), eqLayer);
            _dispatcher.Enqueue(() => OnItemEquipped?.Invoke(new Serial(eqMobileSerial), equippedDto));
        }

        public void ParseLiftReject(ReadOnlySpan<byte> packet)
        {
            byte reason = packet[1];
            _dispatcher.Enqueue(() => OnLiftRejected?.Invoke(reason));
        }

        public void ParseGeneralInfo(ReadOnlySpan<byte> packet)
        {
            if (packet.Length >= 9)
            {
                ushort subCmd = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(3, 2));
                if (subCmd == 0x16)
                {
                    uint closedSerial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(5, 4));
                    _dispatcher.Enqueue(() => OnContainerClosed?.Invoke(new Serial(closedSerial)));
                }
            }
        }

        public void UseItem(Serial itemSerial)
        {
            byte[] packet = new byte[5];
            packet[0] = 0x06;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(1, 4), itemSerial.Value);
            _sendRawAction(packet);
        }

        public void LiftItem(Serial itemSerial, ushort amount = 1)
        {
            byte[] packet = new byte[7];
            packet[0] = 0x07;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(1, 4), itemSerial.Value);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(5, 2), amount);
            _sendRawAction(packet);
        }

        public void DropToWorld(Serial itemSerial, Point3D targetPosition)
        {
            byte[] packet = new byte[15];
            packet[0] = 0x08;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(1, 4), itemSerial.Value);
            BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(5, 2), targetPosition.X);
            BinaryPrimitives.WriteInt16BigEndian(packet.AsSpan(7, 2), targetPosition.Y);
            packet[9] = (byte)targetPosition.Z;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(11, 4), 0xFFFFFFFF);
            _sendRawAction(packet);
        }

        public void DropToContainer(Serial itemSerial, Serial targetContainerSerial)
        {
            byte[] packet = new byte[15];
            packet[0] = 0x08;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(1, 4), itemSerial.Value);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(11, 4), targetContainerSerial.Value);
            _sendRawAction(packet);
        }

        public void EquipItem(Serial itemSerial, Layer targetLayer, Serial playerSerial)
        {
            byte[] packet = new byte[10];
            packet[0] = 0x13;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(1, 4), itemSerial.Value);
            packet[5] = (byte)targetLayer;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(6, 4), playerSerial.Value);
            _sendRawAction(packet);
        }

        public void CloseContainer(Serial containerSerial)
        {
            _dispatcher.Enqueue(() => OnContainerClosed?.Invoke(containerSerial));
        }
    }
}
