using System;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Время шага — одно место для темпа и для картинки (PLAN Стадия 3, п. 7; ADR 0020 пп. 4–6). Задержка между шагами у
    /// сервера — таблица «пешком или верхом × шаг или бег» (Projects/UOContent/Mobiles/PlayerMobile.cs,
    /// ComputeMovementSpeed; значения — <see cref="ServerLimits"/>). Картинка показывает шаг столько же; отставшая на
    /// несколько клеток догоняет быстрее, но не больше чем в <see cref="ClientLimits.StepCatchUpMaxSpeedup"/> раз; рывок
    /// больше <see cref="ClientLimits.StepSnapGapCells"/> клеток — перескок. Чужие мобы — <see cref="OthersMotion"/>; Unity
    /// только читает (задача 3.9).
    /// </summary>
    public static class StepTiming
    {
        /// <summary>Задержка до следующего шага у сервера, мс.</summary>
        public static int StepDelayMs(bool mounted, bool running)
        {
            if (mounted)
            {
                return running ? ServerLimits.RunMountDelayMs : ServerLimits.WalkMountDelayMs;
            }

            return running ? ServerLimits.RunFootDelayMs : ServerLimits.WalkFootDelayMs;
        }

        /// <summary>
        /// Сколько показывать шаг, мс, когда картинка отстала на <paramref name="cellsBehind"/> клеток (считая эту; 0 и 1 —
        /// не отстала).
        /// </summary>
        public static int DisplayMs(bool mounted, bool running, int cellsBehind)
        {
            int speedup = Math.Min(Math.Max(cellsBehind, 1), ClientLimits.StepCatchUpMaxSpeedup);
            return StepDelayMs(mounted, running) / speedup;
        }

        /// <summary>
        /// Перескочить, а не ехать: новая клетка от сервера дальше <see cref="ClientLimits.StepSnapGapCells"/> клеток от
        /// прежней (рывок одним пакетом). Пачка соседних шагов после лага — не рывок: её картинка догоняет.
        /// </summary>
        public static bool ShouldSnap(Point3D previous, Point3D next)
        {
            int gap = Math.Max(Math.Abs(next.X - previous.X), Math.Abs(next.Y - previous.Y));
            return gap > ClientLimits.StepSnapGapCells;
        }
    }
}
