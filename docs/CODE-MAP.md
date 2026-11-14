# CODE-MAP — где что в коде клиента

Карта готовых возможностей: где лежит код, как его расширять, что найдено по коду сервера. Статус задач — `PROGRESS.md`; правила и структура папок — `AGENTS.md` §2; значения протокола — `docs/TARGET.md`; пакеты — `docs/protocol/packets.csv`. Сделал новую возможность — допиши сюда одну-две строки в свой раздел (`AGENTS.md` §6, п. 7).

## 1. Сеть (Стадия 1: 1.1–1.4а, 1.12; [ADR 0013](adr/0013-network-core.md))

- **Путь входящих байтов:** `TcpConnection` (фоновый поток: подключение, чтение) → `ReceivePipeline` (Huffman → `PacketFramer` → `InboundQueue`) → `NetworkSession` (порт `INetworkSession`: `PacketDispatcher` по номеру, `OutgoingBuffer`) → `GameClient.Tick(now)` (бюджет 5 мс, одна отправка за тик). Таймеры (`ITickHandler`) `GameClient` зовёт после пакетов и до отправки.
- **Обработчик пакета:** `PacketDispatcher.Register(id, …)` или `Register(id, подкоманда, …)`. **Исходящий пакет:** `PacketWriter` в `NetworkSession.Outgoing.Reserve/Commit`.
- **Новый пакет:** строка в `docs/protocol/packets.csv` → `python3 tools/gen/packet_table.py` → `tools/check/all.sh`.
- **Сжатие** на игровом сокете включается `NetworkSession.EnableCompression` до отправки 0x91 (ответ сервера уже сжат). 4 байта ключа перед 0x91 пишутся `OutgoingBuffer.WriteRaw` — только первыми в тике, это не пакет.
- Решения по ядру (пределы, дамп, бюджет тика, таймаут, очередь, порты, неизвестная подкоманда — разрыв) — ADR 0013; значения — `TARGET.md` §3.1 и `ClientLimits`.
- **Лог пакетов:** `Network/Diagnostics` (`PacketLogWriter`, `PacketLogReader`), подключается через `NetworkSession.PacketLog`. Формат — строка на пакет: время, S>C/C>S, номер, подкоманда, имя, длина, пометки, байты; события `CONNECTED`/`CLOSED`; пароль в 0x80/0x91 — `*`.
- **Запись и распаковка сырого сжатого потока** проверены в 0.6 / 1.3 (`CaptureConverter`, `tools/golden/`).

## 2. Вход и сессия (1.5–1.15; [ADR 0016](adr/0016-login-and-session.md))

