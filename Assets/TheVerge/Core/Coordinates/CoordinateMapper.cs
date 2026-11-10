using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Конвертер координат между серверной целочисленной сеткой ModernUO (X, Y, Z)
    /// и непрерывным 3D-пространством Unity (x, y, z в метрах).
    /// Правило из AGENTS.md: Unity.X = Server.X, Unity.Z = -Server.Y, Unity.Y = Server.Z * 0.1f.
    /// </summary>
    public static class CoordinateMapper
    {
        public const float TileSizeMeters = 1.0f;
        public const float HeightScaleMeters = 0.1f;

        /// <summary>
        /// Переводит серверные координаты Point3D в координаты мира Unity 3D (X, Y, Z).
        /// </summary>
        public static (float x, float y, float z) ToWorld3D(Point3D serverPos)
        {
            float x = serverPos.X * TileSizeMeters;
            float y = serverPos.Z * HeightScaleMeters;
            float z = -serverPos.Y * TileSizeMeters;
            return (x, y, z);
        }

        /// <summary>
        /// Переводит непрерывные координаты Unity 3D обратно в целочисленную клетку сервера.
        /// </summary>
        public static Point3D ToServerGrid(float unityX, float unityY, float unityZ)
        {
            short serverX = (short)MathF.Round(unityX / TileSizeMeters);
            short serverY = (short)MathF.Round(-unityZ / TileSizeMeters);
            sbyte serverZ = (sbyte)MathF.Round(unityY / HeightScaleMeters);
            return new Point3D(serverX, serverY, serverZ);
        }

        /// <summary>
        /// Переводит 8 направлений Direction UO в угол вращения Unity в градусах вокруг оси Y (Euler Y).
        /// В UO North = 0 (на экране в изометрии вверх-вправо), в 3D Unity сориентировано по сторонам света.
        /// </summary>
        public static float DirectionToEulerY(Direction direction)
        {
            return (direction & Direction.FacingMask) switch
            {
                Direction.North => 0f,
                Direction.Right => 45f,
                Direction.East  => 90f,
                Direction.Down  => 135f,
                Direction.South => 180f,
                Direction.Left  => 225f,
                Direction.West  => 270f,
                Direction.Up    => 315f,
                _               => 0f
            };
        }

        /// <summary>
        /// Вычисляет ближайшее из 8 направлений Direction UO по вектору смещения в 3D (deltaX, deltaZ).
        /// </summary>
        public static Direction VectorToDirection(float deltaX, float deltaZ)
        {
            float angle = MathF.Atan2(deltaX, deltaZ) * (180f / MathF.PI);
            if (angle < 0f) angle += 360f;

            int sector = (int)MathF.Round(angle / 45f) % 8;
            return (Direction)sector;
        }
    }
}
