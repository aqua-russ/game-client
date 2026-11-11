#!/usr/bin/env bash
# Компиляция проекта в Unity без открытия редактора (AGENTS.md §7). Только Mac с установленной Unity.
# Печатает «ОК» или «ОШИБКА: …». Путь к Unity можно задать вручную: UNITY_BIN=/путь/к/Unity tools/check/unity.sh
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
LOG="$ROOT/Logs/check/unity.log"
mkdir -p "$ROOT/Logs/check"

VERSION="$(sed -n 's/^m_EditorVersion: *//p' ProjectSettings/ProjectVersion.txt | tr -d '\r')"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"

if [ ! -x "$UNITY_BIN" ]; then
  echo "ОШИБКА: не найдена Unity $VERSION ($UNITY_BIN). Установите эту версию через Unity Hub."
  exit 1
fi
if [ -f Temp/UnityLockfile ] && pgrep -f "Unity.*-[Pp]roject[Pp]ath.*$(basename "$ROOT")" >/dev/null 2>&1; then
  echo "ОШИБКА: проект открыт в Unity. Закройте редактор Unity и запустите проверку снова."
  exit 1
fi

echo "Unity $VERSION компилирует проект (1–5 минут)…"
"$UNITY_BIN" -batchmode -nographics -quit -projectPath "$ROOT" -logFile "$LOG" > /dev/null 2>&1
CODE=$?

ERRORS="$(grep -E "error CS[0-9]+" "$LOG" | sort -u)"
if [ "$CODE" -ne 0 ] || [ -n "$ERRORS" ] || grep -q "Scripts have compiler errors" "$LOG"; then
  echo "ОШИБКА: Unity не скомпилировала проект (код выхода $CODE)."
  if [ -n "$ERRORS" ]; then echo "$ERRORS" | head -n 30; fi
  echo "Полный лог: Logs/check/unity.log"
  exit 1
fi

WARNINGS="$(grep -cE "warning CS[0-9]+" "$LOG")"
echo "Unity: ошибок компиляции нет (предупреждений в логе: $WARNINGS)."
echo "ОК"
