using TheVerge.Core.Combat;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Effects
{
    /// <summary>
    /// 0xC7 Particle Effect: как 0xC0 (<see cref="HuedEffectHandler"/>), затем short эффект, эффект взрыва, звук взрыва,
    /// Serial, byte слой, short 0. Сервер: Projects/Server/Network/Packets/OutgoingEffectPackets.cs, CreateParticleEffect.
    /// 49 байт. Частицы понадобятся для показа (Стадия 9); в журнал — голова и цвет.
    /// </summary>
    public sealed class ParticleEffectHandler
    {
        public const byte PacketId = 0xC7;

        private readonly CombatLog _log;

        public ParticleEffectHandler(CombatLog log)
        {
            _log = log;
        }

        public void Handle(ref PacketReader reader)
        {
            var head = EffectHead.Read(ref reader);
            var hue = reader.ReadInt32();
            reader.Skip(4 + 2 + 2 + 2 + 4 + 1 + 2);
            _log.OnEffect(head.Kind, head.Source, head.Target, head.ItemId, head.From, hue);
        }
    }
}
