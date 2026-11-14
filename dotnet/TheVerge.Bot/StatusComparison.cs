using System.Text.Json;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// Сверка состояния мобов бота с дампом сервера [DumpMobile (PLAN Стадия 4, п. 4; формат — Custom/TheVerge/Parity/
    /// MobileStatusDump.cs сервера). Свой персонаж — все числа полного статуса (0x11 версии 3) и замки статов; чужой —
    /// то, что сервер шлёт чужим: имя, здоровье долей от 100 (OutgoingMobilePackets.WriteAttribute, normalize), яд и
    /// жёлтая полоска (0x17). Каждое расхождение называет моба и поле.
    /// </summary>
    public static class StatusComparison
    {
        public static ParityReport Compare(JsonElement server, EntityStore store)
        {
            var report = new ParityReport();
            var count = 0;
            foreach (var entry in server.GetProperty("mobiles").EnumerateArray())
            {
                count++;
                var text = entry.GetProperty("serial").GetString() ?? "0x0";
                var mobile = store.FindMobile(new Serial(uint.Parse(text.Substring(2), System.Globalization.NumberStyles.HexNumber)));
                var self = entry.GetProperty("self").GetBoolean();
                var where = self ? "свой персонаж: " : "моб " + text + ": ";
                if (mobile == null)
                {
                    report.Differ(where + "у клиента нет");
                }
                else if (mobile.Status.Version < 0)
                {
                    report.Differ(where + "у клиента нет статуса (сервер не ответил на запрос?)");
                }
                else
                {
                    CompareCommon(entry, mobile.Status, self, where, report);
                    if (self)
                    {
                        CompareSelf(entry, mobile.Status, where, report);
                    }
                }
            }

            report.Summary.Add("Состояние: сверено мобов " + count + ".");
            return report;
        }

        private static void CompareCommon(JsonElement entry, MobileStatus status, bool self, string where, ParityReport report)
        {
            Text(report, where + "имя", entry.GetProperty("name").GetString() ?? string.Empty, status.Name);
            int hits = Int(entry, "hits"), hitsMax = Int(entry, "hitsMax");
            // Чужим сервер шлёт долю: текущее × 100 / максимум и 100 (при максимуме 0 — как есть).
            var expected = self || hitsMax == 0 ? new Vital(hits, hitsMax) : new Vital(hits * 100 / hitsMax, 100);
            VitalField(report, where + "здоровье", expected, status.Hits);
            Number(report, where + "яд", Int(entry, "poison"), status.PoisonLevel);
            Number(report, where + "жёлтая полоска", entry.GetProperty("yellow").GetBoolean() ? 1 : 0, status.YellowHealthbar ? 1 : 0);
        }

        private static void CompareSelf(JsonElement entry, MobileStatus status, string where, ParityReport report)
        {
            var a = status.Attributes;
            VitalField(report, where + "мана", new Vital(Int(entry, "mana"), Int(entry, "manaMax")), status.Mana);
            VitalField(report, where + "выносливость", new Vital(Int(entry, "stam"), Int(entry, "stamMax")), status.Stam);
            Number(report, where + "пол", entry.GetProperty("female").GetBoolean() ? 1 : 0, status.Female ? 1 : 0);
            Number(report, where + "сила", Int(entry, "str"), a.Str);
            Number(report, where + "ловкость", Int(entry, "dex"), a.Dex);
            Number(report, where + "интеллект", Int(entry, "int"), a.Int);
            Number(report, where + "золото", Int(entry, "gold"), a.Gold);
            Number(report, where + "броня", Int(entry, "armor"), a.Armor);
            Number(report, where + "вес", Int(entry, "weight"), a.Weight);
            Number(report, where + "предел статов", Int(entry, "statCap"), a.StatCap);
            Number(report, where + "питомцы", Int(entry, "followers"), status.Followers);
            Number(report, where + "питомцы, максимум", Int(entry, "followersMax"), status.FollowersMax);
            Number(report, where + "замок силы", Int(entry, "strLock"), (int)status.Locks.Str);
            Number(report, where + "замок ловкости", Int(entry, "dexLock"), (int)status.Locks.Dex);
            Number(report, where + "замок интеллекта", Int(entry, "intLock"), (int)status.Locks.Int);
        }

        private static int Int(JsonElement entry, string name) => entry.GetProperty(name).GetInt32();

        private static void VitalField(ParityReport report, string what, Vital server, Vital client)
        {
            if (server.Current != client.Current || server.Maximum != client.Maximum)
            {
                report.Differ(what + " — сервер " + server + ", клиент " + client);
            }
        }

        private static void Number(ParityReport report, string what, int server, int client)
        {
            if (server != client)
            {
                report.Differ(what + " — сервер " + server + ", клиент " + client);
            }
        }

        private static void Text(ParityReport report, string what, string server, string client)
        {
            if (server != client)
            {
                report.Differ(what + " — сервер «" + server + "», клиент «" + client + "»");
            }
        }
    }
}
