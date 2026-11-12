#!/usr/bin/env bash
# Запуск бота из папки client/The Verge: tools/bot/run.sh [параметры]. Справка: tools/bot/run.sh --help.
# Логин и пароль — из remaster/secrets/bot.json (создать: tools/bot/setup.sh). Код выхода — как у бота.
# Бот запускается через dotnet и dll, а не готовым файлом-запускателем: папка проекта общая для Mac и среды агента,
# и запускатель, собранный под другую систему, на этой не стартует.
cd "$(dirname "$0")/../.." || exit 1
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if ! dotnet build dotnet/TheVerge.Bot -nologo -v:q -tl:off > /dev/null 2>&1; then
  echo "ОШИБКА: бот не собрался. Подробности: dotnet build dotnet/TheVerge.Bot"
  exit 1
fi
exec dotnet dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll "$@"
