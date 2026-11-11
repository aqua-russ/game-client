using TheVerge.Core;
using TheVerge.Network;
using TheVerge.Presentation;
using UnityEngine;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Главная точка входа в проект на сцене Bootstrap.unity.
    /// Автоматически определяет наличие запущенного сервера ModernUO (127.0.0.1:2593).
    /// Если сервер выключен — поднимает встроенный локальный TCP-сервер с поддержкой протокола UO и Хаффмана.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Настройки сервера The Verge")]
        [SerializeField] private string _serverHost = "127.0.0.1";
        [SerializeField] private int _serverPort = 2593;

        private MainThreadDispatcher _dispatcher;
        private NetworkStatusHudPresenter _hudPresenter;
        private PlayerStatsPresenter _playerStatsPresenter;
        private PlayerStatsModel _playerStatsModel;
        private AuthAndCharacterUiPresenter _authUiPresenter;
        private InactivityAfkController _afkController;
        private ModernUOLoopbackServer _loopbackServer;

        private ISessionAdapter _sessionAdapter;
        private IMovementAdapter _movementAdapter;
        private IWorldEntityAdapter _worldEntityAdapter;
        private IInventoryAdapter _inventoryAdapter;
        private IGameplayActionsAdapter _gameplayAdapter;
        private IStatsAdapter _statsAdapter;
        private ModernUONetworkAdapter _networkAdapter;

        public ISessionAdapter Session => _sessionAdapter;
        public IMovementAdapter Movement => _movementAdapter;
        public IWorldEntityAdapter World => _worldEntityAdapter;
        public IInventoryAdapter Inventory => _inventoryAdapter;
        public IGameplayActionsAdapter Gameplay => _gameplayAdapter;
        public IStatsAdapter Stats => _statsAdapter;
        public PlayerStatsModel PlayerStats => _playerStatsModel;

        private void Awake()
        {
            // 1. Создаем диспетчер главного потока
            _dispatcher = gameObject.AddComponent<MainThreadDispatcher>();

            // 2. Инициализируем сетевой адаптер (всегда работаем через сеть)
            _networkAdapter = new ModernUONetworkAdapter(_dispatcher);
            _sessionAdapter = _networkAdapter;
            _movementAdapter = _networkAdapter;
            _worldEntityAdapter = _networkAdapter;
            _inventoryAdapter = _networkAdapter;
            _gameplayAdapter = _networkAdapter;
            _statsAdapter = _networkAdapter;

            // 3. Подписываемся на события сессии для логов
            _sessionAdapter.OnPlayerEnteredWorld += OnPlayerEnteredWorld;
            _sessionAdapter.OnSessionError += error => Debug.LogError($"[Bootstrap] Ошибка сессии: {error}");
            _sessionAdapter.OnDisconnected += () =>
            {
                Debug.LogWarning("[Bootstrap] Сессия завершена");
            };

            _worldEntityAdapter.OnMobileSpawned += mob => Debug.Log($"[World] Появился моб: Serial={mob.Serial}, Имя={mob.Name}, Body={mob.BodyId}, Pos={mob.Position}");
            _worldEntityAdapter.OnWorldItemSpawned += item => Debug.Log($"[World] Появился предмет на земле: Serial={item.Serial}, Graphic=0x{item.GraphicId:X4}, Pos={item.WorldPosition}");

            // 4. Модель характеристик персонажа (MVP Model)
            _playerStatsModel = new PlayerStatsModel();

            // 5. Создаем экранный монитор сетевого статуса (Network HUD) и модель характеристик (Шаг 1.2)
            _hudPresenter = gameObject.AddComponent<NetworkStatusHudPresenter>();
            _hudPresenter.Initialize(_networkAdapter, isUsingSimulator: false);

            _playerStatsPresenter = new PlayerStatsPresenter(
                _statsAdapter,
                _playerStatsModel,
                stats => _hudPresenter?.UpdatePlayerStats(stats));

            // 6. Создаем презентер авторизации и создания персонажа (Шаг 1.0)
            _authUiPresenter = gameObject.AddComponent<AuthAndCharacterUiPresenter>();

            // 7. Подключаем контроллер обнаружения бездействия (AFK Timeout)
            _afkController = gameObject.AddComponent<InactivityAfkController>();
        }

        private void Start()
        {
            // Проверяем, запущен ли внешний сервер ModernUO на порту 2593
            bool isLiveServerRunning = IsPortOpen(_serverHost, _serverPort, 300);
            string host = isLiveServerRunning ? _serverHost : "127.0.0.1";
            int port = isLiveServerRunning ? _serverPort : StartLoopbackServer();

            if (isLiveServerRunning)
            {
                Debug.Log($"[Bootstrap] 🟢 Обнаружен живой сервер ModernUO ({host}:{port}). Запуск интерфейса The Verge...");
            }

            _authUiPresenter.Initialize(_sessionAdapter, host, port);
            _afkController.Initialize(
                onAfkTimeout: () => _authUiPresenter.HandleAfkTimeout(),
                isConnectedCheck: () => _sessionAdapter != null && _sessionAdapter.IsConnected && _sessionAdapter.HasEnteredWorld
            );
        }

        private int StartLoopbackServer()
        {
            _loopbackServer = ModernUOLoopbackServer.Start(2594);
            Debug.Log($"[Bootstrap] 🟡 Сервер ModernUO на порту {_serverPort} не запущен. Автоматически запущен встроенный Loopback TCP-сервер (порт {_loopbackServer.Port}) с протоколом UO и Хаффманом.");
            return _loopbackServer.Port;
        }

        private static bool IsPortOpen(string host, int port, int timeoutMs)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var result = client.BeginConnect(host, port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(timeoutMs);
                if (!success) return false;
                client.EndConnect(result);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnPlayerEnteredWorld(PlayerLoginConfirmDto player)
        {
            var (ux, uy, uz) = CoordinateMapper.ToWorld3D(player.Position);
            Debug.Log($"[Bootstrap] ИГРОК ВОШЁЛ В МИР! Serial={player.PlayerSerial}, Серверная точка={player.Position}, Позиция=({ux:F1}, {uy:F1}, {uz:F1}), Направление={player.Direction}");
        }

        private void OnDestroy()
        {
            _playerStatsPresenter?.Dispose();
            _sessionAdapter?.Disconnect();
            _loopbackServer?.Dispose();
            _loopbackServer = null;
        }
    }
}
