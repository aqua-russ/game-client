using System.Threading;
using TheVerge.Core.Session.Screens;
using TheVerge.Presentation.UI.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Точка входа сцены Bootstrap.unity (AGENTS.md §2.1): связывает экраны входа (<see cref="LoginScreens"/>, ядро) с их
    /// разметкой UI Toolkit (<see cref="LoginScreensView"/>) и каждый кадр даёт ядру тик с бюджетом времени
    /// (ClientLimits.TickBudgetMs). Ссылки на разметку и тему ставит меню «The Verge → Prepare Bootstrap Scene».
    /// Имя класса и файл не меняются: на них ссылается сцена.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static readonly Vector2Int ReferenceResolution = new Vector2Int(1280, 720);

        [SerializeField] private VisualTreeAsset _screensLayout = null;
        [SerializeField] private ThemeStyleSheet _theme = null;

        private readonly UnityClock _clock = new UnityClock();
        private CancellationTokenSource _stop;
        private PanelSettings _panel;
        private LoginScreens _screens;
        private LoginScreensView _view;

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
            var log = new UnityLog();
            var stop = _stop.Token;
            _screens = new LoginScreens(() => UnityLoginClient.Create(_clock, log, stop));
            LoginPrefs.Load(_screens.Form);
            _screens.Submitted += OnSubmitted;
            _view = new LoginScreensView(CreateDocument().rootVisualElement, _screens);
            _view.Refresh();
        }

        private void Update()
        {
            _screens.Tick(_clock.NowMs);
            _view.Refresh();
        }

        private void OnDestroy()
        {
            if (_screens == null)
            {
                return;
            }

            _screens.Submitted -= OnSubmitted;
            _view?.Dispose();
            _screens.Dispose();
            _stop.Cancel();
            _stop.Dispose();
            Destroy(_panel);
        }

        private void OnSubmitted() => LoginPrefs.Save(_screens.Form);

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
