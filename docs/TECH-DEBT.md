# TECH-DEBT — технический долг

Единый список всего, что нарушает правила `AGENTS.md` или временно сделано «не как надо». Правило работы с долгом — `AGENTS.md` §8. Закрыл пункт — вычеркни его здесь и запиши в хронику `PROGRESS.md`.

*Обновлено 11 ноября 2026.*

## 1. Что делаем с кодом прототипа

Код в `Assets/TheVerge` (~7 100 строк) написан до плана и архитектуры: работает, но устроен иначе (фасады-адаптеры, симулятор мира, UI из C#). **Решение:** новый код Стадий 1–8 пишется рядом по правилам `AGENTS.md`, а старые файлы удаляются, когда новый код их заменяет. Старое не расширяется и не переделывается «по чуть-чуть». Пока замены нет, прототип продолжает работать как есть.

| Старый файл (строк) | Что не так | Чем заменяется | Когда |
|---|---|---|---|
| `Network/ModernUONetworkAdapter.cs` (480) | сокет, сессия, разбор и фасады в одном классе; шлёт речевые команды `[minoc:` | `TcpConnection`, `PacketFramer`, `GameClient`, обработчики и `*Sender` по доменам | Стадия 1 |
| `Network/Processors/*` (7 файлов) | разбор пакетов в DTO для фасадов | `*Handler` в `Network/Packets/<Domain>/` | Стадии 1–5 |
| `Network/Protocol/PacketLengthTable.cs` (218) | таблица длин руками | `PacketTable.g.cs` из `packets.csv` | задача 0.5 |
| `Network/Compression/HuffmanCodec.cs` (222) | годится как основа, но без тестов нарезки | `HuffmanDecoder` с тестами из плана | Стадия 1 |
| `Network/Loopback/ModernUOLoopbackServer.cs` (645) | свой «сервер» в клиенте дублирует логику ModernUO | фейковый сервер, проигрывающий golden-логи, в `dotnet/TheVerge.Tests` | Стадия 1 |
| `Core/Simulators/LocalWorldSimulator.cs` (687) | симулятор мира на клиенте, противоречит авторитету сервера | не нужен: тесты на golden-логах и фейковом сервере | Стадия 1 |
| `Core/Adapters/I*Adapter.cs` (6 интерфейсов) | «божественные» интерфейсы, выдуманные команды (`RequestCraftItem`, `RequestMineAt`) | доменные состояния, команды и порты (`AGENTS.md` §2.3) | Стадии 1–7 |
| `Core/Adapters/AdapterModels.cs` (290) | ~11 типов в одном файле | доменные события и состояния | вместе с адаптерами |
| `Core/World/MinocMapSliceData.cs` (183) | захардкоженный кусок карты Минока | сборка `Data`, чтение настоящих файлов | Стадия 2 |
| `Core/Threading/*`, `Presentation/Threading/MainThreadDispatcher.cs` | передача сетевых данных в главный поток через диспетчер | очередь пакетов и `GameClient.Tick` (`AGENTS.md` §2.3) | Стадия 1 |
| ~~`Presentation/Bootstrap/GameBootstrap.cs`~~ | ~~точка входа в Presentation~~ | перенесён в сборку `Bootstrap` (`Bootstrap/GameBootstrap.cs`); поиск сервера и запуск loopback остались до замены `ModernUOLoopbackServer` | ✅ задача 0.1 |
| `Presentation/Bootstrap/NetworkStatusHudPresenter.cs` (328) | размер, UI из C#; счётчики сети берёт через временный порт `INetworkDiagnostics` | View + Presenter, разметка UI Toolkit | Стадия 1 |
| `Presentation/UI/Auth/*` (6 файлов, 111–281) | UI из C# на uGUI; создание персонажа по правилам GDD (80 очков статов, 3 навыка из списка GDD) | UI Toolkit; правила сервера: статы 90, навыки до 50, сумма 100 или 120 (`TARGET.md` §4) | Стадия 1 |
| `Presentation/UI/InGameEscapeMenuModal.cs`, `InactivityAfkController.cs` | UI из C# | UI Toolkit | при касании |
| `Core/Ports/INetworkDiagnostics.cs` | временный порт только для старого окна статуса сети (ADR 0009) | уходит вместе с `NetworkStatusHudPresenter` | Стадия 1 |
| `#nullable disable` первой строкой в 12 файлах прототипа (`ModernUONetworkAdapter`, `Processors/*`, `ModernUOLoopbackServer`, `LocalWorldSimulator`, `ISessionAdapter`, `AdapterModels`) | код не размечен под nullable (ADR 0009) | исчезает вместе с файлами | по мере замены |

## 2. Настройки Unity-проекта

| Что | Как сейчас | Как должно быть | Когда |
|---|---|---|---|
| Способ ввода (`Active Input Handling`) | «Both» (старый Input Manager и Input System) — Unity предупреждает при открытии | только Input System | Стадия 3 (вместе с вводом WASD) |
| ~~Ссылка `Presentation → Network` в `.asmdef`~~ | ~~есть~~ | снята; объекты связывает `Bootstrap` | ✅ задача 0.1 |
| Пакет `com.unity.ugui` и TextMeshPro | нужны старому UI | убрать, когда весь UI переедет на UI Toolkit | после Стадии 1 |
| Стартовая сцена | `Assets/TheVerge/Scenes/Bootstrap.unity`, собирается меню `The Verge → Prepare Bootstrap Scene` | так и остаётся; при открытии проекта Unity может показать пустую сцену «Untitled» — нужно открыть `Bootstrap` | — |

## 3. Сервер

| Что | Где | Что сделать | Когда |
|---|---|---|---|
| Песочница Минока и команды `[minoc:` | `Custom/MinocSandboxInitializer.cs` + перехваты в `IncomingAccountPackets`, `IncomingMessagePackets`, `IncomingEntityPackets` | удалить целиком | после Стадий 4–7 |
| Подгонка разбора 0xBD под ошибку старого клиента (пропуск нулей, подстановка версии) | `IncomingAccountPackets.cs` | вернуть оригинал | когда новый клиент пройдёт тест на 0xBD (Стадия 1) |
| Нет комментариев `// THE VERGE:` в ранних правках движка | `SpanWriter.cs`, `OutgoingAccountPackets.cs`, `OutgoingMobilePackets.cs` | добавить комментарии | при следующей правке сервера |
| Снята проверка запрещённых слов в именах | `Misc/NameVerification.cs` | вернуть (с поддержкой кириллицы) | перед открытым тестом |
| Нет 7-дневного запрета на удаление персонажа | `Accounting/AccountHandler.cs` | вернуть `RestrictDeletion = true` | перед открытым тестом |
| Заглушка данных мира | `Distribution/Data/ClientData/` | положить полную установку клиента (ADR 0003) | задача 0.2 |
| Реакция на неверную версию клиента | `modernuo.json`: `Warn` | для parity-проверок — `Kick` (`TARGET.md` §5) | Стадия 1 |

## 4. Базовая линия проверок

Известный долг, который `tools/check/all.sh` не считает ошибкой, пока он не растёт (ADR 0009). Строки только убираются.

| Файл | Что внутри | Как уменьшать |
|---|---|---|
| `tools/check/architecture-baseline.txt` | 75 нарушений правил размеров и именования в коде прототипа | исправил или удалил файл → `tools/check/architecture.sh --shrink-baseline` |
| `tools/check/dotnet-warnings-baseline.txt` | 8 предупреждений CS0067 (неиспользуемые события) в `LocalWorldSimulator`, `InventoryPacketProcessor`, `LoginPacketProcessor` | исчезают вместе с файлами; строку убрать руками |
