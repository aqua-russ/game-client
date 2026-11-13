using System;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.Targeting
{
    /// <summary>
    /// Прицел — базовый ответ (PLAN Стадия 2, п. 10а; быстрые 0xBF и вид курсора — Стадия 4). Сервер открывает курсор 0x6C
    /// (OutgoingTargetPackets.cs, SendTargetReq: «можно ли клетку», номер, флаги) и закрывает его тем же пакетом с флагом
    /// <see cref="FlagCancel"/> (SendCancelTarget). Ответ принимается только с номером открытого курсора, поэтому без
    /// курсора клиент ничего не шлёт; новый курсор заменяет старый. После ответа курсор закрыт.
    /// </summary>
    public sealed class TargetCursor
    {
        public const byte FlagHarmful = 1;
        public const byte FlagBeneficial = 2;

        /// <summary>Флаг 3 от сервера — курсор закрыт (SendCancelTarget).</summary>
        public const byte FlagCancel = 3;

        private readonly ITargetSender _sender;

        public TargetCursor(ITargetSender sender)
        {
            _sender = sender;
        }

        public event Action? Opened;

        /// <summary>Курсор закрыт: ответом клиента или отменой сервера.</summary>
        public event Action? Closed;

        public bool IsActive { get; private set; }

        public int CursorId { get; private set; }

        /// <summary>Можно ли выбрать клетку, а не только объект (AllowGround сервера).</summary>
        public bool AllowsLocation { get; private set; }

        /// <summary>0 — нейтральный, <see cref="FlagHarmful"/> — вред, <see cref="FlagBeneficial"/> — польза.</summary>
        public byte Flags { get; private set; }

        /// <summary>0x6C от сервера: открыть курсор или, с флагом <see cref="FlagCancel"/>, закрыть.</summary>
        public void OnRequest(bool allowsLocation, int cursorId, byte flags)
        {
            if (flags == FlagCancel)
            {
                Close();
                return;
            }

            IsActive = true;
            CursorId = cursorId;
            AllowsLocation = allowsLocation;
            Flags = flags;
            Opened?.Invoke();
        }

        public bool TargetObject(Serial serial)
        {
            if (!IsActive || (!serial.IsMobile && !serial.IsItem))
            {
                return false;
            }

            return Answer(new TargetResponse(TargetKind.Object, CursorId, Flags, serial, Point3D.Zero, 0));
        }

        /// <summary>Выбрать клетку: <paramref name="graphic"/> — номер статики в ней, 0 — земля.</summary>
        public bool TargetLocation(Point3D position, ushort graphic)
        {
            if (!IsActive || !AllowsLocation)
            {
                return false;
            }

            return Answer(new TargetResponse(TargetKind.Location, CursorId, Flags, Serial.Zero, position, graphic));
        }

        /// <summary>Отказаться от выбора (Esc).</summary>
        public bool Cancel()
        {
            if (!IsActive)
            {
                return false;
            }

            return Answer(new TargetResponse(TargetKind.Object, CursorId, Flags, Serial.Zero, new Point3D(-1, -1, 0), 0));
        }

        private bool Answer(in TargetResponse response)
        {
            _sender.SendTarget(response);
            Close();
            return true;
        }

        private void Close()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            Closed?.Invoke();
        }
    }
}