- **Вход ведёт** `Core/Session/LoginFlow`: состояния `LoginState`, ошибка `LoginError`; общее состояние — `LoginProgress` (`MoveTo`, `Fail`, `FailProtocol`, `Require`). Шарды — `Shards`, выбор `SelectShard`, адрес игрового сервера — `Redirect`.
- **Новый пакет входа:** обработчик в `Network/Packets/Account` + строка в `AccountHandlers.Register` + метод `On…` в `LoginFlow`. Пакет не разбирается — `LoginFlow.OnMalformedPacket` (ошибка протокола, не зависание). Отправка — метод в `IAccountSender`/`AccountSender`.
- **Игровой сервер:** по 0x8C `LoginFlow` подключается сам (`IServerConnector.Connect` → `ServerConnector` создаёт новое соединение, `NetworkSession.Attach` переходит на него; логин-соединение закрыто, его события больше не приходят), затем `SendGameLogin`: включает распаковку, пишет 4 байта ключа, шлёт 0x91; состояние `AwaitingCharacterList`.
- **Персонажи:** `LoginFlow.Characters` (`CharacterSelection`: `Slots`, `Cities`, `Select`, `Create` с проверкой `CharacterCreationRules`, `Delete`; события `SlotsChanged`, `DeleteRejected`).
- **Вход в мир:** `WorldEntry` (`Player` — `Core/World/PlayerState`; после 0x55 шлёт язык и дальность). Ответ на запрос версии в любом состоянии — `ServerRequests`. Служебные пакеты — порт `ISessionSender` (`Network/Packets/Session/SessionSender`), обработчики — `SessionHandlers.Register` (рядом с `LoginFlow`, общий `flow.Progress`).
- **В мире:** `GameSession` — ping раз в 10 с, `LastRttMs`, `Logout` → ответ 0xD1 → `IServerConnector.Disconnect` (без ответа 5 с — закрытие всё равно).
- **Ошибки (1.11а):** 0x53 → `CharacterRejected` (`PopupMessageHandler`); молчание сервера 30 с во время входа → `Timeout` (`LoginWatchdog`, таймер); разрыв в первые 25 с в мире → подсказка `DisconnectHint` (время в мире — `GameSession.MsInWorld`).
- **Повтор входа (1.11б):** `Reconnector` (таймер): паузы `ClientLimits.ReconnectDelaysMs`, `LoginFlow.Restart`, тот же шард (`LoginFlow.SelectedShard`) и персонаж (`CharacterSelection.SelectedName`).
- **Тест диалога без сокета:** `FakeConnection.Reply` (ответ на каждую отправку по порядку).

## 3. Экраны входа в Unity (1.16; [ADR 0017](adr/0017-login-screens-unity.md))

- **Модель экранов — в Core** (`Core/Session/Screens`): `LoginScreens` (экран — `Screen`, строка ожидания — `Status`, «в мире» — `WorldStatus`, итог прошлого входа — `Message`, `Revision` растёт при любом видимом изменении; действия `Submit`, `Cancel`, `ChooseShard`, `OpenCreation`/`CloseCreation`/`Create`, `Logout`; `Tick(now)` зовёт `GameClient.Tick` и закрывает клиент, когда вход кончился без повтора), `LoginForm` (адрес «хост:порт», логин, пароль, `Problem`, `CanSubmit`), `CharacterList` (выбор слота, кнопки, удаление с подтверждением, ответ 0x85), `CharacterForm` (подсказки по частям из `CharacterCreationRules`), `LoginErrorText` (причины словами, общие с ботом), `LoginStatusText`; клиент на одну попытку — `LoginParts`.
- **Unity:** разметка `UI/Session/LoginScreens.uxml`/`.uss`, тема `UI/TheVergeTheme.tss`; View — `Presentation/UI/Session` (`LoginScreensView` и панели), помощники `Presentation/UI/UiElements`, `ButtonList`.
- **Сборка клиента** — `Bootstrap/UnityLoginClient` (как `BotClient`, повтор входа включён). Точка входа — `Bootstrap/GameBootstrap`: панель UI создаётся кодом, ссылки на разметку и тему в сцене ставит меню «The Verge → Prepare Bootstrap Scene». Последний адрес и логин — `LoginPrefs` (PlayerPrefs; пароль — никогда).
- **Новый экран:** модель в Core с тестами, в Unity — разметка и тонкий View.
- **Вход и отладочная 3D-карта мира (2.13)** подключены в Unity через `DebugMapHost` (§4).

## 4. Мир (Стадия 2, часть А: 2.7–2.11, 2.14)

