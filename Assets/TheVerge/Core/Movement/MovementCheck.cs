using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Предсказание проходимости шага (PLAN Стадия 3, п. 2): повторяет серверный <c>Movement.CheckMovement</c> и
    /// блокировку живыми мобами из <c>Mobile.CanMove</c>. Работает на данных <see cref="IMapData"/>, <see cref="ITileData"/>,
    /// <see cref="IMultiData"/> и <see cref="EntityStore"/>; не выделяет память в установившемся режиме.
    /// </summary>
    public sealed class MovementCheck
    {
        private const int MobileBlockHeight = 15;

        private readonly IMapData _map;
        private readonly EntityStore? _entities;
        private readonly MovementSurface _surface;
        private readonly List<Mobile> _cellMobiles = new List<Mobile>(8);

        public MovementCheck(IMapData mapData, ITileData tileData, IMultiData? multiData = null, EntityStore? entities = null)
        {
            _map = mapData;
            _entities = entities;
            _surface = new MovementSurface(mapData, tileData, multiData, entities);
        }

        public bool CheckMovement(Point3D loc, Direction direction, out int newZ) =>
            CheckMovement(loc, direction, MoverState.Player(), out newZ);

        public bool CheckMovement(Point3D loc, Direction direction, MoverState mover, out int newZ)
        {
            Direction facing = direction & Direction.FacingMask;
            Point3D forward = loc.GetStepPoint(facing);
            if (!InBounds(forward.X, forward.Y))
            {
                newZ = 0;
                return false;
            }

            bool diagonal = ((int)facing & 0x1) == 0x1;
            Point3D left = diagonal ? loc.GetStepPoint((Direction)(((int)facing - 1) & 0x7)) : default;
            Point3D right = diagonal ? loc.GetStepPoint((Direction)(((int)facing + 1) & 0x7)) : default;
            if (diagonal && (!InBounds(left.X, left.Y) || !InBounds(right.X, right.Y)))
            {
                newZ = 0;
                return false;
            }

            _surface.ComputeStart(loc, mover.IgnoreMovableImpassables);
            if (!_surface.CheckCell(forward.X, forward.Y, loc.Z, mover, out newZ))
            {
                return false;
            }

            if (diagonal && !CheckCorners(loc.Z, left, right, mover))
            {
                newZ = _surface.StartZ;
                return false;
            }

            if (HasBlockingMobile(forward.X, forward.Y, newZ, mover))
            {
                newZ = loc.Z;
                return false;
            }

            return true;
        }

        private bool CheckCorners(int locZ, Point3D left, Point3D right, MoverState mover)
        {
            bool holdLeft = _surface.CheckCell(left.X, left.Y, locZ, mover, out _);
            bool holdRight = _surface.CheckCell(right.X, right.Y, locZ, mover, out _);
            return mover.IsGameMaster ? (holdLeft || holdRight) : (holdLeft && holdRight);
        }

        private bool HasBlockingMobile(int x, int y, int newZ, MoverState mover)
        {
            if (_entities == null || mover.CanShoveMobiles)
            {
                return false;
            }

            _entities.Spatial.QueryMobiles(x, y, 0, _cellMobiles);
            for (int i = 0; i < _cellMobiles.Count; i++)
            {
                var mobile = _cellMobiles[i];
                if (mobile.Serial.Value == mover.Serial || mobile.IsGhost || mobile.Status.IsDeadBonded)
                {
                    continue;
                }

                if (mobile.Position.Z + MobileBlockHeight > newZ && newZ + MobileBlockHeight > mobile.Position.Z)
                {
                    return true;
                }
            }

            return false;
        }

        private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _map.Width && y < _map.Height;
    }
}
