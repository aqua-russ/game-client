using TheVerge.Core.Primitives;

namespace TheVerge.Core.Targeting
{
    /// <summary>
    /// Ответ на курсор сервера: объект (серийник) или клетка (x, y, z и номер статики; 0 — земля). Отмена — x = y = −1 и
    /// серийник 0 (IncomingTargetingPackets.cs, TargetResponse).
    /// </summary>
    public readonly struct TargetResponse
    {
        public TargetResponse(TargetKind kind, int cursorId, byte flags, Serial serial, Point3D position, ushort graphic)
        {
            Kind = kind;
            CursorId = cursorId;
            Flags = flags;
            Serial = serial;
            Position = position;
            Graphic = graphic;
        }

        public TargetKind Kind { get; }

        public int CursorId { get; }

        public byte Flags { get; }

        public Serial Serial { get; }

        public Point3D Position { get; }

        /// <summary>Номер статики в клетке; 0 — клетка земли.</summary>
        public ushort Graphic { get; }
    }
}
