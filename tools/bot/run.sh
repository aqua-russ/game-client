#!/usr/bin/env bash
# Запуск бота из папки client/The Verge: tools/bot/run.sh [параметры]. Справка: tools/bot/run.sh --help.
# Логин и пароль — из remaster/secrets/bot.json (создать: tools/bot/setup.sh). Код выхода — как у бота.
cd "$(dirname "$0")/../.." || exit 1
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
exec dotnet run --project dotnet/TheVerge.Bot -v:q -- "$@"