- **Модель мира** — `Core/World/EntityStore`: `UpdateMobile`, `MoveMobile` (0x77; неизвестный — заглушка), `PlaceInWorld`, `PlaceInContainer` (неизвестный контейнер — заглушка), `Equip`, полный список надетого `BeginEquipment`/`EndEquipment`, `Remove` (со всем вложенным; свой персонаж не удаляется), `Reset` (новый вход), `Clear(keepPlayer)`; отказ — `WorldResult`, счётчики — `Metrics`. **`EntityStore.cs` — ровно 300 строк (предел): новое в реестр — только отдельными классами.**
- **Кто рядом:** `EntityStore.Spatial.QueryMobiles`/`QueryItems` (ведётся самим `EntityStore`).
- **Пакеты мира:** `Network/Packets/World`, регистрация `WorldHandlers.Register(dispatcher, store)`; 0xF3, смена карты, окружение — `WorldEnvironment`; удаление по дальности.
- **Контейнеры:** команда `ItemCommands.Use`, порт `IItemSender`.
- **Речь:** `Core/Chat/SpeechCommands` (таймер `GameClient`), порт `ISpeechSender`, 0xAD.
- **Прицел (база):** `Core/Targeting/TargetCursor`, порт `ITargetSender`, 0x6C.
- **Сверка с сервером:** `WorldSnapshot`; сервер — GM-команда `[DumpVisible` (JSON: `player`, `map`, `x`, `y`, `z`, `objects[]`: `serial`, `kind`, `place` world|equipped|container, `graphic`, `hue`, `amount`, `x`, `y`, `z`, `parent`, `layer`; надетое — место 0 и количество 1, в контейнере — Z 0, как знает клиент). Номер карты (`map`) сверяется тоже. Проверки — `tools/check/world.sh` (вход, яблоко, рюкзак, удаление) и `tools/check/parity.sh` (5 точек и смена карты, 2.12).
- **Устойчивость:** фаззинг `WorldFuzzTests`, целостность — `WorldInvariants`.

- **Отладочная карта (2.13а):** Core `Core/MapView` — `SceneBuilder.Build(x, y, радиус, список)` собирает `SceneBlock` (вид `SceneKind`, клетка, `ZBottom`/`ZTop`, класс `CellClass`) из земли (`IMapData`), статики (`ITileData`), частей домов (`IMultiData`, через `MultiBlocks`) и объектов мира; `CellClassifier` — цвет по флагам; `SceneTracker` — когда мир изменился и пора пересобрать. Unity: `Presentation/World` (`CellSpace` — одна клетка = 1 м, 1 Z = 0,1 м; `TerrainPresenter`, `EntityMarkers`, `DebugMapPresenter`), `Presentation/Camera/DebugCamera`, `Bootstrap/DebugMapHost` и `UnityWorldData` (файлы данных; папка — PlayerPrefs `TheVerge.DataFolder`, по умолчанию `remaster/uo-data/Ultima Online Classic`). Бот: действие `scene:[радиус]` (`SceneStep`, итог — `SceneSummary`). Клик (2.13б): `Presentation/World/CellPicking` (Input System, левая кнопка; луч сцены → клетки через `CellSpace.ToCellRay`) → `Core/MapView/CellPicker` (блоки карты + `SceneMarkers` — мобы и предметы на земле; ближайший к камере) → `CellInspector.Describe` (`CellObject` на объект; текст — `CellReport`; данные только Фелукки) → `InspectorPanelView` (панель `inspectorPanel` в `LoginScreens.uxml`) и журнал. Клик над панелью в мир не идёт — `Presentation/UI/PointerOverUiGuard`. Проверка — `tools/check/debugmap.sh`.

## 5. Файлы данных (Стадия 2, часть Б: 2.1–2.4, 2.6, 2.11б)

