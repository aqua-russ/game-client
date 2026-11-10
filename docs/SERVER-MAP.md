# SERVER-MAP — где что лежит на сервере ModernUO

Шпаргалка для разработки клиента: куда смотреть на сервере, чтобы узнать формат пакета, правило или настройку. Пути — от `server/ModernUO/`. Проверено по исходникам 10 ноября 2026 (ветка `theverge/uor-baseline`). Код сервера (GPL-3.0) читаем как справочник и **не копируем** в клиент.

## 1. Общая схема

| Папка | Что там | Можно ли править |
|---|---|---|
| `Projects/Server/` | движок: сеть, пакеты, `Mobile`, `Item`, карта, таймеры | только с разрешения руководителя (`AGENTS.md` §5) |
| `Projects/UOContent/` | игровое содержимое: навыки, заклинания, предметы, NPC, гампы, обработчики входящих пакетов | да, минимально; своё — в `Custom/TheVerge/` |
| `Projects/UOContent/Custom/` | наш код. Сейчас там только песочница `MinocSandboxInitializer.cs` (выключена, долг) | да |
| `Projects/*.Tests/` | тесты сервера (в том числе тесты пакетов — удобный образец байтов) | — |
| `Distribution/` | собранный сервер, отсюда он запускается | — |
| `Distribution/Configuration/` | настройки стенда (в git не хранится) | да + обновить шаблон |
| `Distribution/ConfigurationTemplates/theverge/` | шаблон наших настроек (в git) | да |
| `Distribution/Data/` | данные сервера: эры, карты, регионы, спавны, навыки | осторожно |
| `Distribution/Data/ClientData/` | файлы данных клиента UO (сейчас заглушка, ADR 0003) | заменить полной установкой |
| `dev-docs/` | документация ModernUO для разработчиков | только чтение |
| `CLAUDE.md` | правила кода сервера (обязательны при правке сервера) | не трогать |

Сервер однопоточный: вся игровая логика в одном цикле. Сборка — `dotnet build -c Release` из корня; запуск — из `Distribution/`.

## 2. Сеть и пакеты

| Что нужно узнать | Где смотреть |
|---|---|
| Какие пакеты сервер **принимает**, их длина | регистрации `IncomingPackets.Register(...)` в `Projects/UOContent/Network/Packets/Incoming*.cs` (список — §3) |
| Разбор конкретного входящего пакета | метод-обработчик в том же файле (`MovementReq`, `DropReq`, `TargetResponse` …) |
| Подкоманды 0xBF | `IncomingExtendedCommandPackets.cs`, `RegisterExtended` |
| Подкоманды 0xD7 | `RegisterEncoded` (дома — `IncomingHousePackets.cs`, остальное — `IncomingPlayerPackets.cs`) |
| Типы текстовой команды 0x12 | `IncomingPlayerPackets.TextCommand` |
| Какие пакеты сервер **шлёт** и их байты | `Projects/Server/Network/Packets/Outgoing*.cs` (по темам: Account, Mobile, Item, Entity, Container, Movement, Combat, Damage, Effect, Message, Menu, Player, Target, SecureTrade, VendorBuy, VendorSell, Light, Map, Equipment) |
| Исходящие пакеты содержимого | рядом с системой в `UOContent`: гампы — `Gumps/Base/` (`Gump.cs`, `DynamicGump.cs`, `OutgoingGumpPackets.cs`), книги — `BookPackets.cs`, карты — `MapItemPackets.cs`, группа — `Engines/Party/PartyPackets.cs`, корабли — `Multis/Boats/BoatPackets.cs`, контекстные меню — `Context Menus/ContextMenuSystem.cs`, позиции группы — `Network/MapUO.cs`, доски — `Items/.../BulletinBoardPackets.cs`, маджонг — `Items/Games/Mahjong/MahjongPackets.cs` |
| Какой формат пакета зависит от версии клиента | `Projects/Server/Network/NetState/ProtocolChanges.cs` (флаги) и `NetState.ClientVersion.cs` (свойства `ns.HighSeas`, `ns.StygianAbyss`, `ns.ContainerGridLines`, `ns.NewSpellbook` …). Для 7.0.95.0 включены **все** флаги до `Version70610` |
| Сжатие Huffman | `Projects/Server/Network/NetworkCompression.cs` |
| Шифрование | `Projects/Server/Network/Encryption/` (`network.encryptionMode`, по умолчанию `Both`) |
| Проверка темпа шагов | `Projects/Server/Network/MovementThrottle.cs`, `NetState/NetState.Movement.cs` |
| Троттлинг пакетов | `Distribution/Configuration/throttles.json` |
| Запись трафика в файл | настройка `netstate.packetLoggingPath` (`NetState.cs`) |
| Как пакеты устроены вообще | `dev-docs/networking-packets.md` |

