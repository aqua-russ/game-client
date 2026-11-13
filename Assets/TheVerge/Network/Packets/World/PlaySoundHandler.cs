using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0x54 Play Sound: звук. Сервер: OutgoingEffectPackets.cs, CreateSoundEffect — byte 1, short звук, short 0, short X, Y,
    /// Z. Пока только считается: играть будет Presentation (Стадия 9).
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
            _environment.OnSound(reader.ReadUInt16());
        }
    }
}
