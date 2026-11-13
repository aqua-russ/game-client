using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Targeting;

namespace TheVerge.Tests.Core.Targeting
{
    /// <summary>
    /// Прицел (PLAN Стадия 2, п. 10а). Сервер открывает курсор 0x6C (OutgoingTargetPackets.cs, SendTargetReq: AllowGround,
    /// TargetID, Flags) и закрывает его тем же пакетом с флагом 3 (SendCancelTarget). Ответ сервер принимает только с
    /// номером открытого курсора; отмена — x = y = −1 и серийник 0 (IncomingTargetingPackets.cs, TargetResponse).
    /// </summary>
    public sealed class TargetCursorTests
    {
        private RecordingTargetSender _sender = null!;
        private TargetCursor _cursor = null!;

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingTargetSender();
            _cursor = new TargetCursor(_sender);
        }

        [Test]
        public void Request_OpensCursor()
        {
            var opened = 0;
            _cursor.Opened += () => opened++;

            _cursor.OnRequest(true, 0x01020304, TargetCursor.FlagHarmful);

            Assert.That(_cursor.IsActive, Is.True);
            Assert.That(_cursor.CursorId, Is.EqualTo(0x01020304));
            Assert.That(_cursor.AllowsLocation, Is.True);
            Assert.That(_cursor.Flags, Is.EqualTo(TargetCursor.FlagHarmful));
            Assert.That(opened, Is.EqualTo(1));
        }

        [Test]
        public void ServerCancel_ClosesCursor()
        {
            var closed = 0;
            _cursor.Closed += () => closed++;
            _cursor.OnRequest(true, 7, 0);

            _cursor.OnRequest(false, 0, TargetCursor.FlagCancel);

            Assert.That(_cursor.IsActive, Is.False);
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(_sender.Responses, Is.Empty);
        }

        [Test]
        public void TargetObject_SendsSerialAndCloses()
        {
            _cursor.OnRequest(false, 7, TargetCursor.FlagBeneficial);

            Assert.That(_cursor.TargetObject(new Serial(0x40000500)), Is.True);

            var r = _sender.Responses[0];
            Assert.That(r.Kind, Is.EqualTo(TargetKind.Object));
            Assert.That(r.CursorId, Is.EqualTo(7));
            Assert.That(r.Flags, Is.EqualTo(TargetCursor.FlagBeneficial));
            Assert.That(r.Serial.Value, Is.EqualTo(0x40000500u));
            Assert.That(_cursor.IsActive, Is.False);
        }

        [Test]
        public void TargetLocation_SendsCellAndGraphic()
        {
            _cursor.OnRequest(true, 9, 0);

            Assert.That(_cursor.TargetLocation(new Point3D(1500, 1600, -5), 0), Is.True);

            var r = _sender.Responses[0];
            Assert.That(r.Kind, Is.EqualTo(TargetKind.Location));
            Assert.That(r.Position, Is.EqualTo(new Point3D(1500, 1600, -5)));
            Assert.That(r.Graphic, Is.EqualTo((ushort)0));
            Assert.That(r.Serial.IsValid, Is.False);
        }

        [Test]
        public void Cancel_SendsMinusOneAndCloses()
        {
            _cursor.OnRequest(true, 9, 0);

            Assert.That(_cursor.Cancel(), Is.True);

            var r = _sender.Responses[0];
            Assert.That(r.CursorId, Is.EqualTo(9));
            Assert.That(r.Position.X, Is.EqualTo(-1));
            Assert.That(r.Position.Y, Is.EqualTo(-1));
            Assert.That(r.Serial.IsValid, Is.False);
            Assert.That(_cursor.IsActive, Is.False);
        }

        [Test]
        public void NoCursor_NothingSent()
        {
            Assert.That(_cursor.TargetObject(new Serial(0x40000500)), Is.False);
            Assert.That(_cursor.TargetLocation(new Point3D(1, 1, 0), 0), Is.False);
            Assert.That(_cursor.Cancel(), Is.False);
            Assert.That(_sender.Responses, Is.Empty);
        }

        [Test]
        public void LocationNotAllowed_NothingSent_CursorStays()
        {
            _cursor.OnRequest(false, 9, 0);

            Assert.That(_cursor.TargetLocation(new Point3D(1, 1, 0), 0), Is.False);

            Assert.That(_sender.Responses, Is.Empty);
            Assert.That(_cursor.IsActive, Is.True);
        }

        [TestCase(0x00000000u)]
        [TestCase(0x80000000u)]
        public void TargetObject_InvalidSerial_NothingSent(uint serial)
        {
            _cursor.OnRequest(false, 9, 0);

            Assert.That(_cursor.TargetObject(new Serial(serial)), Is.False);
            Assert.That(_sender.Responses, Is.Empty);
        }

        [Test]
        public void NewRequest_ReplacesOld()
        {
            _cursor.OnRequest(false, 1, 0);
            _cursor.OnRequest(true, 2, 0);

            _cursor.TargetLocation(new Point3D(1, 1, 0), 0);

            Assert.That(_sender.Responses[0].CursorId, Is.EqualTo(2));
        }

        private sealed class RecordingTargetSender : ITargetSender
        {
            public List<TargetResponse> Responses { get; } = new List<TargetResponse>();

            public void SendTarget(in TargetResponse response) => Responses.Add(response);
        }
    }
}
