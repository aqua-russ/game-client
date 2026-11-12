# DEV-ENV — среда разработки и запуск проверок

Что поставить и как запускать проверки: на Mac пользователя и в среде ИИ-агента. Что считается «готово» — `AGENTS.md` §7; здесь только «как».

## 1. Mac пользователя

| Что | Зачем | Как поставить |
|---|---|---|
| Unity Hub и Unity `6000.6.8f1` | редактор и компиляция Unity-части | Unity Hub → Installs |
| .NET SDK 10 | сборка, тесты, бот, сборка сервера | https://dotnet.microsoft.com/download |
| Инструменты разработчика Xcode | `python3` для проверок `tools/check` | в Терминале: `xcode-select --install` |
| VS Code | отправка на GitHub кнопкой «Sync Changes» (раз в день, `AGENTS.md` §10) | https://code.visualstudio.com |

Проверки — в Терминале из папки `client/The Verge`:
- `tools/check/all.sh` — сборка, тесты, правила архитектуры, запуск бота; в конце `ОК` или `ОШИБКА`.
- `tools/check/all.sh --unity` — то же плюс компиляция в Unity; **редактор Unity должен быть закрыт**.
- Бот против запущенного сервера: один раз `tools/bot/setup.sh` (логин и пароль — в `remaster/secrets/bot.json`, вне git), затем `tools/bot/run.sh`; справка — `tools/bot/run.sh --help`. Лог пакетов — `Logs/packets/`.
- Подробные логи — `Logs/check/`. Упавшие тесты `all.sh` печатает сам (название и причина), полный отчёт — `Logs/check/tests.trx`.

## 2. Среда агента

Агент работает в Linux-машине на Mac пользователя; папка `~/Projects/remaster` подключается к сессии и видна как `~/mnt/remaster`. Unity там нет, поэтому `all.sh` запускается без `--unity`.

1. **.NET SDK 10** ставится в `$HOME/.dotnet`: скачать архив `dotnet-sdk-10.0.*-linux-arm64.tar.gz` с builds.dotnet.microsoft.com (или `dotnet-install.sh --channel 10.0`) и распаковать обычным вызовом — запуск в фоне обрывается. Дальше в каждой команде: `export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet TMPDIR=$HOME/tmp MSBUILDDISABLENODEREUSE=1` (папку `$HOME/tmp` создать).
2. **NuGet.** Проверить `curl -sS -o /dev/null -w '%{http_code}' https://api.nuget.org/v3/index.json`: `200` — настоящий `dotnet test`. Иначе — временная замена NUnit **вне репозитория** и прямо сказать об этом в ответе пользователю; окончательная проверка — `all.sh` на Mac.
3. **Разрешение на удаление.** Сборка в папке проекта и `git commit` удаляют временные файлы (NuGet, блокировки git), а по умолчанию удаление запрещено. Запросить у пользователя одним запросом на папку `remaster`.
4. **Отдельный `dotnet test`** может падать с `MSB1025 … Address already in use` (чужие сокеты MSBuild в общем `/tmp`): помогает `TMPDIR` из п. 1 и запуск `dotnet build`, затем `dotnet test --no-build -m:1`, как в `all.sh`.
5. **Автор коммитов** в этой среде не настроен: `git -c user.name="The Verge" -c user.email=noreply@example.com commit …`. Формат сообщения и что не коммитить — `AGENTS.md` §10.