- **Где файлы:** `remaster/uo-data/Ultima Online Classic` — копия установки классического клиента 7.0.118.46 (вне git, файлы EA). Карта — `map0LegacyMUL.uop`, статика — `staidx0.mul`/`statics0.mul`, `tiledata.mul` нового формата (64-битные флаги, 0x10000 предметов), дома — `MultiCollection.uop` (при нём сервер читает UOP, а не `multi.mul`). Заглушка сервера `Distribution/Data/ClientData` (MUL, tiledata старого формата — 32-битные флаги, 0x8000 предметов, пустой `multi.mul`) — для форматов MUL и быстрых проверок; `multi.mul` старого формата (12 байт на запись) проверяется на файлах, собранных в тестах по коду сервера.
- **Формат папки:** `Data/Files/DataLayoutDetector.Detect(папка, MapDefinition.Felucca, DataOptions)` → `DataDetection` (`Layout` или `Error` словами), `DataLayout.Describe()` — строки для лога; имена без учёта регистра — `DataFolder`; заголовок UOP — `UopHeader`; tiledata — `TileDataFormat.ForLength`; карта — `Data/Map/MapDefinition`.
- **Tiledata:** `Core/Tiles` (`TileFlags`, `LandTileData`, `ItemTileData` + `ItemTileProperties`), порт `Core/Ports/ITileData` (номер по маске, как у сервера); `Data/Tiles/TileDataReader.Load(TileDataFormat, out error)` → `TileDataTable`.
- **Карта:** `Core/Tiles/LandTile` (номер — знаковый short, как у сервера), порт `Core/Ports/IMapData` (`TryGetLand`: false — вне карты); `Data/Map/MapData.Open(MapFiles, кэш, out error)`, блоки — `LandBlocks`, адрес блока в MUL или UOP — `LandBlockLocator` (куски UOP — по первым 4 байтам, как сервер). UOP — `Data/Files/UopIndexReader` + `UopNames` + `UopHash` (lookup3).
- **Статика:** `Core/Tiles/StaticTile` (номер, Z, цвет), `IMapData.TryGetStatics(x, y, список)` — порядок файла, список очищается; `Data/Map/StaticBlocks`; общий кэш «вытесняется самый давний» — `BlockSlots`; чтение «сколько есть» — `Data/Files/StreamRead`.
- **Дома и корабли:** `Core/Multis` (`MultiComponent`, `MultiComponents` — границы по правилам сервера, `From`), порт `Core/Ports/IMultiData` (номер по маске 0x3FFF); `Data/Multis/MultiTable.Load(MultiFiles, out error)`: `MultiMulReader` (16/12 байт), `MultiUopReader` (zlib — `Data/Files/Zlib`: заголовок, deflate, Adler-32).
- **Сверка с сервером:** `tools/check/map.sh <папка> [--self-test]` (сервер — `[DumpMapData`, бот — `mapdump:`): 10 000 клеток, вся tiledata, все multi.
- Dif-патчи (2.5) сервер при версии 7.0.95.0 не применяет (`TileMatrixPatch.Configure`) — задача пропущена.

## 6. Статус, прицел, бой, смерть (Стадия 4: 4.4, 4.6–4.9)

Сообщения сервера пока хранятся номерами; тексты — задача 4.1. Окна статуса и журнала боя в Unity — 4.11. Проверка на сервере — `tools/check/combat.sh` (сценарии `combat-basic` и `death-res` = задача 4.10).

