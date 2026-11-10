using System.Collections.Generic;

namespace TheVerge.Core
{
    /// <summary>
    /// Единый источник данных о геометрии и проходимости полигона «Окрестности Минока».
    /// Синхронизирован с серверными файлами ModernUO (tiledata.mul, map0.mul, statics0.mul).
    /// </summary>
    public static class MinocMapSliceData
    {
        public static readonly Point3D DefaultSpawn = new Point3D(2545, 502, 0);

        // Река (русло с севера на юг)
        public const short RiverMinX = 2546;
        public const short RiverMaxX = 2548;
        public const short RiverMinY = 470;
        public const short RiverMaxY = 540;

        // Деревянный мост через реку
        public const short BridgeMinX = 2544;
        public const short BridgeMaxX = 2550;
        public const short BridgeMinY = 497;
        public const short BridgeMaxY = 499;

        // Деревья
        public static readonly (short X, short Y)[] Trees = new (short, short)[]
        {
            (2530, 490), (2536, 492), (2542, 488), (2552, 485), (2560, 488),
            (2528, 515), (2538, 520), (2548, 525), (2562, 518), (2533, 495),
            (2550, 489), (2535, 523)
        };

        // Валуны / камни
        public static readonly (short X, short Y)[] Boulders = new (short, short)[]
        {
            (2532, 498), (2552, 510), (2565, 522), (2543, 512)
        };

        // Точки рудных жил в пещере
        public static readonly (short X, short Y)[] OreVeins = new (short, short)[]
        {
            (2563, 495), (2569, 499), (2565, 506), (2570, 510)
        };

        public static bool IsRiverWater(int x, int y)
        {
            return x >= RiverMinX && x <= RiverMaxX && y >= RiverMinY && y <= RiverMaxY;
        }

        public static bool IsBridge(int x, int y)
        {
            return x >= BridgeMinX && x <= BridgeMaxX && y >= BridgeMinY && y <= BridgeMaxY;
        }

        public static bool IsBridgeRail(int x, int y)
        {
            return x >= BridgeMinX && x <= BridgeMaxX && (y == BridgeMinY - 1 || y == BridgeMaxY + 1);
        }

        public static sbyte CalculateBridgeZ(int x, int y)
        {
            if (!IsBridge(x, y)) return 0;

            return x switch
            {
                2544 or 2550 => 3,
                2545 or 2549 => 7,
                2546 or 2547 or 2548 => 10,
                _ => 0
            };
        }

        public static bool IsTentWall(int x, int y)
        {
            // Палатка 1 (Северная)
            if ((x == 2534 || x == 2537) && (y >= 505 && y <= 508))
            {
                if (x == 2537 && y == 506) return false; // вход в палатку
                return true;
            }
            if ((y == 505 || y == 508) && (x >= 2534 && x <= 2537)) return true;

            // Палатка 2 (Южная)
            if ((x == 2534 || x == 2537) && (y >= 510 && y <= 513))
            {
                if (x == 2537 && y == 511) return false; // вход в палатку
                return true;
            }
            if ((y == 510 || y == 513) && (x >= 2534 && x <= 2537)) return true;

            return false;
        }

        public static bool IsTree(int x, int y)
        {
            for (int i = 0; i < Trees.Length; i++)
            {
                if (Trees[i].X == x && Trees[i].Y == y) return true;
            }
            return false;
        }

        public static bool IsBoulder(int x, int y)
        {
            for (int i = 0; i < Boulders.Length; i++)
            {
                if (Boulders[i].X == x && Boulders[i].Y == y) return true;
            }
            return false;
        }

        public static bool IsMountainWall(int x, int y)
        {
            // Горный массив на востоке
            if (x >= 2555 && x <= 2574 && y >= 491 && y <= 515)
            {
                // Входной коридор в пещеру
                if (x >= 2554 && x <= 2557 && y >= 498 && y <= 500) return false;
                // Внутренний зал шахты/пещеры
                if (x >= 2558 && x <= 2571 && y >= 494 && y <= 512) return false;

                return true;
            }
            return false;
        }

        public static bool IsCaveInterior(int x, int y)
        {
            if (x >= 2554 && x <= 2557 && y >= 498 && y <= 500) return true;
            if (x >= 2558 && x <= 2571 && y >= 494 && y <= 512) return true;
            return false;
        }

        public static bool IsOreVeinLocation(int x, int y)
        {
            for (int i = 0; i < OreVeins.Length; i++)
            {
                if (OreVeins[i].X == x && OreVeins[i].Y == y) return true;
            }
            return false;
        }

        public static bool IsMinableTile(int x, int y)
        {
            return IsCaveInterior(x, y) || IsMountainWall(x, y) || IsOreVeinLocation(x, y);
        }

        public static bool IsPassable(int x, int y, out sbyte z)
        {
            z = 0;

            // Перила моста непроходимы
            if (IsBridgeRail(x, y)) return false;

            // Проход по мосту над рекой
            if (IsBridge(x, y))
            {
                z = CalculateBridgeZ(x, y);
                return true;
            }

            // Вода реки непроходима без моста
            if (IsRiverWater(x, y)) return false;

            // Стены палаток непроходимы
            if (IsTentWall(x, y)) return false;

            // Деревья и валуны непроходимы
            if (IsTree(x, y)) return false;
            if (IsBoulder(x, y)) return false;

            // Скальные стены горы непроходимы
            if (IsMountainWall(x, y)) return false;

            // Объекты внутри пещеры
            if (x == 2558 && (y == 496 || y == 498)) return false; // Горн и наковальня

            // Внутри пещеры или обычная равнина
            z = 0;
            return true;
        }
    }
}
