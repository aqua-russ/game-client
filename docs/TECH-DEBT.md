# TECH-DEBT — технический долг

Единый список всего, что нарушает правила `AGENTS.md` или временно сделано «не как надо». Правило работы с долгом — `AGENTS.md` §8. Закрыл пункт — убери строку отсюда и запиши в хронику `PROGRESS.md`. Здесь только то, что ещё не сделано.

*Обновлено 13 ноября 2026.*

## 1. Код клиента

Известного долга в коде клиента нет: прототип разобран в задаче 0.9 ([ADR 0011](adr/0011-prototype-audit.md)). Новое временное решение, которое нарушает правила `AGENTS.md`, записывается сюда только с согласия пользователя (`AGENTS.md` §8).

## 2. Настройки Unity-проекта

| Что | Как сейчас | Как должно быть | Когда |
|---|---|---|---|
| Способ ввода (`Active Input Handling`) | «Both» (старый Input Manager и Input System) — Unity предупреждает при открытии | только Input System | Стадия 3 (вместе с вводом WASD) |
| Пакет `com.unity.ugui`, TextMeshPro и папка `Assets/TextMesh Pro` | старый UI удалён (0.9), сборки `TheVerge.*` на них не ссылаются, но пакеты ещё в `manifest.json` | убрать из `manifest.json` и папку TextMesh Pro — только с согласия пользователя (AGENTS §6) | вместе с задачей 1.16 |

## 3. Сервер

| Что | Где | Что сделать | Когда |
|---|---|---|---|
| Песочница Минока и команды `[minoc:` (клиент их больше не шлёт: старая сеть удалена в 0.9) | `Custom/MinocSandboxInitializer.cs` + перехваты в `IncomingAccountPackets`, `IncomingMessagePackets`, `IncomingEntityPackets` | удалить целиком | после Стадий 4–7 |
| Нет комментариев `// THE VERGE:` в ранних правках движка | `SpanWriter.cs`, `OutgoingAccountPackets.cs`, `OutgoingMobilePackets.cs` | добавить комментарии | при следующей правке сервера |
| Правки UTF-8 считают размер пакета по буквам, а не по байтам (0xBF 0x10, 0x9E, 0x7C): с кириллицей — ошибка сервера; имя в кукле 0x88 осталось в Latin-1 | `OutgoingEquipmentPackets.cs`, `OutgoingVendorSellPackets.cs`, `OutgoingMenuPackets.cs`, `OutgoingPlayerPackets.cs` (движок) | исправить размер по байтам, 0x88 — в UTF-8; тест на сервере с кириллическим именем | ждёт разрешения пользователя (правка движка) |
| Снята проверка запрещённых слов в именах | `Misc/NameVerification.cs` | вернуть (с поддержкой кириллицы) | перед открытым тестом |
| Нет 7-дневного запрета на удаление персонажа | `Accounting/AccountHandler.cs` | вернуть `RestrictDeletion = true` | перед открытым тестом |
| Заглушка данных мира | `Distribution/Data/ClientData/` | положить полную установку клиента (ADR 0003) | задача 0.2 |
| Реакция на неверную версию клиента | `modernuo.json`: `Warn` | для parity-проверок — `Kick` (`TARGET.md` §5); бот с `Kick` проверен 13 ноября 2026 на копии сервера: 32 с в мире без разрыва | при сборке parity-стенда (задача 0.2) |

## 4. Базовая линия проверок

`tools/check/architecture-baseline.txt` и `tools/check/dotnet-warnings-baseline.txt` — известный долг, который `tools/check/all.sh` не считает ошибкой, пока он не растёт (ADR 0009). **Сейчас оба пусты.** Строки только убираются (`tools/check/architecture.sh --shrink-baseline`); добавлять — только с согласия пользователя.
