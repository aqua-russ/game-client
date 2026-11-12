# Правки сервера ModernUO

Журнал всех отличий нашего форка `server/ModernUO` от оригинала. Обязателен к обновлению при любой правке сервера (`AGENTS.md` §5). Правки движка (`Projects/Server`) — только с разрешения руководителя, с комментарием `// THE VERGE: <причина>`.

*Состояние на 10 ноября 2026. Все правки закоммичены в ветке `theverge/uor-baseline` и отправлены в `github.com/aqua-russ/game-server`. Сборка `dotnet build -c Release` проходит без ошибок и предупреждений. Список сверен с `git diff` против оригинала ModernUO: 16 файлов, все в таблицах ниже.*

## Движок (`Projects/Server`) — риск конфликтов при обновлении ModernUO

| Файл | Что | Зачем | Решение |
|---|---|---|---|
| `Buffers/SpanWriter.cs` | `Write(..., encoding, fixedLength)` обрезает строку по байтам | UTF-8 имена не переполняют поле фиксированной длины (краш 8 ноября) | [ADR 0005](adr/0005-utf8-names.md), принято |
| `Network/Packets/OutgoingAccountPackets.cs` | имена в списках персонажей — UTF-8 (оба варианта) | кириллица | ADR 0005 |
| `Network/Packets/OutgoingMessagePackets.cs` | имя говорящего в 0xC1, 0xCC, 0x1C, 0xAE — UTF-8 | кириллица | ADR 0005 |
| `Network/Packets/OutgoingSecureTradePackets.cs` | имя в 0x6F — UTF-8 | кириллица | ADR 0005 |
| `Network/Packets/OutgoingVendorSellPackets.cs`, `OutgoingMenuPackets.cs`, `OutgoingEquipmentPackets.cs` | строки с длиной впереди — UTF-8, длина в байтах | кириллица | ADR 0005 |
| `Network/Packets/OutgoingMobilePackets.cs` | имя в 0x98 и 0x11 — UTF-8 | кириллица | ADR 0005 |

Новые правки помечены `// THE VERGE:`; в трёх ранних (`SpanWriter.cs`, `OutgoingAccountPackets.cs`, `OutgoingMobilePackets.cs`) комментариев нет — долг, см. [`TECH-DEBT.md`](TECH-DEBT.md).

## Контент (`Projects/UOContent`)

| Файл | Что | Зачем | Статус |
|---|---|---|---|
| `Custom/MinocSandboxInitializer.cs` | песочница Минока, речевые команды `[minoc:` | прототип клиента | выключена флагом `theverge.sandbox.enable`, команды только для GM ([ADR 0004](adr/0004-sandbox-and-parity-stand.md)); удалить после Стадий 4–7 |
| `Network/Packets/IncomingAccountPackets.cs` | `DoLogin`: перенос в Минок и вызовы песочницы | прототип | перенос только при включённой песочнице |
| `Network/Packets/IncomingAccountPackets.cs` | 0xF8: имя читается как UTF-8 | кириллица | ADR 0005 |
| `Network/Packets/IncomingMobilePackets.cs` | 0x75 (переименование питомца) читается как UTF-8 | кириллица | ADR 0005 |
| `Items/Games/Mahjong/MahjongPackets.cs` | имя игрока — UTF-8 | кириллица | ADR 0005 |
| `Network/Packets/IncomingAccountPackets.cs` | 0xBD: пропуск ведущих нулей, подстановка `7.0.95.0` при пустой версии | обход ошибки старого клиента-прототипа | **долг**: сервер не должен подгоняться под ошибки клиента; убрать, когда клиент проверен тестом на 0xBD |
| `Network/Packets/IncomingMessagePackets.cs` | перехват речи для `[minoc:` | песочница | работает только при флаге и для GM |
| `Network/Packets/IncomingEntityPackets.cs` | перехват двойного клика для песочницы | песочница | работает только при флаге |
| `Misc/NameVerification.cs` | имя игрока: любые буквы, 2–24 символа, ≤ 29 байт UTF-8 (**проверка запрещённых слов оригинала отключена — вернуть**); имя питомца: ASCII — как в оригинале, прочее — только буквы | кириллица | ADR 0005 |
| `Accounting/AccountHandler.cs` | `RestrictDeletion = false` (нет 7-дневного запрета удаления персонажа) | удобство разработки | вернуть перед открытым тестом |
| `Engines/Character Creation/CharacterCreation.cs` | — | — | **возвращён к оригиналу** ([ADR 0001](adr/0001-no-gdd-port-original-mechanics.md)) |

## Данные и конфигурация

| Файл | Что | Статус |
|---|---|---|
| `Distribution/Data/map-definitions.json` | — | правила Траммеля **возвращены** к `TrammelRules` (ADR 0001) |
| `Distribution/Data/ClientData/` | заглушка мира для прототипа | заменить полными данными ([ADR 0003](adr/0003-world-data.md)) |
| `Distribution/Configuration/expansion.json` | Renaissance, только Фелукка | [ADR 0002](adr/0002-client-version-and-era.md) |
| `Distribution/Configuration/modernuo.json` | значения эры UOR, `theverge.sandbox.enable`, `serverListing.address` = 127.0.0.1 | ADR 0002, ADR 0004 |
| `Distribution/Configuration/FeatureFlags/flags.json` | `speedhack_detection` = true | ADR 0004 |
| `Distribution/Configuration/ip-allowlist.json`, `ip-allowlist-theverge.txt` | ручной список исключений включён, в нём 127.0.0.1 | dev-стенд: молчаливое подключение не блокирует localhost на 15 минут (`TARGET.md` §5), 12 ноября 2026 |

`Distribution/Configuration/` не хранится в git (так задумано в ModernUO). Версионируемая копия значений The Verge — `Distribution/ConfigurationTemplates/theverge/` (см. README там); при изменении реальной конфигурации обновлять шаблон. Копия до правок 10 ноября — `Distribution/Configuration/backup-2026-11-10/` (локально).

## Как проверить, что журнал полный

В папке сервера: `git diff --stat upstream/main...theverge/uor-baseline -- Projects`. Каждый файл из вывода должен быть в таблицах выше; если появился новый — журнал отстал.