Приём поиска: формат исходящего пакета ищется по строке `0xNN); // Packet ID` или `stackalloc byte[] { 0xNN` в папках выше.

## 3. Входящие пакеты сервера (полный список регистраций)

Длина 0 — переменная (2 байта длины после id). «вне игры» — только до входа в мир.

| Тема | Пакеты (id, длина) |
|---|---|
| Вход | 0xEF seed (21, вне игры) · 0x80 логин (62) · 0xA0 выбор шарда (3) · 0x91 игровой логин (65) · 0x5D выбор персонажа (73) · 0xF8 создание (106) · 0x00 старое создание (104) · 0x83 удаление (39) · 0xBD версия клиента (перем.) · 0xE1 тип клиента (перем.) |
| Сессия | 0x73 пинг (2) · 0x01 отключение (5) · 0xD1 выход (2) · 0xC8 дальность обзора (2) · 0xA4 сведения о системе (149) · 0xD9 (268) · 0xBE версия ассистента · 0xF4 отчёт о сбое |
| Движение | 0x02 шаг (7) · 0x22 ресинхронизация (3) |
| Речь и команды | 0xAD Unicode-речь · 0x03 ASCII-речь · 0x12 текстовая команда · 0xB3 и 0xB5 чат (выключен) |
| Мир | 0x06 двойной клик (5) · 0x09 одиночный клик (5) · 0x05 атака (5) · 0x72 боевая стойка (5) · 0x6C ответ прицела (19) · 0x34 запрос статуса или навыков (10) · 0x3A замок навыка · 0x98 запрос имени · 0x75 переименование (35) · 0x2C ответ на смерть (2) |
| Предметы | 0x07 поднять (7) · 0x08 положить (14, для нашей версии 15) · 0x13 надеть (10) · 0x95 выбор цвета (9) |
| Окна | 0xB1 ответ гампа · 0x7D ответ меню (13) · 0x9A и 0xC2 ответы на запрос ввода · 0xB8 профиль · 0xB6 справка по объекту (9) · 0x9B помощь (258) · 0xA7 окно подсказок (4) |
| Книги, карты, доски | 0x66 страницы книги · 0x93 и 0xD4 заголовок книги · 0x56 булавки карты (11) · 0x71 доска объявлений |
| Торговля | 0x3B покупка · 0x9F продажа · 0x6F безопасная торговля |
| Составные | 0xBF расширенные · 0xD7 кодированные · 0xD6 запрос свойств · 0xD0 файл настроек · 0xDA маджонг |
| Ассистенты и прочее | 0xF0 (0x00 позиции группы, 0x01 гильдии; 0xFF рукопожатие) · 0xEC и 0xED макросы EC · 0xFA магазин (1) · 0xFB (2) · 0xBB (9) |

**0xBF, принимаемые подкоманды:** 0x05 размер экрана · 0x06 группа · 0x07 стрелка квеста · 0x09 обезоружить · 0x0A оглушить · 0x0B язык · 0x0C закрыть статус · 0x0E анимация · 0x0F · 0x10 запрос свойств · 0x13 запрос контекстного меню · 0x15 выбор пункта меню · 0x1A замок стата · 0x1C каст · 0x1E детали дома · 0x24 · 0x2A смена расы · 0x2C бинт на цель · 0x2D заклинание на цель · 0x2E навык на цель · 0x30 цель по ресурсу · 0x32 полёт.

**0x12, типы команды:** 0x24 навык · 0x27 каст из книги · 0x56 каст из макроса · 0x43 открыть книгу заклинаний · 0x58 открыть дверь · 0xC7 анимация · 0xF4 добродетель · 0x2F старый двойной клик по свитку.

**0xD7:** 0x19 выбор приёма оружия · 0x28 кнопка гильдии · 0x32 кнопка квестов · 0x02–0x1A конструктор домов. В эре UOR ни одна из них не нужна (`TARGET.md` §2).

**Закомментировано в сервере (не работает):** 0xF0 и 0xF1 как «новое движение».

## 4. Эра и настройки

| Что нужно узнать | Где смотреть |
|---|---|
| Текущая эра и карты | `Distribution/Configuration/expansion.json`; все эры — `Distribution/Data/expansions.json` |
| Что в коде зависит от эры | поиск по `Core.UOR`, `Core.AOS`, `Core.SE` … (флаги — `Projects/Server/Main.cs`); обзор — `dev-docs/era-expansion.md` |
| Значения по умолчанию, зависящие от эры (тултипы, урон, задержка действий, меню крафта) | `Projects/UOContent/Configuration/ExpansionConfiguration.cs` |
| Основные настройки стенда | `Distribution/Configuration/modernuo.json`; чтение — `ServerConfiguration.GetSetting("ключ", значение_по_умолчанию)` в коде; описание — `dev-docs/configuration.md` |
| Флаги возможностей (проверка темпа, торговля, PvP, дома) | `Distribution/Configuration/FeatureFlags/flags.json`, блокировки — `*-blocks.json`; код — `Projects/UOContent/Engines/FeatureFlags/` |
| Размеры и номера карт | `Distribution/Data/map-definitions.json` |
| Регионы, стража, города | `Distribution/Data/regions.json`, `Projects/UOContent/Regions/` |
| Наши значения и профили стенда | `docs/TARGET.md` (в репозитории клиента) |

