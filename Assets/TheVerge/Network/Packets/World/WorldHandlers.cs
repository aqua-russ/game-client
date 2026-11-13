using TheVerge.Core.World;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.World
{
    /// <summary>Обработчики пакетов мира (PLAN Стадия 2, п. 9) — все разом для Bootstrap и бота.</summary>
    public static class WorldHandlers
    {
        public static void Register(PacketDispatcher dispatcher, EntityStore store, WorldEnvironment environment)
        {
            dispatcher.Register(MobileIncomingHandler.PacketId, new MobileIncomingHandler(store).Handle);
            dispatcher.Register(MobileUpdateHandler.PacketId, new MobileUpdateHandler(store).Handle);
            dispatcher.Register(MobileMovingHandler.PacketId, new MobileMovingHandler(store).Handle);
            dispatcher.Register(EquipUpdateHandler.PacketId, new EquipUpdateHandler(store).Handle);
            dispatcher.Register(RemoveEntityHandler.PacketId, new RemoveEntityHandler(store).Handle);
            dispatcher.Register(WorldObjectHandler.PacketId, new WorldObjectHandler(store).Handle);
            dispatcher.Register(MapChangeHandler.PacketId, MapChangeHandler.SubCommand, new MapChangeHandler(store).Handle);
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
