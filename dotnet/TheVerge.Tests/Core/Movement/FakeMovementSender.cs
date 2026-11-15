using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>Порт отправки шагов в тестах: запоминает, что ушло бы серверу (список с запасом — без новой памяти).</summary>
    internal sealed class FakeMovementSender : IMovementSender
    {
        public List<Sent> Steps { get; } = new List<Sent>(64);

        public Sent Last => Steps[Steps.Count - 1];

        /// <summary>Сколько раз ушёл запрос ресинхронизации.</summary>
        public int Resyncs { get; private set; }

        public void SendStep(Direction direction, bool run, byte sequence) => Steps.Add(new Sent(direction, run, sequence));

        public void SendResync() => Resyncs++;

        internal readonly struct Sent
        {
            public Sent(Direction direction, bool run, byte sequence)
            {
                Direction = direction;
                Run = run;
                Sequence = sequence;
            }

            public Direction Direction { get; }
            public bool Run { get; }
            public byte Sequence { get; }

            public override string ToString() => Direction + (Run ? " бег" : " шаг") + " #" + Sequence;
        }
    }
}
