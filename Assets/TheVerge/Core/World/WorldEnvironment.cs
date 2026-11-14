using System;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Всё о мире, кроме объектов (PLAN Стадия 2, п. 9): свет (0x4F, 0x4E), сезон (0xBC), время (0x5B), погода (0x65),
    /// размер карты (0x76), число патчей карт (0xBF 0x18), звук (0x54) и музыка (0x6D). Пока только хранится; показывать
    /// и играть будет Presentation. О каждом изменении — <see cref="Changed"/>.
    /// </summary>
    public sealed class WorldEnvironment
    {
        /// <summary>«Музыки нет»: сервер останавливает её номером 0x1FFF (OutgoingPlayerPackets.cs, SendStopMusic).</summary>
        public const ushort NoMusic = 0x1FFF;

        /// <summary>Карт, о патчах которых помнит клиент: сервер шлёт 4 (OutgoingMapPackets.cs, SendMapPatches).</summary>
        public const int PatchMaps = 8;

        private readonly int[] _staticPatches = new int[PatchMaps];
        private readonly int[] _landPatches = new int[PatchMaps];

        public event Action? Changed;

        /// <summary>Звук сервера (0x54): номер и точка — для журнала боя (Стадия 4, п. 8) и, позже, для проигрывания.</summary>
        public event Action<ushort, Point3D>? SoundPlayed;

        public byte GlobalLight { get; private set; }

        public byte PersonalLight { get; private set; }

        public byte Season { get; private set; }

        public int Hour { get; private set; }

        public int Minute { get; private set; }

        public int Second { get; private set; }

        public byte WeatherType { get; private set; }

        public byte WeatherDensity { get; private set; }

        /// <summary>Температура: сервер пишет её байтом, у клиента она со знаком.</summary>
        public sbyte Temperature { get; private set; }

        public MapSize MapSize { get; private set; }

        public long SoundsPlayed { get; private set; }

        public ushort LastSound { get; private set; }

        public ushort Music { get; private set; } = NoMusic;

        public int GetStaticPatches(int map) => map >= 0 && map < PatchMaps ? _staticPatches[map] : 0;

        public int GetLandPatches(int map) => map >= 0 && map < PatchMaps ? _landPatches[map] : 0;

        public void SetGlobalLight(byte level)
        {
            GlobalLight = level;
            Changed?.Invoke();
        }

        public void SetPersonalLight(byte level)
        {
            PersonalLight = level;
            Changed?.Invoke();
        }

        public void SetSeason(byte season)
        {
            Season = season;
            Changed?.Invoke();
        }

        public void SetTime(int hour, int minute, int second)
        {
            Hour = hour;
            Minute = minute;
            Second = second;
            Changed?.Invoke();
        }

        public void SetWeather(byte type, byte density, sbyte temperature)
        {
            WeatherType = type;
            WeatherDensity = density;
            Temperature = temperature;
            Changed?.Invoke();
        }

        public void SetMapSize(MapSize size)
        {
            MapSize = size;
            Changed?.Invoke();
        }

        /// <summary>Патчи одной карты; карты за пределами <see cref="PatchMaps"/> не запоминаются.</summary>
        public void SetMapPatches(int map, int statics, int land)
        {
            if (map < 0 || map >= PatchMaps)
            {
                return;
            }

            _staticPatches[map] = statics;
            _landPatches[map] = land;
            Changed?.Invoke();
        }

        public void OnSound(ushort sound, Point3D position)
        {
            SoundsPlayed++;
            LastSound = sound;
            SoundPlayed?.Invoke(sound, position);
        }

        public void SetMusic(ushort music)
        {
            Music = music;
            Changed?.Invoke();
        }
    }
}
