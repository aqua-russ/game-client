#!/usr/bin/env bash
# Долгие критерии Стадии 1 (docs/PLAN.md, Стадия 1, «Критерии готовности») — сценарий бота login-idle-logout против
# настоящего сервера: 100 входов подряд без сбоев, затем 2 часа в мире без разрыва, выход без ошибок на сервере.
# Запуск: tools/check/stage1-long.sh          — полная проверка (около 2 часов 5 минут; Mac не должен засыпать);
#         tools/check/stage1-long.sh --short  — короткая: 5 входов и 60 с в мире (около 3 минут).
# Сам запускает и останавливает сервер (он должен быть остановлен). Логин и пароль — ваши (tools/bot/setup.sh), файл
# только читается. Печатает «ОК» или «ОШИБКА: …». Логи — Logs/check/stage1-*.log, лог сервера — Logs/check/stage1-server.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
LOGS="$ROOT/Logs/check"
SERVER_DIR="${THEVERGE_SERVER_DIR:-$ROOT/../../server/ModernUO/Distribution}"
SECRETS="${THEVERGE_SECRETS_DIR:-$ROOT/../../secrets}/bot.json"
BOT_DLL="$ROOT/dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll"
LOGINS=100
IDLE=7200
[ "${1:-}" = "--short" ] && { LOGINS=5; IDLE=60; }
SERVER_PID=""
FAILED=0
mkdir -p "$LOGS"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

ok() { echo "  ОК: $1"; }
fail() { echo "  ОШИБКА: $1"; FAILED=1; }
port_open() { python3 -c "import socket,sys; s=socket.socket(); s.settimeout(1); sys.exit(s.connect_ex(('127.0.0.1',2593)))"; }
stop_server() {
  [ -z "$SERVER_PID" ] && return
  kill -INT "$SERVER_PID" 2>/dev/null
  for _ in $(seq 1 60); do kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 0.5; done
  kill -9 "$SERVER_PID" 2>/dev/null
  SERVER_PID=""
}
trap stop_server EXIT
# Mac не засыпает, пока идёт проверка (caffeinate есть только на macOS).
awake() { if command -v caffeinate >/dev/null 2>&1; then caffeinate -i "$@"; else "$@"; fi; }
# THEVERGE_BOT_CHECK_CREATE=1 — создать персонажа, если его нет (чистый стенд в среде агента; ваш аккаунт так не трогается).
CREATE=()
[ "${THEVERGE_BOT_CHECK_CREATE:-}" = "1" ] && CREATE=(--create-character)

echo "1/5 Сборка бота…"
dotnet build dotnet/TheVerge.Bot -nologo -v:q -tl:off > "$LOGS/stage1-build.log" 2>&1 || { echo "ОШИБКА: бот не собрался. Лог: Logs/check/stage1-build.log"; exit 1; }
[ -f "$SERVER_DIR/ModernUO.dll" ] || { echo "ОШИБКА: нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
[ -f "$SECRETS" ] || { echo "ОШИБКА: нет логина бота ($SECRETS). Сохраните его: tools/bot/setup.sh"; exit 1; }
port_open && { echo "ОШИБКА: сервер уже запущен (порт 2593 занят). Остановите его и запустите проверку снова."; exit 1; }

echo "2/5 Запуск сервера (до 90 с)…"
(cd "$SERVER_DIR" && exec dotnet ModernUO.dll > "$LOGS/stage1-server.log" 2>&1 < /dev/null) &
SERVER_PID=$!
for _ in $(seq 1 90); do grep -q "Listening: " "$LOGS/stage1-server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
grep -q "Listening: " "$LOGS/stage1-server.log" 2>/dev/null || { echo "ОШИБКА: сервер не запустился. Лог: Logs/check/stage1-server.log"; exit 1; }
ok "сервер запущен"

echo "3/5 $LOGINS входов подряд: вход, мир, выход…"
BAD=0
for i in $(seq 1 "$LOGINS"); do
  dotnet "$BOT_DLL" --config "$SECRETS" --no-packet-log "${CREATE[@]}" > "$LOGS/stage1-login.log" 2>&1
  CODE=$?
  if [ "$CODE" -ne 0 ] || ! grep -q "Вышел из игры" "$LOGS/stage1-login.log"; then
    BAD=$((BAD + 1))
    cp "$LOGS/stage1-login.log" "$LOGS/stage1-login-failed-$i.log"
    echo "  вход $i: код $CODE — $(grep -E 'ОШИБКА' "$LOGS/stage1-login.log" | head -n 1) (лог: Logs/check/stage1-login-failed-$i.log)"
  fi
  CREATE=()
  [ $((i % 10)) -eq 0 ] && echo "  … $i из $LOGINS"
done
[ "$BAD" -eq 0 ] && ok "$LOGINS входов подряд, сбоев 0" || fail "сбоев $BAD из $LOGINS входов"

echo "4/5 $IDLE с в мире без разрыва, затем выход…"
START=$(date +%s)
awake dotnet "$BOT_DLL" --config "$SECRETS" --idle "$IDLE" > "$LOGS/stage1-idle.log" 2>&1
CODE=$?
ELAPSED=$(( $(date +%s) - START ))
if [ "$CODE" -eq 0 ] && grep -q "Вышел из игры" "$LOGS/stage1-idle.log" && [ "$ELAPSED" -ge "$IDLE" ]; then
  ok "в мире $ELAPSED с без разрыва, выход ($(grep -o 'задержка до сервера [0-9]* мс' "$LOGS/stage1-idle.log" | tail -n 1))"
else
  fail "в мире: код $CODE через $ELAPSED с из $IDLE — $(grep -E 'ОШИБКА' "$LOGS/stage1-idle.log" | head -n 1). Лог: Logs/check/stage1-idle.log"
fi

echo "5/5 Остановка сервера и ошибки в его логе…"
stop_server
ERRORS="$(grep -E '\[(ERR|FTL)\]|Unhandled exception|Exception:' "$LOGS/stage1-server.log" | head -n 5)"
[ -z "$ERRORS" ] && ok "в логе сервера ошибок нет" || { fail "в логе сервера есть ошибки (Logs/check/stage1-server.log):"; echo "$ERRORS"; }

if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
