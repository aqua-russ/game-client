using System.Collections.Generic;

namespace TheVerge.Tests.Network.Packets.Combat
{
    /// <summary>
    /// Боевые события так, как их пишет сервер (big-endian, поле за полем): OutgoingCombatPackets.cs (SendSwing),
    /// OutgoingMobilePackets.cs (CreateMobileAnimation), OutgoingDamagePackets.cs (SendDamage),
    /// OutgoingEffectPackets.cs (SendScreenEffect, CreateHuedEffect, CreateParticleEffect, CreateSoundEffect).
    /// </summary>
    internal static class ServerCombatEventPackets
    {
        /// <summary>SendSwing: 0x2F, byte 0, атакующий, цель. 10 байт.</summary>
        public static byte[] Swing(uint attacker, uint defender)
        {
            var p = new List<byte> { 0x2F, 0 };
            U32(p, attacker);
            U32(p, defender);
            return p.ToArray();
        }

        /// <summary>CreateMobileAnimation: 0x6E, моб, short действие, кадров, повторов, bool «назад» (!forward), повторять, byte задержка. 14 байт.</summary>
        public static byte[] Animation(uint mobile, int action, (int Frames, int Repeats, int Delay) timing, bool forward, bool repeat)
        {
            var p = new List<byte> { 0x6E };
            U32(p, mobile);
            U16(p, action);
            U16(p, timing.Frames);
            U16(p, timing.Repeats);
            p.Add((byte)(forward ? 0 : 1));
            p.Add((byte)(repeat ? 1 : 0));
            p.Add((byte)timing.Delay);
            return p.ToArray();
        }

        /// <summary>SendDamage для клиента с DamagePacket (5.0.0a+): 0x0B, моб, ushort урон (0..0xFFFF). 7 байт.</summary>
        public static byte[] Damage(uint mobile, int amount)
        {
            var p = new List<byte> { 0x0B };
            U32(p, mobile);
            U16(p, amount);
            return p.ToArray();
        }

        /// <summary>SendScreenEffect: 0x70, byte 4, 8 нулей, ushort вид, 16 нулей. 28 байт.</summary>
        public static byte[] Screen(int type)
        {
            var p = new List<byte> { 0x70, 0x04 };
            p.AddRange(new byte[8]);
            U16(p, type);
            p.AddRange(new byte[16]);
            return p.ToArray();
        }

        /// <summary>CreateHuedEffect: 0xC0, вид, от, к, графика, точки «от» и «к» (short X, Y, sbyte Z), скорость, длительность, 0, 0, неподвижное направление, взрыв, int цвет, int режим. 36 байт.</summary>
        public static byte[] Hued(Effect e) => HuedBody(0xC0, e).ToArray();

        /// <summary>CreateParticleEffect: как 0xC0, затем short эффект, эффект взрыва, звук взрыва, Serial, byte слой, short 0. 49 байт.</summary>
        public static byte[] Particle(Effect e, int effect, int explodeEffect, int explodeSound, uint serial, int layer)
        {
            var p = HuedBody(0xC7, e);
            U16(p, effect);
            U16(p, explodeEffect);
            U16(p, explodeSound);
            U32(p, serial);
            p.Add((byte)layer);
            U16(p, 0);
            return p.ToArray();
        }

        /// <summary>
        /// Графический эффект 0x70 в общем виде (сервер шлёт только экранный; общий вид — голова 0xC0 без цвета и режима).
        /// 28 байт.
        /// </summary>
        public static byte[] Graphical(Effect e)
        {
            var p = HuedBody(0x70, e);
            p.RemoveRange(p.Count - 8, 8);
            return p.ToArray();
        }

        /// <summary>CreateSoundEffect: 0x54, byte 1, short звук, short 0 (громкость), short X, Y, Z. 12 байт.</summary>
        public static byte[] Sound(int sound, int x, int y, int z)
        {
            var p = new List<byte> { 0x54, 1 };
            U16(p, sound);
            U16(p, 0);
            U16(p, x);
            U16(p, y);
            U16(p, z);
            return p.ToArray();
        }

        private static List<byte> HuedBody(byte id, Effect e)
        {
            var p = new List<byte> { id, (byte)e.Type };
            U32(p, e.From);
            U32(p, e.To);
            U16(p, e.ItemId);
            U16(p, e.FromX);
            U16(p, e.FromY);
            p.Add((byte)(sbyte)e.FromZ);
            U16(p, e.ToX);
            U16(p, e.ToY);
            p.Add((byte)(sbyte)e.ToZ);
            p.Add((byte)e.Speed);
            p.Add((byte)e.Duration);
            p.Add(0);
            p.Add(0);
            p.Add((byte)(e.FixedDirection ? 1 : 0));
            p.Add((byte)(e.Explode ? 1 : 0));
            U32(p, (uint)e.Hue);
            U32(p, (uint)e.RenderMode);
            return p;
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

        /// <summary>Поля эффекта, общие для 0x70, 0xC0, 0xC7.</summary>
        internal sealed class Effect
        {
            public int Type { get; set; }
            public uint From { get; set; }
            public uint To { get; set; }
            public int ItemId { get; set; }
            public int FromX { get; set; }
            public int FromY { get; set; }
            public int FromZ { get; set; }
            public int ToX { get; set; }
            public int ToY { get; set; }
            public int ToZ { get; set; }
            public int Speed { get; set; }
            public int Duration { get; set; }
            public bool FixedDirection { get; set; }
            public bool Explode { get; set; }
            public int Hue { get; set; }
            public int RenderMode { get; set; }
        }
    }
}
