# The Verge — Изометрический клиент на Unity 6

Чистый старт клиентской части игры на базе протокола Ultima Online (ModernUO).

## Архитектура проекта
- **Core (`Assets/TheVerge/Core/`):** интерфейсы клиент-серверных адаптеров (`ISessionAdapter`, `IMovementAdapter`, `IWorldEntityAdapter`, `IInventoryAdapter`, `IGameplayActionsAdapter`, `IStatsAdapter`), все DTO-модели, примитивы (`Serial`, `Point3D`, `Direction`, `Layer`, `SkillType`, `SkillInfo`), маппер координат, модель статов `PlayerStatsModel` и автономный симулятор `LocalWorldSimulator`.
- **Network (`Assets/TheVerge/Network/`):** реализация сетевого адаптера `ModernUONetworkAdapter`, протокол UO 7.0.95.0, сжатие Хаффмана, таблица длин пакетов, билдер создания персонажа (0xF8), процессоры пакетов авторизации, мира, движения, боя, инвентаря, диалогов и статов, а также встроенный сервер-эмулятор `ModernUOLoopbackServer`.
- **Presentation (`Assets/TheVerge/Presentation/`):**
  - **Авторизация и персонаж (`UI/Auth/`):** экраны логина (`LoginView`), выбора персонажа (`CharacterSelectView`), создания персонажа (`CharacterCreationView`), панели 52 навыков (`CharacterCreationSkillsPanel`), UI-тема (`AuthUiTheme`) и оркестратор (`AuthAndCharacterUiPresenter`).
  - **Системы:** меню выхода по Escape (`InGameEscapeMenuModal`), контроль бездействия AFK (`InactivityAfkController`), сетевой HUD (`NetworkStatusHudPresenter`), презентер статов (`PlayerStatsPresenter`) и диспетчер потоков (`MainThreadDispatcher`).
  - **Точка входа (`Bootstrap/GameBootstrap.cs`):** автоматическое обнаружение живого сервера ModernUO (порт 2593) либо прозрачный запуск локального loopback-сервера (порт 2594), инициализация адаптеров и показ окна авторизации.
- **Scenes (`Assets/TheVerge/Scenes/`):** стартовая сцена `Bootstrap.unity`.
- **Editor (`Assets/TheVerge/Editor/`):** утилита `BootstrapBuild.cs` (меню `The Verge -> Prepare Bootstrap Scene`).

## Запуск
1. Откройте Unity Hub.
2. Нажмите **Add** (Добавить проект из диска) и выберите папку `The Verge` (версия Unity 6000.6.8f1).
3. Откройте сцену `Assets/TheVerge/Scenes/Bootstrap.unity` и нажмите **Play**.

## Документы
- `AGENTS.md` — правила для ИИ-агентов и код-стайл.
- `PROGRESS.md` — статус и хроника; `docs/PLAN.md` — план стадий 0–9.
- `docs/TARGET.md` — версия клиента 7.0.95.0, эра Renaissance (только Фелукка), пакеты и лимиты сервера.
- `docs/adr/` — принятые решения; `docs/server-patches.md` — правки сервера ModernUO.

Для полноценной работы нужны файлы данных классического клиента UO 7.0.95.0 (свои, в репозитории их нет) — см. `docs/adr/0003-world-data.md`.