- **Состояние мобов (4.4):** `Mobile.Status` (`Core/World/MobileStatus`: `Vital`, `StatusAttributes`, `StatLocks`); обновления — `Core/Status/MobileStatusUpdates` (событие `Changed`; неизвестный моб — пропуск и счётчик `UnknownMobile`); запрос — `StatusCommands.RequestStatus` → порт `IStatusSender`; `Network/Packets/Status`: 0x11, 0xA1–0xA3, 0x2D, 0x17, 0xBF 0x19, 0x34 (`StatusHandlers.Register`). Сервер — `[DumpMobile`; сверка — `StatusComparison`, отчёт — общий `ParityReport`.
- **Прицел полностью (4.6):** вид курсора — `TargetCursor.IsHarmful`/`IsBeneficial`; быстрые цели — `Core/Targeting/TargetCommands` (`Bandage`, `CastAt`, `UseSkillAt`, `TargetByResource`) → порт `IQuickTargetSender` → `Network/Packets/Targeting/QuickTargetSender` (`QuickTargetPackets`); общая задержка действий — `Core/Session/ActionTimer` (двойной клик и бинт; `ItemCommands` принимает её необязательным параметром). Клиентские надстройки из `CONTROLS.md` (единая цель, предупреждения 2 с, авто-ответ на `0x6C` после каста, удаление `TargetByResource`) добавляются на шаге 7 (Стадия 4).
- **Стойка и атака (4.7):** `Core/Combat/CombatState` (только по пакетам 0x72 и 0xAA, `Reset` при новом входе); `CombatCommands` (`SetWarMode`: та же стойка — не шлём; `Attack`: не моб или свой персонаж — не шлём) → порт `ICombatSender` → `Network/Packets/Combat` (`CombatHandlers.Register`, `CombatSender`). Клиентская надстройка из `CONTROLS.md` (стойка включается сама при атаке; выключается только по `Esc`, автовыхода по времени нет — ADR 0021) добавляется на шаге 7 (Стадия 4).
- **Журнал боя (4.8):** `Core/Combat/CombatLog` — кольцо на `ClientLimits.CombatLogCapacity` = 512 событий `CombatEvent` (вид `CombatEventKind`, кто, по кому, значение, вид эффекта `EffectKind`, точка, цвет, время по часам клиента), событие `Added`, `Clear` при новом входе. Обработчики: `Network/Packets/Combat` (0x2F `SwingHandler`, 0x0B `DamageHandler`, 0x6E `MobileAnimationHandler`; все — `CombatLogHandlers.Register`) и `Network/Packets/Effects` (0x70, 0xC0, 0xC7; общая голова — `EffectHead`); звук 0x54 — через `WorldEnvironment.SoundPlayed` (подписать `CombatLog.OnSound`). Сверка порядка с логом пакетов — `tools/check/combat_order.py`.
- **Смерть и воскрешение (4.9):** своя смерть — `CombatState.OnDeathStatus` (0x2C, `Deaths`, событие `Died`; ответ не шлём); жив ли — по телу сервера: `Mobile.IsGhost` (`Core/World/Bodies.IsGhost`, как `Body.IsGhost` сервера); чужая смерть — `CombatEventKind.Death` в журнале (0xAF: убитый, труп); вещи трупа по слоям — `Core/World/CorpseEquipment` (`CorpseItem`; 0x89 — `Network/Packets/Items/CorpseHandlers.Register`; забывается вместе с трупом).

## 6а. Движение (Стадия 3: 3.2–3.3; заготовка 3.5–3.6)

- **Физика шага (3.2):** `Core/Movement/MovementCheck` (`CheckMovement`, `MoverState`: обычный игрок, GM, призрак) повторяет серверный `MovementImpl.CheckMovement` и блокировку живыми мобами из `Mobile.CanMove` поверх `IMapData`, `ITileData`, `IMultiData` и `EntityStore.Spatial`; работа без выделения памяти в установившемся режиме. Сбор земли, статики, частей multi и предметов клетки — `MovementCellData`; какие части дома занимают клетку и их высота — одно место для физики и отладочной карты: `MultiComponents.IsPlaced`, `MultiComponent.WorldZ`; «пустая» земля — `LandTile.IsIgnored`, переносимый предмет — `Item.IsMovable`; выбор опоры (`GetStartZ`, `Check`) и проверка потолка 16 (`IsOk`) — `MovementSurface`.
- **Что не входит в `MovementCheck`** (сервер решает при шаге сам, клиент узнаёт отказом 0x21): заморозка и паралич, каст заклинания (`Spell.OnCasterMoving`), перегруз и усталость (`StaminaSystem`), правила региона (`Region.CanMove`, например запрет входа в дом), предметы на клетке (`OnMoveOver`), поворот — шаг в новую сторону сначала только поворачивает (`Mobile.Move`). Проход сквозь моба при полной выносливости (`Mobile.CheckShove`, −10 выносливости) включается флагом `MoverState.CanShoveMobiles`; считать его — задача 3.4.
- **Известные упрощения** (на Фелукке не мешают): признак «переносимый» из пакета сервер ставит и украшениям с `ForceShowProperties` (`Item.GetPacketFlags`); стены-заклинания игрок проходит только на других картах (`ignoreSpellFields`); плавание и `CantWalk` не учитываются (только игроки); невидимого игрока клиент не видит, а сервер им загораживает.
- **Очередь шагов (3.5–3.6, заготовка):** `Core/Movement/MovementSequenceQueue` (первый шаг 0, далее 1–255, предел неподтверждённых шагов, `Reset`).
- **Сверка с сервером (3.3):** серверная GM-команда `[DumpMove` (`Custom/TheVerge/Parity/DumpMoveCommand.cs`, `MoveDump.cs`), действие бота `movedump:<проверок>,<seed>,<папка>` (`MoveDumpRequest`, `MoveDumpStep`, `MoveDumpComparison`) и сценарий `tools/check/movement.sh` (100 000 проверок × 4 режима — `player`, `gm`, `ghost`, `shove` — на реальной Фелукке с домом, сундуком и крысой). Кейс дампа — 12 чисел; дамп старого формата бот не сверяет и просит пересобрать сервер.

