using System.Threading;
using TheVerge.Core.Session.Screens;
using TheVerge.Presentation.UI;
using TheVerge.Presentation.UI.Movement;
using TheVerge.Presentation.UI.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Точка входа сцены Bootstrap.unity (AGENTS.md §2.1): связывает экраны входа (<see cref="LoginScreens"/>, ядро) с их
    /// разметкой UI Toolkit (<see cref="LoginScreensView"/>), отладочную карту и ходьбу (<see cref="DebugMapHost"/>) и каждый
    /// кадр даёт ядру тик с бюджетом времени (ClientLimits.TickBudgetMs): сначала клавиши, потом тик, потом картинка.
    /// Ссылки на разметку и тему ставит меню «The Verge → Prepare Bootstrap Scene». Имя класса и файл не меняются: на них
    /// ссылается сцена.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static readonly Vector2Int ReferenceResolution = new Vector2Int(1280, 720);

        [SerializeField] private VisualTreeAsset _screensLayout = null;
        [SerializeField] private ThemeStyleSheet _theme = null;

        private readonly UnityClock _clock = new UnityClock();
        private CancellationTokenSource _stop;
        private PanelSettings _panel;
        private UnityLog _log;
        private WorldDataSource _data;
        private LoginScreens _screens;
        private LoginScreensView _view;
        private DebugMapHost _debugMap;
        private UnityWorld _world;

        private void Awake()
        {
            if (_screensLayout == null || _theme == null)
            {
                Debug.LogError("[The Verge] У GameBootstrap не заданы разметка экранов или тема UI. Выполните меню " +
                               "The Verge → Prepare Bootstrap Scene и сохраните сцену.");
                enabled = false;
                return;
            }

            _stop = new CancellationTokenSource();
            _log = new UnityLog();
            _data = new WorldDataSource(_log);
            var stop = _stop.Token;
            _screens = new LoginScreens(() => NewClient(stop));
            LoginPrefs.Load(_screens.Form);
            _screens.Submitted += OnSubmitted;
            var root = CreateDocument().rootVisualElement;
            _view = new LoginScreensView(root, _screens);
            var ui = new WorldUiHooks(_view.IsPointerOverUi, ShowCellReport, () => _view.IsTextInputFocused,
                new RunIndicatorView(root));
            _debugMap = new DebugMapHost(_log, FindCamera(), _data, ui);
            _view.Refresh();
        }

        private void Update()
        {
            _debugMap.ReadInput();
            _screens.Tick(_clock.NowMs);
            _debugMap.Update(_screens.Screen == LoginScreen.InWorld, _world, _clock.NowMs);
            _view.SetMapStatus(_debugMap.Status);
            _view.Refresh();
        }

        private void OnDestroy()
        {
            if (_screens == null)
            {
                return;
            }

            _screens.Submitted -= OnSubmitted;
            _debugMap?.Dispose();
            _view?.Dispose();
            _screens.Dispose();
            _data.Dispose();
            _stop.Cancel();
            _stop.Dispose();
            Destroy(_panel);
        }

        private void OnSubmitted() => LoginPrefs.Save(_screens.Form);

        // Окно клетки показывается на экране и пишется в журнал Unity (Console), откуда текст можно скопировать.
        private void ShowCellReport(string text)
        {
            _log.Info(text);
            _view.ShowCellReport(text);
        }

        // Каждый вход — новый клиент и новый мир; карта и ходьба следят за миром текущего входа. Файлы данных читаются при
        // первом входе и дальше общие.
        private LoginParts NewClient(CancellationToken stop)
        {
            var parts = UnityLoginClient.Create(_clock, _log, stop, _data.Load(), out var world);
            _world = world;
            return parts;
        }

        // Камера сцены (Main Camera); если её нет — своя. Положение и вид ей задаёт отладочная карта.
        private static Camera FindCamera()
        {
            var main = Camera.main;
            if (main != null)
            {
                return main;
            }

            var created = new GameObject("Main Camera", typeof(Camera));
            created.tag = "MainCamera";
            return created.GetComponent<Camera>();
        }

        // Панель создаётся кодом, а не ассетом: настройки панели — здесь, в сцене — только ссылки на разметку и тему.
        private UIDocument CreateDocument()
        {
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.themeStyleSheet = _theme;
            _panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panel.referenceResolution = ReferenceResolution;
            var host = new GameObject("LoginScreens");
            host.SetActive(false);
            host.transform.SetParent(transform, false);
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = _panel;
            document.visualTreeAsset = _screensLayout;
            host.SetActive(true);
            return document;
        }
    }
}
