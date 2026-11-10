# AGENTS.md — правила разработки для ИИ-агента

## 1. Проект
- **Remaster** — изометрическая 2D MMORPG по правилам Ultima Online (эпоха AoS) со славянским колоритом.
- **Клиент:** Unity 6 (`6000.6.8f1`), 2D Isometric / 2.5D (ортографическая камера, `SpriteRenderer`, Unity Custom Axis Sorting), папка `client-unity/`.
- **Сервер:** модифицированный **ModernUO** (.NET 10, C#), отдельный репозиторий `../../server/ModernUO` (`127.0.0.1:2593`).
- **Границы:** клиент и сервер — отдельные процессы без общих DLL. Взаимодействие строго через бинарный TCP-протокол UO (клиент `7.0.95.0`, кодек Хаффмана).
- **Авторитетность:** сервер — единственный источник истины (координаты, коллизии, тайминги, урон, крафт, вес, права, БД). Клиент отвечает только за ввод, предсказание/интерполяцию шага, локальный кэш состояния по `Serial`, 2D-спрайтовый рендер, звук и адаптивный UI.
- **Источники правил игры:**
  - Механики, формулы, баланс — только в [`docs/GDD.md`](docs/GDD.md) (и `docs/gdd/`).
  - Управление и интерактив — только в [`docs/CONTROLS.md`](docs/CONTROLS.md) (способности `Q, E, R, C, V`, пояс расходников `1–5`).
  - Запрещено выдумывать или дублировать в коде правила, противоречащие этим документам. Если правила нет или есть противоречие — остановись и спроси пользователя.

---

## 2. Формат ответа пользователю (АБСОЛЮТНЫЙ ПРИОРИТЕТ)
Пользователь — руководитель проекта без опыта программирования.
- **КАТЕГОРИЧЕСКИ ЗАПРЕЩЕНО** выводить код в чат или перегружать ответ техническими деталями (байты, потоки, `Span<byte>`, `.asmdef`, названия паттернов). Все изменения агент вносит в файлы проекта самостоятельно.
- Каждый ответ обязан состоять строго из **3 пунктов**:
  1. **Что сейчас сделано:** 1–3 предложения простым языком, какая игровая возможность появилась или исправлена (если менялся сервер — указать это в одной фразе).
  2. **Что нажать в Unity (Пошагово):** простая инструкция для редактора (`Hierarchy`, `Project`, `Inspector`). Все связки делать кодом в `GameBootstrap`, чтобы в идеале шаг сводился к «Откройте сцену `Bootstrap` и нажмите Play».
  3. **Как проверить физически:** четкий сценарий проверки глазами в игре после нажатия Play.

---

## 3. Порядок работы агента над задачей (Workflow)
1. **Контекст:** прочитай [`PROGRESS.md`](PROGRESS.md), чтобы видеть, что уже реализовано, и не ломать готовые системы, и [`docs/WORKFLOW.md`](docs/WORKFLOW.md) — чек-лист шагов (текущий шаг помечен 🔶; после проверки отмечай `[x]`).
2. **Спецификация:** сверь задачу с [`docs/GDD.md`](docs/GDD.md) и [`docs/CONTROLS.md`](docs/CONTROLS.md).
3. **Изоляция изменений (Scope Isolation):** меняй только файлы решаемой задачи. Запрещено трогать чужие рабочие модули, `LoginPacketProcessor` или менять сигнатуры существующих публичных событий/адаптеров без прямого поручения.
4. **Проверка сервера:** если нужен новый сетевой пакет или команда — сначала проверь обработчик в `../../server/ModernUO`. Вся игровая логика сервера крутится в одном потоке (`Core.LoopContext`); основные правки контента — в `Projects/UOContent/`.
5. **Разработка снизу вверх (Bottom-Up):**
   - Шаг 1: DTO / структуры событий (`readonly struct`) и чистый класс `Model` в `Core` или модуле фичи.
   - Шаг 2: контракт адаптера в `Core/Adapters` + парсер/билдер пакета в `Network/Processors`.
   - Шаг 3: `Presenter` (с `IDisposable`) и `View` (`MonoBehaviour` на префабе), регистрация в `GameBootstrap`.
6. **Проверка сборки:** если редактор Unity закрыт — запусти batchmode-проверку (`scripts/prepare-3d.sh` или вызов Unity CLI с `-batchmode -nographics -quit`).
7. **Фиксация прогресса:** обнови [`PROGRESS.md`](PROGRESS.md) (§11) и пройди чек-лист самопроверки (§12) перед ответом.

---

## 4. Технологический стек
- **Движок и язык:** Unity 6 LTS (`6000.6.8f1`), 2D Isometric / 2.5D, C# 10+ (`.NET Standard 2.1`).
- **Камера:** ортографическая (`Camera.orthographic = true`), фиксированный угол изометрии.
- **Рендеринг:** `SpriteRenderer`, `SpriteAtlas`, Unity Custom Axis Sorting (сортировка слоев и глубины).
- **UI:** `uGUI` (`UnityEngine.UI`) + `TextMeshPro` (`TMP_Text`, `TMP_InputField`). Строгий запрет на `OnGUI`, устаревший `UnityEngine.UI.Text` и старый 3D `TextMesh`.
- **Ввод:** только `UnityEngine.InputSystem` (New Input System). Устаревший `UnityEngine.Input` запрещен.
- **Память и спавн:** `UnityEngine.Pool.ObjectPool<T>` для динамических спрайтовых сущностей, эффектов и ячеек UI.
- **Конфиги и каталоги:** `ScriptableObject`, кэшируемые при старте в `Dictionary<TKey, TValue>` для поиска за $O(1)$.
- **Пакеты Unity:** не добавлять новые зависимости в `Packages/manifest.json` без согласования.

---

## 5. Архитектура и структура папок
Код клиента в `client-unity/Assets/UO/` разделен на изолированные сборки (`.asmdef`) по принципу **MVP (Model-View-Presenter) + Адаптеры + EventBus**:

```text
Assets/UO/
├── Core/                               # Remaster.Core.asmdef (noEngineReferences: true — БЕЗ UnityEngine!)
│   ├── Adapters/                       # Интерфейсы фасадов (ISessionAdapter, IMovementAdapter, IWorldEntityAdapter,
│   │                                   # IInventoryAdapter, IGameplayActionsAdapter, IStatsAdapter) и структуры *Dto
│   ├── Coordinates/                    # CoordinateMapper (Server X,Y,Z <-> изометрические координаты Unity)
│   ├── Events/                         # EventBus и структуры событий (readonly struct) для связи между фичами
│   ├── Movement/                       # MovementSequenceQueue (Sequence 0..255)
│   ├── Primitives/                     # Serial, Point3D, Direction, Layer, SkillInfo, SkillType
│   ├── State/                          # Состояния активности и модели (PlayerStatsModel)
│   ├── Threading/                      # IMainThreadDispatcher
│   ├── World/                          # Данные срезов карты (MinocMapSliceData)
│   └── Simulators/                     # LocalWorldSimulator (автономный режим)
│
├── Network/                            # Remaster.Network.asmdef (noEngineReferences: true — БЕЗ UnityEngine!)
│   ├── Compression/                    # HuffmanCodec (декомпрессия входящего потока сервера)
│   ├── Protocol/                       # PacketLengthTable (длины пакетов 7.0.95.0), билдеры пакетов
│   ├── Processors/                     # Парсеры и билдеры пакетов по доменам (*PacketProcessor.cs)
│   ├── Loopback/                       # ModernUOLoopbackServer (встроенный локальный TCP-сервер для тестов)
│   └── ModernUONetworkAdapter.cs       # Асинхронный TCP-клиент и реализация фасадов Core
│
├── Shared/                             # ScriptableObject-каталоги (GraphicCatalog, CreatureCatalog), пулы
│
├── Features/                           # Изолированные модули фич (Vertical Slices)
│   └── <FeatureName>/                  # Папка фичи (Model/, Presenter/, View/, Prefabs/)
│
├── Presentation/                       # Remaster.Presentation.asmdef (Точка входа, сцена, камера, презентеры)
│   ├── Bootstrap/                      # GameBootstrap (Composition Root), NetworkStatusHudPresenter, PlayerStatsPresenter
│   ├── Threading/                      # MainThreadDispatcher (ConcurrentQueue<Action> в Update)
│   ├── Camera/                         # IsometricCameraController (ортографическое слежение за игроком)
│   ├── Movement/                       # PlayerMovementController (WASD, интерполяция шага, Direction)
│   ├── Interaction/                    # WorldEntityView, WorldMobileMarker, WorldItemMarker
│   ├── Graybox/                        # WorldEntitiesPresenter (ObjectPool мобов и предметов мира)
│   └── UI/                             # Auth (LoginView, CharacterSelectView, CharacterCreationView), Modals
│
├── Prefabs/                            # Готовые префабы UI Canvas, спрайтовых сущностей и эффектов
├── Art/                                # Спрайты, спрайтшиты, атласы, шрифты TextMeshPro
├── Editor/                             # Remaster.Editor.asmdef (BootstrapBuild и утилиты редактора)
└── Scenes/                             # Bootstrap.unity (стартовая сцена)
```

### Правила слоев и жизненного цикла:
1. **`Core` и `Network` — чистый C#:** флаг `noEngineReferences: true`. Любой `using UnityEngine` запрещен.
2. **Поток данных:** Фоновый TCP-поток читает пакет $\rightarrow$ собирает `readonly struct` DTO $\rightarrow$ передает через `IMainThreadDispatcher.Enqueue` в главный поток $\rightarrow$ вызывает событие адаптера $\rightarrow$ `Presenter` обновляет `Model` и `View`.
3. **Разделение MVP:**
   - `Model`: чистый C#-класс без `UnityEngine`. Хранит состояние по `Serial (uint)` и рассылает события изменений.
   - `Presenter`: чистый C#-класс (`IDisposable`). Связывает события адаптеров, `Model` и `View`.
   - `View`: `MonoBehaviour` на **префабе**. Отвечает только за отображение (`SpriteRenderer`, `Image`, `TMP_Text`, `Animator`) и передачу ввода в `Presenter`. Запрещена сетевая логика и расчеты внутри `View`.
4. **Строгий запрет на процедурный UI из C#:** все окна и панели создаются как **префабы в редакторе Unity**. Запрещено собирать UI кодом через `new GameObject()`, `AddComponent<RectTransform>()` и ручные смещения пикселей.
5. **Единая точка входа (`GameBootstrap`):** все сервисы, сетевой адаптер и презентеры регистрируются кодом в `GameBootstrap`. Глобальные синглтоны запрещены.

---

## 6. Многопоточность, асинхронность и отказоустойчивость
- **Запрет Unity API вне Main Thread:** любые обращения к `Transform`, `GameObject`, `Instantiate`, `SpriteRenderer`, `UI` из фонового сокета запрещены.
- **Контроль асинхронности:** запрещены неуправляемые `Task.Run` и бесконечные корутины. Любые асинхронные таймеры обязаны принимать `CancellationToken`.
- **Отказоустойчивость парсера пакетов:** ошибка чтения одного пакета известной длины не должна ронять клиент — буфер пропускается по длине из `PacketLengthTable`. При неизвестном ID — сессия штатно закрывается через `OnSessionError`.

---

## 7. Контракт и ограничения сервера ModernUO
Сервер `ModernUO` имеет жесткие протокольные и античит-ограничения, которые клиент обязан соблюдать:

1. **Порядок байтов (Big-Endian):** все многобайтовые числа (`ushort`, `short`, `uint`, `int`) в пакетах читаются и пишутся строго в Big-Endian через `BinaryPrimitives`.
2. **Версия клиента и сжатие:**
   - Клиент представляется версией `7.0.95.0` (пакет `0xEF` Seed 21 байт + пакет `0xBD` Client Version).
   - Поток Клиент $\rightarrow$ Сервер идет без сжатия. Поток Сервер $\rightarrow$ Клиент сжат кодом Хаффмана (`HuffmanCodec`).
   - Вход идет в 2 фазы: авторизация (`0xEF` + `0x80` $\rightarrow$ `0xA8` $\rightarrow$ `0xA0` $\rightarrow$ релей `0x8C` с `AuthKey`) и подключение к игровому порту (`AuthKey` + `0x91` $\rightarrow$ `0x5D` выбор героя).
3. **Движение, Sequence Key и античит `MovementThrottle` (`0x02`, `0x21`, `0x22`):**
   - Каждый шаг отправляется пакетом `0x02` (7 байт) с номером `Sequence`: первый шаг равен **`0`**, далее **`1..255`**, а после `255` заворачивается в **`1`** (не в `0`!).
   - На клиенте разрешено не более **5** неподтвержденных шагов (`MovementSequenceQueue`).
   - Серверные интервалы движения: шаг пешком `400 мс`, бег `200 мс`, поворот на месте `0 мс`. Клиент обязан выдерживать эти тайминги, иначе сервер отклонит шаг.
   - При получении `0x21 Move Reject` клиент мгновенно синхронизирует позицию с серверной `(X, Y, Z)` и сбрасывает `MovementSequenceQueue.Reset()` (`Sequence = 0`).
4. **Инвентарь, Lift/Drop и перехват окон:**
   - Перемещение предмета: `0x07 Lift` $\rightarrow$ `0x08 Drop`. Никакого оптимистичного переноса до ответа сервера; при `0x27 Lift Reject` предмет возвращается на место.
   - Пакет `0x08 Drop` в клиенте `7.0.95.0` имеет длину **15 байт**.
   - Старые серверные Gump-окна (`0xB0`, `0xDD`) не рендерятся по серверной верстке: данные читаются в DTO и выводятся через современные префабы `uGUI + TextMeshPro`, а ответ отправляется пакетом `0xB1`.

---

## 8. Изометрическая 2D-проекция, координаты и спрайты
1. **Система координат (`CoordinateMapper`):**
   - Серверная сетка: целочисленные `(X, Y)` и высота `Z` (`sbyte`). 1 клетка = 1.0 единица в координатах мира.
   - Координата $Z$ сервера задает вертикальный подъем спрайта на экране (`ScreenY` / `Unity.Y`).
   - Поворот персонажа по 8 сторонам света — строго через `CoordinateMapper.DirectionToEulerY(direction)`.
2. **Сортировка глубины слоев (Depth Sorting):**
   - Настройка сортировки в Unity: `Custom Axis Sorting` (вектор `(0, 1, -0.26)` или сортировка по Y), чтобы объекты снизу экрана и объекты с меньшим $Z$ корректно перекрывались объектами сверху экрана.
   - Скрытие крыш и верхних этажей при входе игрока в здание реализуется через управление `SortingGroup` / прозрачностью спрайтов.
3. **Спрайтовый пайплайн и каталоги:**
   - Соответствие `GraphicId` $\leftrightarrow$ `Sprite` предметов и `BodyId` $\leftrightarrow$ аниматоры мобов хранится в `ScriptableObject`-каталогах (`GraphicCatalog`, `CreatureCatalog`). Если спрайт не найден — выводится безопасная заглушка (*Placeholder*).
   - Все динамические сущности мира (`Mobile`, `WorldItem`, спецэффекты) берутся из `ObjectPool<T>` и возвращаются в пул по пакету удаления `0x1D`.
   - Числовые параметры (HP, мана, стамина) обновляются в модели и UI мгновенно по приходу пакета.

---

## 9. Правила UI и взаимодействия с мышью
- **Блокировка сквозных кликов:** любой клик мыши в игровой мир обязан начинаться с проверки `EventSystem.current.IsPointerOverGameObject()`. Если курсор над UI — действие в мир блокируется.
- **Адаптивность и Raycast Target:** все `Canvas` используют `Scale With Screen Size`. На декоративных `Image` и `TMP_Text`, не принимающих клики, флаг `Raycast Target` выключен (`false`).
- **Стек окон и клавиша Esc:** нажатие `Esc` работает иерархически (прервать каст $\rightarrow$ закрыть модалку/окно $\rightarrow$ сбросить таргет). При шаге на `WASD` окна внешних сундуков/трупов закрываются при отходе дальше 2 клеток.

---

## 10. Код-стайл и жесткие ограничения (Code Guardrails)
- **Лимиты размера:**
  - Новый `.cs` файл — не более **150–200 строк**, строго **1 тип** на файл. Имя файла = имя типа.
  - Метод — не более **25–30 строк**, вложенность `if/for/foreach` — не глубже **2** (использовать `Guard Clauses`).
  - Старые файлы запрещено раздувать — любую новую логику выносить в отдельные небольшие классы.
- **Именование и сериализация:**
  - Обязательный `namespace` в каждом файле (`Remaster.Core`, `Remaster.Network`, `Remaster.Presentation`, `Remaster.Features.<Name>`).
  - Классы, методы, свойства — `PascalCase`; приватные поля — `_camelCase`; аргументы — `camelCase`.
  - Поля для инспектора — только `[SerializeField] private`. Магические числа запрещены (выносить в `const` или `ScriptableObject`).
- **Технические запреты в рантайме:**
  - **ЗАПРЕЩЕНО:** `GameObject.Find`, `Transform.Find`, `FindObjectOfType`, `SendMessage`.
  - **ЗАПРЕЩЕНО:** вызывать `GetComponent<T>()`, использовать LINQ (`Where`, `Select`), аллоцировать ссылочные типы (`new List<T>()`) внутри `Update()`, `FixedUpdate()`, `LateUpdate()` и в цикле чтения сокета.
- **Безопасность подписок:** каждая подписка `+=` в `OnEnable()` / `Initialize()` обязана иметь зеркальную отписку `-=` в `OnDisable()` / `Dispose()`.

---

## 11. Ведение `PROGRESS.md` и Git
- После завершения каждой задачи обнови [`PROGRESS.md`](PROGRESS.md): актуализируй статус затронутой подсистемы в таблице и добавь краткую запись (1–3 строки) в хронику.
- В Git никогда не коммитить `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.DS_Store` и архивы `.zip`. Каждый файл в `Assets/` обязан иметь парный `.meta`-файл. Коммиты делать только по просьбе пользователя в формате `[Модуль] Описание`.

---

## 12. Чек-лист самопроверки перед ответом
- [ ] Проверены ли [`PROGRESS.md`](PROGRESS.md), [`docs/GDD.md`](docs/GDD.md) и [`docs/CONTROLS.md`](docs/CONTROLS.md)?
- [ ] Новые файлы $\le 200$ строк (1 тип на файл), методы $\le 30$ строк?
- [ ] В `Core` и `Network` нет ссылок на `UnityEngine`, а из фонового потока нет вызовов Unity API?
- [ ] Соблюдены ли Big-Endian, `Sequence 0..255` (`255 -> 1`) и длины пакетов сервера?
- [ ] UI выполнен через префабы и `TextMeshPro`, а не процедурный C#-код?
- [ ] Нет ли в `Update()` вызовов `GetComponent`, LINQ, аллокаций и заблокирован ли клик сквозь UI (`IsPointerOverGameObject`)?
- [ ] Все ли подписки `+=` имеют отписку `-=`, а асинхронные операции — `CancellationToken`?
- [ ] Обновлен ли [`PROGRESS.md`](PROGRESS.md)?
- [ ] Ответ пользователю состоит строго из **3 простых пунктов** без кода и технического жаргона?
