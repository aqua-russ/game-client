using TheVerge.Core.World;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Свойства шагающего моба, влияющие на проходимость клеток: права GM (диагонали и толпа), проход сквозь двери
    /// (призрак, тело 0x03DB, мёртвый привязанный питомец), проход сквозь переносимые предметы и проталкивание живых мобов
    /// (Movement.cs и Mobile.CanMove сервера).
    /// </summary>
    public readonly struct MoverState
    {
        public MoverState(uint serial, bool isGameMaster, bool ignoreDoors, bool ignoreMovableImpassables, bool canShoveMobiles)
        {
            Serial = serial;
            IsGameMaster = isGameMaster;
            IgnoreDoors = ignoreDoors;
            IgnoreMovableImpassables = ignoreMovableImpassables;
            CanShoveMobiles = canShoveMobiles;
        }

        public uint Serial { get; }
        public bool IsGameMaster { get; }
        public bool IgnoreDoors { get; }
        public bool IgnoreMovableImpassables { get; }
        public bool CanShoveMobiles { get; }

        public static MoverState Player(uint serial = 0, bool canShoveMobiles = false) =>
            new MoverState(serial, isGameMaster: false, ignoreDoors: false, ignoreMovableImpassables: false, canShoveMobiles);

        public static MoverState GameMaster(uint serial = 0) =>
            new MoverState(serial, isGameMaster: true, ignoreDoors: false, ignoreMovableImpassables: false, canShoveMobiles: true);

        public static MoverState Ghost(uint serial = 0) =>
            new MoverState(serial, isGameMaster: false, ignoreDoors: true, ignoreMovableImpassables: true, canShoveMobiles: true);

        public static MoverState FromMobile(Mobile mobile, bool isGameMaster = false, bool canShoveMobiles = false)
        {
            bool isGhost = mobile.IsGhost;
            bool ignoreDoors = isGhost || Bodies.PassesDoors(mobile.Body) || mobile.Status.IsDeadBonded;
            bool shove = isGameMaster || isGhost || canShoveMobiles;
            return new MoverState(mobile.Serial.Value, isGameMaster, ignoreDoors, isGhost, shove);
        }
    }
}
