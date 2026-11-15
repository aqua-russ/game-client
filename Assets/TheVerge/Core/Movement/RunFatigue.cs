using TheVerge.Core.Session;
using TheVerge.Core.World;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Бег пешком и усталость — правило сервера Projects/UOContent/Misc/StaminaSystem.cs, ProcessPlayerMovement: если
    /// выносливость ниже <see cref="ServerLimits.RunExtraStaminaLossBelowPercent"/>% максимума, шаг бега сначала отнимает
    /// ещё единицу, и при выносливости 0 бег запрещён, а шаг — нет. Верхом действует усталость коня (ProcessMountMovement),
    /// призрака сервер не проверяет (EventSink_Movement: !Alive). Клиент в таком случае идёт шагом, а не бежит, как
    /// классический клиент (ClassicUO PlayerMobile.Walk: выносливость не больше 1 — без бега).
    /// </summary>
    public static class RunFatigue
    {
        private const int Percent = 100;

        /// <summary>Пустит ли сервер героя пешком бегом при этой выносливости; неизвестная (максимум 0) — пустит.</summary>
        public static bool AllowsRun(Vital stamina)
        {
            if (stamina.Maximum <= 0)
            {
                return true;
            }

            int left = stamina.Current;
            if (left * Percent < stamina.Maximum * ServerLimits.RunExtraStaminaLossBelowPercent)
            {
                left--;
            }

            return left > 0;
        }
    }
}
