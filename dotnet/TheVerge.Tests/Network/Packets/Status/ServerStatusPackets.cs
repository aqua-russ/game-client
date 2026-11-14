using System.Collections.Generic;
using System.Text;

namespace TheVerge.Tests.Network.Packets.Status
{
    /// <summary>
    /// Пакеты состояния моба так, как их пишет сервер (Projects/Server/Network/Packets/OutgoingMobilePackets.cs,
    /// OutgoingPlayerPackets.cs): числа big-endian, поле за полем в том же порядке.
    /// </summary>
    internal static class ServerStatusPackets
    {
        /// <summary>
        /// CreateMobileStatus, версия 0 (краткий — чужим): имя UTF-8 30 байт, здоровье «текущее, максимум» (reverse) —
        /// у краткого долей: cur × 100 / max и 100 (WriteAttribute, normalize), переименовать ли, версия 0. 43 байта.
        /// </summary>
        public static byte[] Compact(uint serial, string name, int hits, int hitsMax, bool canBeRenamed)
        {
            var p = Head(serial, name);
            U16(p, hitsMax == 0 ? hits : hits * 100 / hitsMax);
            U16(p, hitsMax == 0 ? hitsMax : 100);
            p.Add((byte)(canBeRenamed ? 1 : 0));
            p.Add(0);
            return Finish(p);
        }

        /// <summary>CreateMobileStatus, версия 3 (свой персонаж в UOR): после краткой части — пол, статы, выносливость, мана, золото, броня, вес, предел статов, питомцы. 70 байт.</summary>
        public static byte[] Full(uint serial, string name, Sheet s)
        {
            var p = Head(serial, name);
            U16(p, s.Hits);
            U16(p, s.HitsMax);
            p.Add(0);
            p.Add(3);
            p.Add((byte)(s.Female ? 1 : 0));
            foreach (var v in new[] { s.Str, s.Dex, s.Int, s.Stam, s.StamMax, s.Mana, s.ManaMax })
            {
                U16(p, v);
            }

            U32(p, (uint)s.Gold);
            U16(p, s.Armor);
            U16(p, s.Weight);
            U16(p, s.StatCap);
            p.Add((byte)s.Followers);
            p.Add((byte)s.FollowersMax);
            return Finish(p);
        }

        /// <summary>CreateMobileHits / Mana / Stam: id, Serial, максимум, текущее (не reverse). 9 байт.</summary>
        public static byte[] Vital(byte id, uint serial, int max, int current)
        {
            var p = new List<byte> { id };
            U32(p, serial);
            U16(p, max);
            U16(p, current);
            return p.ToArray();
        }

        /// <summary>CreateMobileAttributes 0x2D: здоровье, мана, выносливость — каждое «максимум, текущее». 17 байт.</summary>
        public static byte[] Attributes(uint serial, (int Max, int Current) hits, (int Max, int Current) mana, (int Max, int Current) stam)
        {
            var p = new List<byte> { 0x2D };
            U32(p, serial);
            foreach (var v in new[] { hits, mana, stam })
            {
                U16(p, v.Max);
                U16(p, v.Current);
            }

            return p.ToArray();
        }

        /// <summary>CreateMobileHealthbar 0x17: длина 12, Serial, short 1, short вид (1 — яд, 2 — жёлтая), byte уровень.</summary>
        public static byte[] Healthbar(uint serial, int kind, int level)
        {
            var p = new List<byte> { 0x17, 0, 12 };
            U32(p, serial);
            U16(p, 1);
            U16(p, kind);
            p.Add((byte)level);
            return p.ToArray();
        }

        /// <summary>SendBondedStatus 0xBF 0x19, команда 0: Serial, мёртвый ли привязанный питомец. 11 байт.</summary>
        public static byte[] Bonded(uint serial, bool dead)
        {
            var p = new List<byte> { 0xBF, 0, 11, 0, 0x19, 0 };
            U32(p, serial);
            p.Add((byte)(dead ? 1 : 0));
            return p.ToArray();
        }

        /// <summary>SendStatLockInfo 0xBF 0x19, команда 2: Serial, 0, биты замков (сила &lt;&lt; 4 | ловкость &lt;&lt; 2 | интеллект). 12 байт.</summary>
        public static byte[] StatLocks(uint serial, int str, int dex, int intel)
        {
            var p = new List<byte> { 0xBF, 0, 12, 0, 0x19, 2 };
            U32(p, serial);
            p.Add(0);
            p.Add((byte)((str << 4) | (dex << 2) | intel));
            return p.ToArray();
        }

        private static List<byte> Head(uint serial, string name)
        {
            var p = new List<byte> { 0x11, 0, 0 };
            U32(p, serial);
            var bytes = new byte[30];
            var text = Encoding.UTF8.GetBytes(name);
            System.Array.Copy(text, bytes, System.Math.Min(text.Length, 30));
            p.AddRange(bytes);
            return p;
        }

        private static byte[] Finish(List<byte> p)
        {
            p[1] = (byte)(p.Count >> 8);
            p[2] = (byte)p.Count;
            return p.ToArray();
        }

        private static void U16(List<byte> p, int v)
        {
            p.Add((byte)(v >> 8));
            p.Add((byte)v);
        }

        private static void U32(List<byte> p, uint v)
        {
            p.Add((byte)(v >> 24));
            p.Add((byte)(v >> 16));
            p.Add((byte)(v >> 8));
            p.Add((byte)v);
        }

        internal sealed class Sheet
        {
            public int Hits { get; set; } = 67;
            public int HitsMax { get; set; } = 67;
            public bool Female { get; set; }
            public int Str { get; set; } = 35;
            public int Dex { get; set; } = 35;
            public int Int { get; set; } = 20;
            public int Stam { get; set; } = 35;
            public int StamMax { get; set; } = 35;
            public int Mana { get; set; } = 20;
            public int ManaMax { get; set; } = 20;
            public int Gold { get; set; } = 1000;
            public int Armor { get; set; }
            public int Weight { get; set; } = 79;
            public int StatCap { get; set; } = 225;
            public int Followers { get; set; }
            public int FollowersMax { get; set; } = 5;
        }
    }
}