## 5. Механики по стадиям плана

| Стадия | Тема | Где на сервере |
|---|---|---|
| 1 | вход, список персонажей, создание | `UOContent/Network/Packets/IncomingAccountPackets.cs`, `UOContent/Accounting/AccountHandler.cs`, `Engines/Character Creation/CharacterCreation.cs`, `Server/Network/Packets/OutgoingAccountPackets.cs` |
| 2 | чтение карты, статики, tiledata, multi | `Server/TileMatrix/`, `Server/TileData.cs`, `Server/Client/MultiData.cs`, `Server/Maps/` |
| 2 | отправка мобов и предметов | `OutgoingMobilePackets.cs`, `OutgoingEntityPackets.cs`, `OutgoingItemPackets.cs`; логика «кому что слать» — `Server/Mobiles/Mobile.cs`, `Server/Items/Item.cs` |
| 3 | можно ли шагнуть и новый Z | `Server/Mobiles/Movement.cs` → `UOContent/Engines/Pathing/Movement.cs` (`MovementImpl`) |
| 3 | шаг, поворот, скорость | `Mobile.Move` (`Server/Mobiles/Mobile.cs`), `PlayerMobile.ComputeMovementSpeed` (`UOContent/Mobiles/PlayerMobile.cs`), обработчик — `IncomingMovementPackets.cs`, ответы — `OutgoingMovementPackets.cs` |
| 3 | готовые отладочные команды движения | `UOContent/Commands/MovementDebugCommands.cs` |
| 4 | речь, журнал | `IncomingMessagePackets.cs`, `OutgoingMessagePackets*.cs`, ключевые слова — `Server/KeywordList.cs` |
| 4 | статус, навыки | `OutgoingMobilePackets.cs` (0x11), `Server/Skills.cs`, `UOContent/Skills/`, `Distribution/Data/skills.json` |
| 4 | прицел | `Server/Targeting/`, `IncomingTargetingPackets.cs`, `OutgoingTargetPackets.cs` |
| 4 | бой, урон, смерть | `UOContent/Items/Weapons/BaseWeapon.cs`, `OutgoingCombatPackets.cs`, `OutgoingDamagePackets.cs`, `Mobile.Kill`/`Resurrect` |
| 4 | преступники, убийцы, стража | `Server/Mobiles/Notoriety.cs`, `UOContent/Misc/Notoriety*.cs`, `UOContent/Mobiles/Guards/`, `Engines/Player Murder System/` |
| 5 | поднять, положить, надеть | `IncomingItemPackets.cs`, `Network/ContainerGridPacketHandler.cs`, `Mobile.Lift`/`Drop` |
| 5 | контейнеры | `Server/Items/Container.cs`, `OutgoingContainerPackets.cs`, `Distribution/Data/containers.cfg` |
| 6 | гампы | `UOContent/Gumps/Base/`, `dev-docs/gump-system.md`; меню 0x7C — `Server/Menus/`, `OutgoingMenuPackets.cs`; запросы ввода — `Server/Prompt.cs` |
| 6 | крафт через меню (эра UOR) | `UOContent/Engines/Craft/T2A/`, `dev-docs/t2a-crafting.md` |
| 7 | магия | `UOContent/Spells/` (круги First…Eighth), `Items/Skill Items/Magical/Spellbook.cs` |
| 7 | вендоры | `UOContent/Mobiles/Vendors/BaseVendor.cs`, `IncomingVendorPackets.cs` |
| 7 | обмен, группа, гильдии | `Server/SecureTrade.cs`, `Engines/Party/`, `UOContent/Misc/Guild.cs` |
| 7 | оглушение и обезоруживание | `UOContent/Items/Weapons/Fists.cs` |
| 8 | дома, корабли | `UOContent/Multis/Houses/`, `UOContent/Multis/Boats/` |
| все | GM-команды (`[go`, `[add`, `[set` …) | `UOContent/Commands/`, `Server/Commands.cs`, `dev-docs/commands-targeting.md`; префикс — `commandsystem.prefix` |

## 6. Наши правки сервера

Полный журнал — [`server-patches.md`](server-patches.md). Проверка полноты журнала: в папке сервера `git diff --stat upstream/main...theverge/uor-baseline -- Projects`.
