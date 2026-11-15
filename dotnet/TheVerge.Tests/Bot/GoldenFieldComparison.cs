using System.Collections.Generic;
using TheVerge.Network.Framing;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Сравнитель полей пакетов клиента с эталоном ClassicUO (docs/REFERENCE-CLIENT.md §5, шаг 2; PLAN Стадия 1, п. 12).
    /// Каждый пакет бота сравнивается с первым пакетом эталона с тем же номером и подкомандой: длина и все байты, кроме
    /// закрытых маской (поле, которое законно отличается, — с причиной). Пакет, которого нет в эталоне, — расхождение,
    /// если он не в списке допустимых.
    /// </summary>
    internal sealed class GoldenFieldComparison
    {
        private readonly List<Mask> _masks = new List<Mask>();
        private readonly HashSet<int> _botOnly = new HashSet<int>();

        /// <summary>Байты [from, to] пакета <paramref name="id"/> (подкоманда, если есть) законно отличаются.</summary>
        public GoldenFieldComparison Allow(int id, int subCommand, int from, int to, string reason)
        {
            _masks.Add(new Mask(Key(id, subCommand), from, to, reason));
            return this;
        }

        /// <summary>Пакета нет у ClassicUO, но он законен (причина — в golden/allowed-diffs.md).</summary>
        public GoldenFieldComparison AllowBotOnly(int id, int subCommand)
        {
            _botOnly.Add(Key(id, subCommand));
            return this;
        }

        public List<string> Compare(IReadOnlyList<byte[]> bot, IReadOnlyList<byte[]> golden)
        {
            var problems = new List<string>();
            foreach (var packet in bot)
            {
                var key = KeyOf(packet);
                var reference = Find(golden, key);
                if (reference == null)
                {
                    if (!_botOnly.Contains(key))
                    {
                        problems.Add(Label(key) + ": у ClassicUO такого пакета нет.");
                    }

                    continue;
                }

                CompareBytes(key, packet, reference, problems);
            }

            return problems;
        }

        private void CompareBytes(int key, byte[] bot, byte[] reference, List<string> problems)
        {
            if (bot.Length != reference.Length)
            {
                problems.Add(Label(key) + ": длина " + bot.Length + ", у ClassicUO " + reference.Length + ".");
                return;
            }

            for (int i = 0; i < bot.Length; i++)
            {
                if (bot[i] != reference[i] && !IsMasked(key, i))
                {
                    problems.Add(Label(key) + ": байт " + i + " — 0x" + bot[i].ToString("X2") + ", у ClassicUO 0x" +
                                 reference[i].ToString("X2") + ".");
                }
            }
        }

        private bool IsMasked(int key, int index)
        {
            foreach (var mask in _masks)
            {
                if (mask.Key == key && index >= mask.From && index <= mask.To)
                {
                    return true;
                }
            }

            return false;
        }

        private static byte[]? Find(IReadOnlyList<byte[]> golden, int key)
        {
            foreach (var packet in golden)
            {
                if (KeyOf(packet) == key)
                {
                    return packet;
                }
            }

            return null;
        }

        private static int KeyOf(byte[] packet)
        {
            var layout = PacketTable.GetSubCommandLayout(PacketDirection.ClientToServer, packet[0]);
            return Key(packet[0], layout.HasSubCommand && packet.Length >= layout.MinPacketLength ? layout.Read(packet) : -1);
        }

        private static int Key(int id, int subCommand) => (id << 16) | (subCommand & 0xFFFF);

        private static string Label(int key)
        {
            int id = key >> 16, sub = key & 0xFFFF;
            return "0x" + id.ToString("X2") + (sub == 0xFFFF ? string.Empty : " 0x" + sub.ToString("X2"));
        }

        private readonly struct Mask
        {
            public Mask(int key, int from, int to, string reason)
            {
                Key = key;
                From = from;
                To = to;
                Reason = reason;
            }

            public int Key { get; }
            public int From { get; }
            public int To { get; }
            public string Reason { get; }
        }
    }
}
