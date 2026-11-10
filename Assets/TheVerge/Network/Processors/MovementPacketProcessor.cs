using System;
using System.Buffers.Binary;
using TheVerge.Core;

namespace TheVerge.Network.Processors
{
    public sealed class MovementPacketProcessor
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly Action<byte[]> _sendRawAction;
        private readonly MovementSequenceQueue _sequenceQueue = new MovementSequenceQueue();

        public Point3D CurrentPosition { get; set; } = new Point3D(2545, 502, 0);
        public Direction CurrentDirection { get; set; } = Direction.South;
        public Point3D PendingPosition { get; set; } = new Point3D(2545, 502, 0);
        public Direction PendingDirection { get; set; } = Direction.South;
        public byte LastSequenceKey { get; private set; }

        public event Action<MoveResultDto> OnMoveAccepted;
        public event Action<MoveResultDto> OnMoveRejected;

        public MovementPacketProcessor(IMainThreadDispatcher dispatcher, Action<byte[]> sendRawAction)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _sendRawAction = sendRawAction ?? throw new ArgumentNullException(nameof(sendRawAction));
        }

        public void Reset()
        {
            _sequenceQueue.Reset();
        }

        public bool TryRequestMove(Direction direction, bool isRunning, bool isConnected, bool isLiveServer)
        {
            if (!isConnected) return false;

            Direction cleanDir = direction & Direction.FacingMask;
            if (isLiveServer && PendingDirection != cleanDir)
            {
                if (!_sequenceQueue.TryEnqueueStep(cleanDir, PendingPosition, out byte turnSeq))
                {
                    return false;
                }
                PendingDirection = cleanDir;
                LastSequenceKey = turnSeq;
                SendMovePacket(cleanDir, isRunning, turnSeq);
            }

            var targetPos = PendingPosition.GetStepPoint(cleanDir);
            if (MinocMapSliceData.IsPassable(targetPos.X, targetPos.Y, out sbyte tileZ))
            {
                targetPos = new Point3D(targetPos.X, targetPos.Y, tileZ);
            }

            if (!_sequenceQueue.TryEnqueueStep(cleanDir, targetPos, out byte seq))
            {
                return false;
            }

            PendingDirection = cleanDir;
            PendingPosition = targetPos;
            LastSequenceKey = seq;
            SendMovePacket(cleanDir, isRunning, seq);
            return true;
        }

        public void RequestTurn(Direction direction, bool isConnected, bool isLiveServer)
        {
            if (!isConnected) return;

            Direction cleanDir = direction & Direction.FacingMask;
            if (PendingDirection == cleanDir) return;

            if (isLiveServer)
            {
                if (!_sequenceQueue.TryEnqueueStep(cleanDir, PendingPosition, out byte turnSeq))
                {
                    return;
                }
                PendingDirection = cleanDir;
                LastSequenceKey = turnSeq;
                SendMovePacket(cleanDir, false, turnSeq);
            }
            else
            {
                PendingDirection = cleanDir;
                CurrentDirection = cleanDir;
            }
        }

        public void ParseMoveAck(ReadOnlySpan<byte> packet)
        {
            byte ackSeq = packet[1];
            LastSequenceKey = ackSeq;
            _dispatcher.Enqueue(() =>
            {
                if (_sequenceQueue.ConfirmStep(ackSeq, out var step))
                {
                    CurrentPosition = step.TargetPosition;
                    CurrentDirection = step.Direction;
                    OnMoveAccepted?.Invoke(new MoveResultDto(ackSeq, CurrentPosition, CurrentDirection));
                }
            });
        }

        public void ParseMoveReject(ReadOnlySpan<byte> packet)
        {
            byte rejSeq = packet[1];
            short rejX = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(2, 2));
            short rejY = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(4, 2));
            var rejDir = (Direction)(packet[6] & 0x07);
            sbyte rejZ = (sbyte)packet[7];
            var correctedPos = new Point3D(rejX, rejY, rejZ);

            _dispatcher.Enqueue(() =>
            {
                _sequenceQueue.Reset();
                CurrentPosition = correctedPos;
                CurrentDirection = rejDir;
                PendingPosition = correctedPos;
                PendingDirection = rejDir;
                OnMoveRejected?.Invoke(new MoveResultDto(rejSeq, correctedPos, rejDir));
            });
        }

        public void SimulateMoveReject()
        {
            _dispatcher.Enqueue(() =>
            {
                _sequenceQueue.Reset();
                OnMoveRejected?.Invoke(new MoveResultDto(0, CurrentPosition, CurrentDirection));
            });
        }

        private void SendMovePacket(Direction cleanDir, bool isRunning, byte seq)
        {
            byte[] packet = new byte[7];
            packet[0] = 0x02;
            packet[1] = (byte)(cleanDir | (isRunning ? Direction.Running : 0));
            packet[2] = seq;
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(3, 4), 0);
            _sendRawAction(packet);
        }
    }
}
