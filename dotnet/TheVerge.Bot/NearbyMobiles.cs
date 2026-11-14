using System;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>Ближайший к своему персонажу другой моб с телом (fight:, deathof:, target:mob:).</summary>
    internal static class NearbyMobiles
    {
        public static Mobile? Nearest(EntityStore entities, ushort body)
        {
            var player = entities.Player;
            if (player == null)
            {
                return null;
            }

            Mobile? nearest = null;
            foreach (var mobile in entities.Mobiles)
            {
                if (mobile.Serial != player.Serial && mobile.Body == body &&
                    (nearest == null || Distance(player, mobile) < Distance(player, nearest)))
                {
                    nearest = mobile;
                }
            }

            return nearest;
        }

        private static int Distance(Mobile a, Mobile b) =>
            Math.Max(Math.Abs(a.Position.X - b.Position.X), Math.Abs(a.Position.Y - b.Position.Y));
    }
}
