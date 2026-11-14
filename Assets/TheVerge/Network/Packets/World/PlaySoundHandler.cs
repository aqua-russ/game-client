using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0x54 Play Sound: звук. Сервер: OutgoingEffectPackets.cs, CreateSoundEffect — byte 1, short звук, short 0, short X, Y,
    /// Z. Счётчик и последний звук — в <see cref="WorldEnvironment"/>, точка — в событие звука (журнал боя, Стадия 4, п. 8);
    /// играть будет Presentation (Стадия 9).
    /// </summary>
    public sealed class PlaySoundHandler
    {
        public const byte PacketId = 0x54;

        private readonly WorldEnvironment _environment;

        public PlaySoundHandler(WorldEnvironment environment)
        {
            _environment = environment;
        }

        public void Handle(ref PacketReader reader)
        {
            reader.Skip(1);
            var sound = reader.ReadUInt16();
            reader.Skip(2);
            var position = new Point3D(reader.ReadInt16(), reader.ReadInt16(), (sbyte)reader.ReadInt16());
            _environment.OnSound(sound, position);
        }
    }
}
