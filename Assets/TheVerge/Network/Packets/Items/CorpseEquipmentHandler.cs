using System.Collections.Generic;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Items
{
    /// <summary>
    /// 0x89 Corpse Equipment: труп, пары «byte слой + 1, Serial вещи», конец — байт 0 (Layer.Invalid). Сервер:
    /// UOContent/Items/Misc/Corpses/CorpsePackets.cs, SendCorpseEquip (после 0x3C с теми же вещами в трупе). Список
    /// сохраняется целиком только после конца: обрезанный пакет — ошибка пакета, прежние данные не трогаются.
    /// </summary>
    public sealed class CorpseEquipmentHandler
    {
        public const byte PacketId = 0x89;

        private readonly CorpseEquipment _corpses;
        private readonly List<CorpseItem> _items = new List<CorpseItem>();

        public CorpseEquipmentHandler(CorpseEquipment corpses)
        {
            _corpses = corpses;
        }

        public void Handle(ref PacketReader reader)
        {
            var corpse = new Serial(reader.ReadUInt32());
            _items.Clear();
            for (var layer = reader.ReadByte(); layer != (byte)Layer.Invalid; layer = reader.ReadByte())
            {
                _items.Add(new CorpseItem((Layer)(layer - 1), new Serial(reader.ReadUInt32())));
            }

            _corpses.Set(corpse, _items);
        }
    }
}
