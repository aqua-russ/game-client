using System.Text.Json;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// Сверка физики движения клиента <see cref="MovementCheck"/> с дампом сервера [DumpMove: для каждого кейса
    /// [x, y, z, dir, pOk, pZ, gOk, gZ, hOk, hZ, sOk, sZ] проверяются режимы player (выносливость не полная — моб
    /// загораживает), gm, ghost и shove (игрок с полной выносливостью проходит сквозь моба, Mobile.CheckShove).
    /// Формат — Custom/TheVerge/Parity/MoveDump.cs.
    /// </summary>
    public static class MoveDumpComparison
    {
        public const int CaseLength = 12;

        private static readonly string[] ModeNames = { "player", "gm", "ghost", "shove" };

        public static ParityReport Compare(JsonElement server, MovementCheck checker, uint playerSerial = 0)
        {
            var report = new ParityReport();
            var modes = new[]
            {
                MoverState.Player(playerSerial, canShoveMobiles: false),
                MoverState.GameMaster(playerSerial),
                MoverState.Ghost(playerSerial),
                MoverState.Player(playerSerial, canShoveMobiles: true),
            };
            var seed = server.GetProperty("seed").GetInt32();
            var count = 0;

            foreach (var entry in server.GetProperty("cases").EnumerateArray())
            {
                if (entry.GetArrayLength() != CaseLength)
                {
                    report.Differ($"в дампе сервера {entry.GetArrayLength()} чисел на проверку вместо {CaseLength}: " +
                                  "сервер собран до режима shove — пересоберите сервер (dotnet build -c Release в server/ModernUO).");
                    return report;
                }

                for (int modeIndex = 0; modeIndex < modes.Length; modeIndex++)
                {
                    CheckMode(checker, entry, modes[modeIndex], modeIndex, report);
                }

                count++;
            }

            report.Summary.Add($"Физика движения: сверено проверок {count} (режимы player/gm/ghost/shove, seed {seed}).");
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
