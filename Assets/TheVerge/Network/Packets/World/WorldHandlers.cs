using TheVerge.Core.Movement;
using TheVerge.Core.World;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// Обработчики пакетов мира (PLAN Стадия 2, п. 9) — все разом для Bootstrap и бота. С <see cref="MovementReplies"/> 0x20
    /// о своём персонаже и смена карты сбрасывают и движение (PLAN Стадия 3, п. 6); с <see cref="OthersMotion"/> 0x77 и 0x78
    /// ведут показ шагов чужих мобов (п. 8); без них — только мир.
    /// </summary>
    public static class WorldHandlers
    {
        public static void Register(PacketDispatcher dispatcher, EntityStore store, WorldEnvironment environment,
            MovementReplies? movement = null, OthersMotion? others = null)
        {
            dispatcher.Register(MobileIncomingHandler.PacketId, new MobileIncomingHandler(store, others).Handle);
            dispatcher.Register(MobileUpdateHandler.PacketId, new MobileUpdateHandler(store, movement).Handle);
            dispatcher.Register(MobileMovingHandler.PacketId, new MobileMovingHandler(store, others).Handle);
            dispatcher.Register(EquipUpdateHandler.PacketId, new EquipUpdateHandler(store).Handle);
            dispatcher.Register(RemoveEntityHandler.PacketId, new RemoveEntityHandler(store).Handle);
            dispatcher.Register(WorldObjectHandler.PacketId, new WorldObjectHandler(store).Handle);
            dispatcher.Register(MapChangeHandler.PacketId, MapChangeHandler.SubCommand, new MapChangeHandler(store, movement).Handle);
            dispatcher.Register(ServerChangeHandler.PacketId, new ServerChangeHandler(store, environment).Handle);
            RegisterEnvironment(dispatcher, store, environment);
        }

        private static void RegisterEnvironment(PacketDispatcher dispatcher, EntityStore store, WorldEnvironment environment)
        {
            dispatcher.Register(MapPatchesHandler.PacketId, MapPatchesHandler.SubCommand, new MapPatchesHandler(environment).Handle);
            dispatcher.Register(GlobalLightHandler.PacketId, new GlobalLightHandler(environment).Handle);
            dispatcher.Register(PersonalLightHandler.PacketId, new PersonalLightHandler(store, environment).Handle);
            dispatcher.Register(SeasonChangeHandler.PacketId, new SeasonChangeHandler(environment).Handle);
            dispatcher.Register(CurrentTimeHandler.PacketId, new CurrentTimeHandler(environment).Handle);
            dispatcher.Register(WeatherHandler.PacketId, new WeatherHandler(environment).Handle);
            dispatcher.Register(PlaySoundHandler.PacketId, new PlaySoundHandler(environment).Handle);
            dispatcher.Register(PlayMusicHandler.PacketId, new PlayMusicHandler(environment).Handle);
        }
    }
}
