using TheVerge.Core.Chat;
using TheVerge.Core.Items;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>Мир клиента бота и действия в нём: то, с чем работают действия --action.</summary>
    public sealed class BotWorld
    {
        public BotWorld(EntityStore entities, WorldEnvironment environment, ContainerContents containers, ItemCommands items,
            SpeechCommands speech)
        {
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
    }
}
