using System;
using System.IO;
using TheVerge.Core.Ports;
using UnityEngine;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Файлы данных мира — один раз на запуск, общие для ходьбы (предсказание шага, PLAN Стадия 3) и отладочной карты
    /// (PLAN Стадия 2, п. 13). Читаются при первом входе; не прочитались — <see cref="Data"/> null, причина — в
    /// <see cref="Problem"/> и журнале, повторно не читаются. Папка — настройка Unity <c>TheVerge.DataFolder</c>, по
    /// умолчанию remaster/uo-data/Ultima Online Classic рядом с папкой клиента.
    /// </summary>
    public sealed class WorldDataSource : IDisposable
    {
        private const string FolderKey = "TheVerge.DataFolder";

        private readonly ILog _log;
        private bool _tried;

        public WorldDataSource(ILog log)
        {
            _log = log;
        }

        /// <summary>Прочитанные файлы; null — ещё не читались (<see cref="Load"/>) или не подошли.</summary>
        public UnityWorldData Data { get; private set; }

        /// <summary>Почему файлов нет: папка и ошибка словами; пусто — файлы есть или ещё не читались.</summary>
        public string Problem { get; private set; } = string.Empty;

        /// <summary>Прочитать файлы при первом вызове; дальше — то, что прочитано тогда.</summary>
        public UnityWorldData Load()
        {
            if (_tried)
            {
                return Data;
            }

            _tried = true;
            var folder = PlayerPrefs.GetString(FolderKey, DefaultFolder());
            Data = UnityWorldData.Load(folder, _log, out var error);
            if (Data == null)
            {
                Problem = "файлы данных (" + folder + "): " + error;
                _log.Warning("Ходьбы и карты нет — " + Problem);
            }

            return Data;
        }

        public void Dispose()
        {
            Data?.Dispose();
            Data = null;
        }

        // Assets → The Verge → client → remaster: три шага вверх от папки Assets.
        private static string DefaultFolder() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "uo-data", "Ultima Online Classic"));
    }
}
