using System;
using System.Collections.Generic;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// Чужие мобы у бота (PLAN Стадия 3, п. 8): показ шагов (<see cref="OthersMotion"/>, его тикает GameClient) и что бот
    /// по нему видел — <see cref="OtherStats"/> на каждого моба; перед выходом — строка «Чужой …» на каждого
    /// (<see cref="WriteTo"/>), её читает tools/check/others.sh.
    /// </summary>
    public sealed class BotOthers : IDisposable
    {
        private readonly EntityStore _entities;
        private readonly SortedDictionary<uint, OtherStats> _stats = new SortedDictionary<uint, OtherStats>();

        public BotOthers(OthersMotion motion, EntityStore entities)
        {
            Motion = motion;
            _entities = entities;
            Motion.StepStarted += OnStepStarted;
            Motion.Snapped += OnSnapped;
        }

        public OthersMotion Motion { get; }

        public void WriteTo(ILog log)
        {
            foreach (var pair in _stats)
            {
                log.Info(pair.Value.Describe(new Serial(pair.Key)));
            }
        }

        public void Dispose()
        {
            Motion.StepStarted -= OnStepStarted;
            Motion.Snapped -= OnSnapped;
            Motion.Dispose();
        }

        private void OnStepStarted(Serial serial, ShownStep step) => Stats(serial).Add(step);

        private void OnSnapped(Serial serial, MotionSnap snap) => Stats(serial).Add(snap);

        private OtherStats Stats(Serial serial)
        {
            if (!_stats.TryGetValue(serial.Value, out var stats))
            {
                stats = new OtherStats(_entities.FindMobile(serial)?.Body ?? 0);
                _stats.Add(serial.Value, stats);
            }

            return stats;
        }
    }
}