## 7. Бот и проверки

- **Сборка клиента без Unity** — `dotnet/TheVerge.Bot/BotClient.cs` (то же делает `Bootstrap/UnityLoginClient`). Решения бота (шард, персонаж, выход после `--idle` секунд) — `BotDriver`; бот работает, пока вход не кончится (`LoginFlow.IsFinished`, `BotClient.IsFinished`); персонаж — `--character` (по умолчанию из `bot.json`) или первый, `--create-character` создаёт недостающего; `--reconnect` — повтор входа; решение уходит в следующем тике, соединение закрывается только после него; состояния пишутся в лог по событию `StateChanged`.
- **Запуск:** `tools/bot/run.sh` (справка `--help`), логин и пароль — `tools/bot/setup.sh` → `remaster/secrets/bot.json` (вне git); логи — `Logs/packets/` (20 последних, до 64 МБ); коды выхода 0/1/2/3/4/5/6/130 (`BotExitCode`); `all.sh` запускает бота с `--self-check`. С `--data <папка>` бот читает файлы данных (`BotData.Load` → `BotWorldData`: tiledata, `Map`, `Multis`; с `--self-check` — только проверка папки).
- **Мир бота:** `BotClient.CreateWorld` → `BotClient.InWorld` (`BotWorld`): `Entities` (сводка — `BotWorldSummary`), `Environment`, `Status` (`BotStatus`, строка «Статус: …» перед выходом), `QuickTargets`, `Combat` (`BotCombat`, журнал — `BotCombat.Log` → `CombatLogReport`), `Corpses`.
- **Действия в мире** (`--action вид:значение`, разбор `BotAction.Parse`, выполнение `BotScript` → шаги `IBotStep`; ожидание ответа до 10 с; шаг может сразу сообщить о невозможности — `IBotStep.Failure`; свой срок — `IBotStep.TimeoutMs`; бой — `BotScript.FightTimeoutMs` 60 с, смерть — `BotScript.DeathTimeoutMs`): `say:`, `at:x,y`, `use:backpack`, `use:0xГРАФИКА`, `target:…`, `target:mob:0xТЕЛО`, `seen:0xГРАФИКА[,КОЛИЧЕСТВО]`, `seenmob:0xТЕЛО`, `bandage:self`, `fight:0xТЕЛО` (стойка, атака ближайшего моба с этим телом; смерть — 0xAF), `dead`, `alive`, `deathof:0xТЕЛО`, `dump:<папка>`, `mapdump:`, `statusdump:<папка>`, `movedump:<проверок>,<seed>,<папка>`.
- **Фейковый сервер для CI (1.13):** `dotnet/TheVerge.Bot/Replay` (`ReplayScript` из лога пакетов, `ReplayServer.NextConnection`, расхождения — `Mismatches`).
- **Записи с настоящего сервера** (перезаписывать ботом против копии сервера, если изменился вход или мир): вход — `dotnet/TheVerge.Tests/Bot/Replays/bot-login-modernuo.tsv` (аккаунт `replaybot`, персонаж `Replay`); мир — `Bot/Replays/world-dump-modernuo.*` (бот-GM с `--action use:backpack --action dump:<папка>` против копии с пустым миром).
- **Эталоны ClassicUO (0.6):** посредник с записью — `tools/golden/proxy.py` (подменяет адрес в 0x8C на свой); запись → лог пакетов — `TheVerge.Bot --convert-capture` (`CaptureConverter`, соединение — `CaptureConnection`: вид по первым 9 байтам клиента — ключ + 0x91 с тем же ключом = игровое, поток сервера сжат); запись сеансов — `tools/golden/record.sh` (на Mac), сравнение логов — `tools/golden/diff.sh`, бот против эталонов — `tools/golden/compare.sh`, самопроверка цепочки — `tools/golden/selftest.sh`; временная копия сервера для них — `tools/golden/server-copy.sh`; эталоны — `golden/`; ClassicUO — `remaster/classicuo` (вне git).
- **Действия сверки мира (2.12):** `onmap:N` — ждать, пока бот окажется на карте N (`OnMapStep`); `seenmulti:` — ждать дом или корабль (multi) в мире (`SeenMultiStep`). Сценарий — `tools/check/parity.sh`: GM-команда `[ParityPlace house|ship` ставит дом или корабль рядом с GM на настоящей карте; `[move` двигает моба; `[go Trammel` / `[go Felucca` меняют карту.
- **Действия `scene:[радиус]` и `inspect:[x,y]` (2.13):** `scene:` строит сцену вокруг бота (`SceneBuilder`) и печатает сводку — число блоков по видам и по цветам; `inspect:` пишет в журнал окно клетки (`CellInspector`, по строке на объект; без клетки — клетка бота); нужен `--data`. Сценарий — `tools/check/debugmap.sh`.
- **Скрипты проверок** и что они делают — `docs/DEV-ENV.md` §1.
- **Сервер:** GM-команды сверки — `server/ModernUO/Projects/UOContent/Custom/TheVerge/Parity/` (`[DumpVisible`, `[DumpMapData`, `[DumpMobile`, `[DumpMove`, `[ParityPlace`), только для GameMaster.

