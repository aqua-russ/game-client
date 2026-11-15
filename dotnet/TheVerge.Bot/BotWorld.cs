using TheVerge.Core.Chat;
using TheVerge.Core.Items;
using TheVerge.Core.Targeting;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>Мир клиента бота и действия в нём: то, с чем работают действия --action.</summary>
    public sealed class BotWorld
    {
        public BotWorld(EntityStore entities, WorldEnvironment environment, ContainerContents containers, ItemCommands items,
            SpeechCommands speech, TargetCursor target, BotStatus? status = null)
        {
            Status = status;
            Target = target;
            Speech = speech;
            Entities = entities;
            Environment = environment;
            Containers = containers;
            Items = items;
        }

        public EntityStore Entities { get; }

        public WorldEnvironment Environment { get; }

        public ContainerContents Containers { get; }

        public ItemCommands Items { get; }

        public SpeechCommands Speech { get; }

        public TargetCursor Target { get; }

        /// <summary>Быстрые цели без курсора (Стадия 4, п. 6); null — не подключены (тесты, которым они не нужны).</summary>
        public TargetCommands? QuickTargets { get; init; }

        /// <summary>Состояние мобов (Стадия 4); null — не подключено (тесты, которым оно не нужно).</summary>
        public BotStatus? Status { get; }

        /// <summary>Стойка и атака (Стадия 4, п. 7); null — не подключены (тесты, которым они не нужны).</summary>
        public BotCombat? Combat { get; init; }

        /// <summary>Вещи трупов по слоям (Стадия 4, п. 9, 0x89); null — не подключены.</summary>
        public CorpseEquipment? Corpses { get; init; }

        /// <summary>Движение своего персонажа (Стадия 3); null — без файлов данных (--data) шаг не предсказать.</summary>
        public BotMovement? Movement { get; init; }

        /// <summary>Показ шагов чужих мобов и что бот по нему видел (Стадия 3, п. 8); null — не подключён (тесты).</summary>
        public BotOthers? Others { get; init; }
    }
}
