using TheVerge.Core.Combat;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Effects
{
    /// <summary>
    /// 0xC0 Hued Effect: голова эффекта (<see cref="EffectHead"/>), int цвет, int режим отрисовки. Сервер:
    /// Projects/Server/Network/Packets/OutgoingEffectPackets.cs, CreateHuedEffect. 36 байт.
    /// </summary>
    public sealed class HuedEffectHandler
    {
        public const byte PacketId = 0xC0;

        private readonly CombatLog _log;

        public HuedEffectHandler(CombatLog log)
        {
            _log = log;
        }

        public void Handle(ref PacketReader reader)
        {
            var head = EffectHead.Read(ref reader);
            var hue = reader.ReadInt32();
            reader.Skip(4);
            _log.OnEffect(head.Kind, head.Source, head.Target, head.ItemId, head.From, hue);
        }
    }
}
