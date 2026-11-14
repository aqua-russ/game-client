using System;
using System.IO;
using TheVerge.Core.MapView;
using TheVerge.Core.Ports;
using TheVerge.Core.World;
using TheVerge.Presentation.World;
using UnityEngine;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Хозяин отладочной карты (PLAN Стадия 2, п. 13): читает файлы данных один раз при первом входе в мир, создаёт
    /// <see cref="DebugMapPresenter"/> на время «в мире» и закрывает его при выходе. Папка данных — настройка Unity
    /// <c>TheVerge.DataFolder</c>, по умолчанию remaster/uo-data/Ultima Online Classic рядом с папкой клиента.
    /// </summary>
    public sealed class DebugMapHost : IDisposable
    {
        private const string FolderKey = "TheVerge.DataFolder";

        private readonly ILog _log;
        private readonly Camera _camera;
        private readonly Func<Vector2, bool> _pointerOverUi;
        private readonly Action<string> _showReport;
        private UnityWorldData _data;
        private bool _dataTried;
        private string _dataError = string.Empty;
        private DebugMapPresenter _presenter;

        /// <param name="pointerOverUi">Позиция курсора на экране → над панелью ли он.</param>
        /// <param name="showReport">Показать окно клетки (клик по клетке).</param>
        public DebugMapHost(ILog log, Camera camera, Func<Vector2, bool> pointerOverUi, Action<string> showReport)
        {
            _log = log;
            _camera = camera;
            _pointerOverUi = pointerOverUi;
            _showReport = showReport;
        }

        /// <summary>Что показать на панели «в мире»; пусто — ничего.</summary>
        public string Status => _presenter != null ? _presenter.Status : string.Empty;

        /// <summary>Раз в кадр: <paramref name="entities"/> — мир текущего входа (null — клиента нет).</summary>
        public void Update(bool inWorld, EntityStore entities)
        {
            if (!inWorld || entities == null)
            {
                Close();
                return;
            }

            if (_presenter != null && _presenter.Entities != entities)
            {
                Close();
            }

            _presenter ??= Open(entities);
            _presenter.Update();
        }

        public void Dispose()
        {
            Close();
            _data?.Dispose();
            _data = null;
        }

        private DebugMapPresenter Open(EntityStore entities)
        {
            EnsureData();
            var builder = _data == null ? null : new SceneBuilder(_data.Map, _data.Tiles, _data.Multis, entities);
            var inspector = _data == null ? null : new CellInspector(_data.Map, _data.Tiles, _data.Multis, entities);
            var presenter = new DebugMapPresenter(entities, builder, inspector, _camera, _pointerOverUi, ShowReport);
            if (_data == null)
            {
                _log.Warning("Отладочная карта без земли: " + _dataError);
            }

            return presenter;
        }

        // Окно клетки показывается на экране и пишется в журнал Unity (Console), откуда текст можно скопировать.
        private void ShowReport(string text)
        {
            _log.Info(text);
            _showReport(text);
        }

        private void Close()
        {
            _presenter?.Dispose();
            _presenter = null;
        }

        private void EnsureData()
        {
            if (_dataTried)
            {
                return;
            }

            _dataTried = true;
            var folder = PlayerPrefs.GetString(FolderKey, DefaultFolder());
            _data = UnityWorldData.Load(folder, _log, out _dataError);
            if (_data == null)
            {
                _dataError = "файлы данных (" + folder + "): " + _dataError;
            }
        }

        // Assets → The Verge → client → remaster: три шага вверх от папки Assets.
        private static string DefaultFolder() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "uo-data", "Ultima Online Classic"));
    }
}