## 8. Поведение сервера, найденное по его коду

Не угадывать заново — проверено по исходнику ModernUO при выполнении задач.

| Что | Как ведёт себя сервер |
|---|---|
| 0x8C (редирект) | IP в обычном порядке байт; после 0x8C сервер сам закрывает логин-сокет — это не ошибка |
| 4 байта перед 0x91 | сервер читает их как seed (`NetState`, `AwaitingSeed`) и сверяет ключ из 0x91 с seed и своим окном ключей |
| Версия клиента | сервер узнаёт её по `client.exe` в папке данных и требует от клиентов, поэтому в настройках задана явно (`clientData.clientVersion` = 7.0.95.0, `TARGET.md`) |
| 0x53 | при входе сервер шлёт только «персонаж уже в мире» |
| 0xBF 0x05 (размер экрана) | сервер не читает — не шлём |
| 0xC8 (дальность обзора) | сервер всегда отвечает 18 |
| Смена карты 0xBF 0x08 | при входе сервер шлёт её дважды; смена на ту же карту ничего не удаляет |
| Объекты за дальностью | дальше 18 клеток (multi — 22) сервер 0x1D не шлёт (`Mobile.SetLocation`) — клиент удаляет сам |
| Повторный 0x78 того же моба | заменяет надетое целиком (сервер шлёт весь видимый список) |
| Здоровье чужого моба | сервер шлёт долей «из 100» (`AttributeMaximum`); точное — только своё |
| 0xBF 0x0C (закрыть статус) | сервер читает и ничего не делает — не шлём |
| 0xBF 0x2D (заклинание на цель) | цель не используется: сервер начинает каст, дальше обычный курсор |
| Ранний бинт | сервер отвечает «подождите»; клиент всё равно не шлёт раньше задержки |
| Атака без стойки | `Mobile.Attack` стойку не включает, только ставит противника — клиент тоже не меняет |
| 0x2F (замах) | только атакующему: замахи противника по нам не приходят, видны его анимации |
| 0x0B (урон) | в UOR по умолчанию не шлётся (`visibleDamage`) |
| 0x89 (вещи трупа) | только для трупов людей (`Corpse.ShowsWornGear`), раньше 0xAF и 0x2C (труп кладётся до анимации) |
| 0x06 со старшим битом | «показать куклу» этого моба (`UseReq`) — так ClassicUO открывает куклу; ответ 0x88 |
| 0x72 от клиента | сервер читает только первый байт (`SetWarMode`); хвост у клиентов разный |
| Стартовые вещи | в UOR неразрывны (Newbied) и остаются на призраке |
| Призрак игрока | виден GM: у GM моб не удаляется, а меняет тело |
| Молчащее подключение | 5 с без данных — адрес блокируется на 15 минут (`TARGET.md` §5); на стенде 127.0.0.1 в списке исключений |
| Dif-патчи карты | при версии 7.0.95.0 не применяются (`TileMatrixPatch.Configure`) |
| Смена карты (`[go Trammel`) | сервер заново присылает игроку содержимое его рюкзака (0x3C), а `[DumpVisible` перечисляет содержимое только открытых контейнеров (`Container.Openers`) — перед сверкой после смены карты нужно `use:backpack` |
| Моб (крыса) | у него свой рюкзак — надетый предмет на слое 21; «яблоко и крыса» — 3 объекта, а не 2 |
| Ключи дома (`BaseHouse.CreateKeys`, не AOS) | кладутся в рюкзак и банковский ящик хозяина; банковский ящик после этого виден боту как надетый предмет (слой 29) |
| Предметы в контейнере и обновление (`Item.ProcessDelta`) | 0x25 шлётся корневому владельцу-мобу, даже если контейнер не открыт — бот узнаёт о таких предметах, а дамп нет, пока контейнер не открыт |
| Противник 0xAA (`Mobile.AggressiveAction`, `Mobile.Combatant`) | противник ставится только при первом нападении нового агрессора; если бьют двое и первый умер, приходит 0, хотя второй ещё бьёт, — по 0xAA нельзя решать «бой кончился»; удары идут без проверки стойки (`Mobile.CheckCombatTime`) |
| Стрелы лучника (`BaseRanged.OnMiss`) | в UOR промахнувшаяся стрела сразу падает на землю рядом с целью; сбор стрел вне стойки (`AmmoRecovery`) — только с эры SE |
| Клад по карте сокровищ (`TreasureMap.HasDiggingTool`) | копать можно только инструментом семейства `BaseHarvestTool` в рюкзаке (лопата); кирка — оружие и не подходит (меняем — ADR 0021) |
| Физика движения: земля с флагом `Wet` (`MovementImpl.Check`) | при ходьбе без плавания (`!CanSwim`) блокирует только флаг `Impassable`, а один `Wet` проходим (например, плитки кислоты `acid` в Solen Hive); обычная вода в `tiledata.mul` несёт оба флага `Impassable | Wet` |
| Физика движения: лестницы и двери (`MovementImpl.Check`, `IsOk`) | при наличии `TileFlags.Bridge` `itemTop = item.Z` (без прибавления `Height`, поэтому шаг поднимается на `CalcHeight` выше 2); для статики и multi `ignoreDoors` у призрака не применяется — двери игнорируются только среди динамических предметов |
