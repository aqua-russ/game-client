# TECH-DEBT — технический долг

Единый список всего, что нарушает правила `AGENTS.md` или временно сделано «не как надо». Правило работы с долгом — `AGENTS.md` §8. Закрыл пункт — вычеркни его здесь и запиши в хронику `PROGRESS.md`.

*Обновлено 11 ноября 2026.*

## 1. Что делаем с кодом прототипа

Решение по каждому файлу `Assets/TheVerge` принято в задаче 0.9 — [ADR 0011](adr/0011-prototype-audit.md): там таблица, какой файл удаляется, какой доводится через тесты и какой остаётся как есть. Прежнее правило «новый код пишется рядом, старые файлы удаляются, когда их заменят» этим ADR отменено.

Аудит шёл частями (по слоям, каждая часть — отдельный коммит). **Все части сделаны 11 ноября 2026**: из 54 файлов удалён 41, 6 доведены через тесты по серверу, `GameBootstrap` упрощён; добавлены `HuffmanTree`, `PendingStep` (вынесены в свои файлы) и метка `PresentationAssembly`. Долга прототипа в коде больше нет. Осталось одно: компиляция Unity на Mac (`tools/check/all.sh --unity`).

| Часть (ADR 0011) | Что | Статус |
|---|---|---|
| 1. Presentation, Bootstrap, Editor | удалить старый UI на uGUI (окна входа, окно сети, меню Esc, AFK), диспетчер главного потока; упростить `GameBootstrap`; метка `PresentationAssembly`; `.asmdef` без uGUI и TextMeshPro | ✅ |
| 2. Network | удалить `ModernUONetworkAdapter`, `Processors/*` (7), `ModernUOLoopbackServer`, `PacketLengthTable`, `CharacterCreationPacketBuilder` | ✅ |
| 3. Network/Compression | `HuffmanCodec` → `HuffmanDecoder` через тесты (эталон сервера, переполнение буфера) | ✅ |
| 4. Core | удалить фасады `Adapters/*`, симулятор, кусок карты Минока, навыки GDD, модель статов, диспетчер, `CoordinateMapper`, `INetworkDiagnostics`; базовые типы — тесты по серверу и папка `Core/Primitives` | ✅ |
| 5. Core/Movement | `MovementSequenceQueue` через тесты по серверу | ✅ |

## 2. Настройки Unity-проекта

| Что | Как сейчас | Как должно быть | Когда |
|---|---|---|---|
| Способ ввода (`Active Input Handling`) | «Both» (старый Input Manager и Input System) — Unity предупреждает при открытии | только Input System | Стадия 3 (вместе с вводом WASD) |
| ~~Ссылка `Presentation → Network` в `.asmdef`~~ | ~~есть~~ | снята; объекты связывает `Bootstrap` | ✅ задача 0.1 |
| Пакет `com.unity.ugui`, TextMeshPro и папка `Assets/TextMesh Pro` | старый UI удалён (0.9), сборки `TheVerge.*` на них не ссылаются, но пакеты ещё в `manifest.json` | убрать из `manifest.json` и папку TextMesh Pro — только с согласия пользователя (AGENTS §6) | вместе с задачей 1.16 |
| Стартовая сцена | `Assets/TheVerge/Scenes/Bootstrap.unity`, собирается меню `The Verge → Prepare Bootstrap Scene` | так и остаётся; при открытии проекта Unity может показать пустую сцену «Untitled» — нужно открыть `Bootstrap` | — |

## 3. Сервер

| Что | Где | Что сделать | Когда |
|---|---|---|---|
| Песочница Минока и команды `[minoc:` (клиент их больше не шлёт: старая сеть удалена в 0.9) | `Custom/MinocSandboxInitializer.cs` + перехваты в `IncomingAccountPackets`, `IncomingMessagePackets`, `IncomingEntityPackets` | удалить целиком | после Стадий 4–7 |
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
| `tools/check/architecture-baseline.txt` | нарушения правил размеров и именования: было 75 (прототип), после аудита 0.9 — **пусто** | исправил или удалил файл → `tools/check/architecture.sh --shrink-baseline`; новые строки — только с согласия пользователя |
| `tools/check/dotnet-warnings-baseline.txt` | было 8 предупреждений CS0067 (прототип); после аудита 0.9 — **пусто** | исчезают вместе с файлами; строку убрать руками |
