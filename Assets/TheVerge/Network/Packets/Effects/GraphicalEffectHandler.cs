using TheVerge.Core.Combat;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Effects
{
    /// <summary>
    /// 0x70 Graphical Effect: голова эффекта без цвета (<see cref="EffectHead"/>). Сервер шлёт его только экранным
    /// (OutgoingEffectPackets.cs, SendScreenEffect: вид 4, 8 нулей, ushort номер экранного эффекта на месте графики, 16 нулей);
    /// остальные виды разбираем так же, как 0xC0.
    /// </summary>
    public sealed class GraphicalEffectHandler
    {
        public const byte PacketId = 0x70;

        private readonly CombatLog _log;

        public GraphicalEffectHandler(CombatLog log)
        {
            _log = log;
        }

        public void Handle(ref PacketReader reader)
        {
            var head = EffectHead.Read(ref reader);
            if (head.Kind == EffectKind.Screen)
            {
                _log.OnScreenEffect(head.ItemId);
                return;
            }

            _log.OnEffect(head.Kind, head.Source, head.Target, head.ItemId, head.From, 0);
        }
    }
}
