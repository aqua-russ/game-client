using System.Text.Json;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// Сверка физики движения клиента <see cref="MovementCheck"/> с дампом сервера [DumpMove: для каждого кейса
    /// [x, y, z, dir, pOk, pZ, gOk, gZ, hOk, hZ] проверяются режимы player, gm и ghost.
    /// </summary>
    public static class MoveDumpComparison
    {
        private static readonly string[] ModeNames = { "player", "gm", "ghost" };

        public static ParityReport Compare(JsonElement server, MovementCheck checker, uint playerSerial = 0)
        {
            var report = new ParityReport();
            var modes = new[]
            {
                MoverState.Player(playerSerial, canShoveMobiles: false),
                MoverState.GameMaster(playerSerial),
                MoverState.Ghost(playerSerial),
            };
            var seed = server.GetProperty("seed").GetInt32();
            var count = 0;

            foreach (var entry in server.GetProperty("cases").EnumerateArray())
            {
                for (int modeIndex = 0; modeIndex < modes.Length; modeIndex++)
                {
                    CheckMode(checker, entry, modes[modeIndex], modeIndex, report);
                }

                count++;
            }

            report.Summary.Add($"Физика движения: сверено проверок {count} (режимы player/gm/ghost, seed {seed}).");
            return report;
        }

        private static void CheckMode(MovementCheck checker, JsonElement entry, MoverState mover,
            int modeIndex, ParityReport report)
        {
            var loc = new Point3D((short)entry[0].GetInt32(), (short)entry[1].GetInt32(), (sbyte)entry[2].GetInt32());
            var dir = (Direction)entry[3].GetInt32();
            bool serverOk = entry[4 + (modeIndex * 2)].GetInt32() != 0;
            int serverZ = entry[5 + (modeIndex * 2)].GetInt32();
            bool clientOk = checker.CheckMovement(loc, dir, mover, out int clientZ);
            if (clientOk != serverOk || clientZ != serverZ)
            {
                string mode = ModeNames[modeIndex];
                report.Differ($"({loc.X}, {loc.Y}, {loc.Z}) {dir} [{mode}]: сервер ok={serverOk}, newZ={serverZ}; клиент ok={clientOk}, newZ={clientZ}");
            }
        }
    }
}
