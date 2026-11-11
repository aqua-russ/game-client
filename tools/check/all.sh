#!/usr/bin/env bash
# Проверка «готово» (AGENTS.md §7) одной командой: сборка, тесты, архитектура, запуск бота.
# Печатает «ОК» или «ОШИБКА: …». Ключ --unity дополнительно проверяет компиляцию в Unity (tools/check/unity.sh).
# Подробные логи — в Logs/check/.
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
LOGS="$ROOT/Logs/check"
mkdir -p "$LOGS"
SLN="dotnet/TheVerge.sln"
FAILED=0

fail() { echo "ОШИБКА: $1"; FAILED=1; }
show_tail() { echo "----- последние строки $1 -----"; tail -n "${2:-25}" "$1"; echo "-----"; }

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ОШИБКА: не найден dotnet. Установите .NET SDK 10: https://dotnet.microsoft.com/download"
  exit 1
fi
if ! command -v python3 >/dev/null 2>&1; then
  echo "ОШИБКА: не найден python3. На Mac выполните в Терминале: xcode-select --install"
  exit 1
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "1/4 Сборка dotnet…"
if dotnet build "$SLN" --no-incremental -nologo -tl:off -clp:NoSummary -v:q > "$LOGS/build.log" 2>&1; then
  if ! python3 tools/check/dotnet_warnings.py "$LOGS/build.log"; then
    fail "появились новые предупреждения сборки (список выше). Исправьте код."
  fi
else
  if grep -q "NU1301\|Unable to load the service index" "$LOGS/build.log"; then
    fail "не удалось скачать библиотеки NuGet. Проверьте интернет и запустите снова."
  else
    fail "сборка не прошла."
  fi
  grep -E ": error " "$LOGS/build.log" | sort -u | head -n 30
  echo "Полный лог: Logs/check/build.log"
  echo "ОШИБКА"
  exit 1
fi

echo "2/4 Тесты…"
if dotnet test "$SLN" --no-build -nologo -tl:off -v:q > "$LOGS/test.log" 2>&1; then
  grep -E "Passed!|Пройден" "$LOGS/test.log" | head -n 5
else
  fail "упали тесты."
  show_tail "$LOGS/test.log" 40
fi

echo "3/4 Архитектура…"
if ! tools/check/architecture.sh > "$LOGS/architecture.log" 2>&1; then
  fail "нарушены правила архитектуры."
  grep -v "^ОК$" "$LOGS/architecture.log"
else
  tail -n 2 "$LOGS/architecture.log" | head -n 1
fi

echo "4/4 Запуск бота…"
if dotnet run --project dotnet/TheVerge.Bot --no-build > "$LOGS/bot.log" 2>&1 && grep -q "TheVerge.Bot: ОК" "$LOGS/bot.log"; then
  grep "TheVerge.Bot: ОК" "$LOGS/bot.log"
else
  fail "бот не запустился."
  show_tail "$LOGS/bot.log" 20
fi

if [ "${1:-}" = "--unity" ]; then
  echo "+ Компиляция в Unity…"
  if ! tools/check/unity.sh; then
    fail "Unity не компилирует проект."
  fi
fi

if [ "$FAILED" -eq 0 ]; then
  echo "ОК"
else
  echo "ОШИБКА: см. сообщения выше."
  exit 1
fi
