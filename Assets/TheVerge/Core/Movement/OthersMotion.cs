using System;
using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Чужие мобы (PLAN Стадия 3, п. 8; ADR 0020 пп. 4–6): у каждого — шаг показа (<see cref="ShownStep"/>). Позиция в мире
    /// дискретна (<see cref="EntityStore"/>), здесь — только когда и за сколько картинка едет в новую клетку; Unity читает
    /// (задача 3.9). Обработчики 0x77 и 0x78 сообщают сюда после того, как изменили мир; удаление и очистка мира — события
    /// <see cref="EntityStore"/>.
    /// <list type="bullet">
    /// <item>Шаг (0x77 с новой клеткой) — без буфера задержки: картинка едет с прихода пакета, если не занята прежним
    /// шагом, иначе сразу после него. Длительность — <see cref="StepTiming"/> по биту бега и коню (Layer.Mount); NPC сервер
    /// ставит бит бега под эту таблицу (Projects/UOContent/Mobiles/AI/BaseAI/AIMovement.cs, ShouldRun). Пачку шагов после
    /// лага картинка догоняет ускоренно. Шаги начинаются в <see cref="Update"/> — в том же тике, что и разбор пакетов,
    /// поэтому пачка из одного тика догоняется целиком.</item>
    /// <item>0x77 с той же клеткой — поворот, флаги, «злость» или повтор после 0x78 (Projects/Server/Mobiles/Mobile.cs: Move,
    /// ProcessDelta) — не шаг.</item>
    /// <item>Перескок: появление (<see cref="SnapReason.Appeared"/>), телепорт — 0x78 с новой клеткой (Mobile.SetLocation с
    /// isTeleport), рывок дальше <see cref="ClientLimits.StepSnapGapCells"/> клеток одним 0x77, отставание картинки больше
    /// <see cref="ClientLimits.StepBacklogSnapMs"/>. Смена карты — мир очищен, сервер присылает всех заново.</item>
    /// </list>
    /// Время — <see cref="IClock"/> (приход пакета) и <see cref="Update"/>; на шаг новой памяти нет.
    /// </summary>
    public sealed class OthersMotion : ITickHandler, IDisposable
    {
        private readonly EntityStore _world;
        private readonly IClock _clock;
        private readonly Dictionary<uint, ShownSteps> _mobiles = new Dictionary<uint, ShownSteps>();
        private readonly Stack<ShownSteps> _pool = new Stack<ShownSteps>();

        public OthersMotion(EntityStore world, IClock clock)
        {
            _world = world;
            _clock = clock;
            _world.Removed += OnRemoved;
            _world.Cleared += OnCleared;
        }

        /// <summary>Картинка начала шаг (сразу или из очереди).</summary>
        public event Action<Serial, ShownStep>? StepStarted;

        /// <summary>Картинка встала на клетку без езды.</summary>
        public event Action<Serial, MotionSnap>? Snapped;

        public int TrackedCount => _mobiles.Count;

        /// <summary>Сколько шагов показано ускоренно (с начала).</summary>
        public int CatchUpCount { get; private set; }

        /// <summary>Сколько было перескоков (с начала), не считая появлений.</summary>
        public int SnapCount { get; private set; }

        /// <summary>Текущий шаг показа моба; моб стоит — шаг на месте. false — моб не чужой или не в мире.</summary>
        public bool TryGetStep(Serial serial, out ShownStep step)
        {
            var found = _mobiles.TryGetValue(serial.Value, out var steps);
            step = found ? steps!.Current : default;
            return found;
        }

        /// <summary>Сколько пришедших шагов моба ждут показа.</summary>
        public int WaitingSteps(Serial serial) => _mobiles.TryGetValue(serial.Value, out var steps) ? steps.WaitingCount : 0;

        /// <summary>0x78: моб появился, телепортировался (новая клетка) или обновился на месте.</summary>
        public void OnIncoming(Serial serial)
        {
            var mobile = Other(serial);
            if (mobile == null)
            {
                return;
            }

            if (!_mobiles.TryGetValue(serial.Value, out var steps))
            {
                Appear(mobile);
            }
            else if (mobile.Position != steps.Target)
            {
                Snap(steps, mobile, SnapReason.Teleported);
            }
        }

        /// <summary>0x77: шаг (новая клетка), поворот или смена флагов (та же клетка).</summary>
        public void OnMoving(Serial serial)
        {
            var mobile = Other(serial);
            if (mobile == null)
            {
                return;
            }

            if (!_mobiles.TryGetValue(serial.Value, out var steps))
            {
                Appear(mobile);
                return;
            }

            var cell = mobile.Position;
            if (cell == steps.Target)
            {
                return;
            }

            bool sameSpot = cell.X == steps.Target.X && cell.Y == steps.Target.Y;
            if (sameSpot || StepTiming.ShouldSnap(steps.Target, cell))
            {
                Snap(steps, mobile, SnapReason.Jumped);
                return;
            }

            long now = _clock.NowMs;
            bool mounted = mobile.GetEquipped(Layer.Mount) != null;
            if (!steps.Enqueue(cell, mobile.Direction, now, mounted) || steps.BacklogMs(now) > ClientLimits.StepBacklogSnapMs)
            {
                Snap(steps, mobile, SnapReason.Backlog);
            }
        }

        /// <summary>Тик: начать шаги, чья очередь пришла.</summary>
        public void Update(long now)
        {
            foreach (var steps in _mobiles.Values)
            {
                while (steps.TryStartNext(now, out var step))
                {
                    if (step.IsCatchUp)
                    {
                        CatchUpCount++;
                    }

                    StepStarted?.Invoke(steps.Serial, step);
                }
            }
        }

        public void Dispose()
        {
            _world.Removed -= OnRemoved;
            _world.Cleared -= OnCleared;
        }

        private Mobile? Other(Serial serial) => serial == _world.PlayerSerial ? null : _world.FindMobile(serial);

        private void Appear(Mobile mobile)
        {
            var steps = _pool.Count > 0 ? _pool.Pop() : new ShownSteps();
            _mobiles.Add(mobile.Serial.Value, steps);
            long now = _clock.NowMs;
            steps.SnapTo(mobile.Serial, mobile.Position, mobile.Direction, now);
            Snapped?.Invoke(mobile.Serial, new MotionSnap(mobile.Position, SnapReason.Appeared, 0, now));
        }

        private void Snap(ShownSteps steps, Mobile mobile, SnapReason reason)
        {
            long now = _clock.NowMs;
            int dropped = steps.SnapTo(mobile.Serial, mobile.Position, mobile.Direction, now);
            SnapCount++;
            Snapped?.Invoke(mobile.Serial, new MotionSnap(mobile.Position, reason, dropped, now));
        }

        private void OnRemoved(Serial serial)
        {
            if (_mobiles.TryGetValue(serial.Value, out var steps))
            {
                _mobiles.Remove(serial.Value);
                _pool.Push(steps);
            }
        }

        private void OnCleared()
        {
            foreach (var steps in _mobiles.Values)
            {
                _pool.Push(steps);
            }

            _mobiles.Clear();
        }
    }
}
