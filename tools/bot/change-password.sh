#!/usr/bin/env bash
# Смена пароля аккаунта на вашем сервере одной командой: tools/bot/change-password.sh [логин] (по умолчанию admin).
# Сам запускает ваш сервер (он должен быть остановлен), входит ботом со старым паролем и ставит новый случайный
# (игровая команда [password — на время работы скрипт включает её настройкой accountHandler.enablePlayerPasswordCommand
# и потом возвращает ваш файл настроек как был), входит с новым паролем и сохраняет мир ([save — нужен аккаунт уровня
# Administrator или Owner), проверяет, что старый пароль больше не подходит, и останавливает сервер. Новый пароль печатается и сохраняется в
# remaster/secrets/<логин>.txt (вне git, доступен только вам). Старый пароль спрашивается скрыто (Enter — «admin»).
# Печатает «ОК» или «ОШИБКА: …». Логи — Logs/check/password-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
LOGS="$ROOT/Logs/check"
SERVER_DIR="${THEVERGE_SERVER_DIR:-$ROOT/../../server/ModernUO/Distribution}"
SECRETS_DIR="${THEVERGE_SECRETS_DIR:-$ROOT/../../secrets}"
BOT_DLL="$ROOT/dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll"
LOGIN="${1:-admin}"
TMP="$(mktemp -d)"
SERVER_PID=""
mkdir -p "$LOGS"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

port_open() { python3 -c "import socket,sys; s=socket.socket(); s.settimeout(1); sys.exit(s.connect_ex(('127.0.0.1',2593)))"; }
stop_server() {
  [ -z "$SERVER_PID" ] && return
  kill -INT "$SERVER_PID" 2>/dev/null
  for _ in $(seq 1 60); do kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 0.5; done
  kill -9 "$SERVER_PID" 2>/dev/null
  SERVER_PID=""
}
CONFIG="$SERVER_DIR/Configuration/modernuo.json"
restore_config() { [ -f "$TMP/modernuo.json" ] && cp "$TMP/modernuo.json" "$CONFIG"; }
cleanup() { stop_server; restore_config; rm -rf "$TMP"; }
trap cleanup EXIT
fail() { echo "ОШИБКА: $1"; exit 1; }
bot() { dotnet "$BOT_DLL" "$@"; }
json() { python3 -c 'import json,sys; print(json.dumps({"login": sys.argv[1], "password": sys.argv[2]}))' "$1" "$2"; }
mtime() { python3 -c 'import os,sys; print(int(os.path.getmtime(sys.argv[1])) if os.path.exists(sys.argv[1]) else 0)' "$1"; }

if [ -t 0 ]; then
  printf "Старый пароль аккаунта %s (Enter — admin): " "$LOGIN"; read -r -s OLD; echo
else
  read -r OLD || OLD=""
fi
[ -n "$OLD" ] || OLD="admin"
NEW="$(python3 -c 'import secrets; print(secrets.token_urlsafe(12))')"
json "$LOGIN" "$OLD" > "$TMP/old.json"
json "$LOGIN" "$NEW" > "$TMP/new.json"
chmod 600 "$TMP/old.json" "$TMP/new.json"

echo "1/5 Сборка бота…"
dotnet build dotnet/TheVerge.Bot -nologo -v:q -tl:off > "$LOGS/password-build.log" 2>&1 \
  || fail "бот не собрался. Лог: Logs/check/password-build.log"
[ -f "$SERVER_DIR/ModernUO.dll" ] || fail "нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"
port_open && fail "сервер уже запущен (порт 2593 занят). Остановите его (Ctrl+C в его окне) и запустите снова."

[ -f "$CONFIG" ] || fail "нет настроек сервера $CONFIG — сначала запустите сервер один раз вручную."
cp "$CONFIG" "$TMP/modernuo.json"
python3 - "$CONFIG" <<'PY' || fail "не удалось включить команду [password в $CONFIG"
import json, sys
p = sys.argv[1]; d = json.load(open(p))
d.setdefault("settings", d)["accountHandler.enablePlayerPasswordCommand"] = "True"
json.dump(d, open(p, "w"), indent=2)
PY

echo "2/5 Запуск сервера (до 90 с)…"
(cd "$SERVER_DIR" && exec dotnet ModernUO.dll > "$LOGS/password-server.log" 2>&1 < /dev/null) &
SERVER_PID=$!
for _ in $(seq 1 90); do grep -q "Listening: " "$LOGS/password-server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
grep -q "Listening: " "$LOGS/password-server.log" 2>/dev/null || { tail -n 15 "$LOGS/password-server.log"; fail "сервер не запустился. Лог: Logs/check/password-server.log"; }
ACCOUNTS="$SERVER_DIR/Saves/Accounts/Accounts.bin"
BEFORE="$(mtime "$ACCOUNTS")"

echo "3/5 Вход со старым паролем и смена пароля…"
CHANGE=(--action "say:[password \"$NEW\" \"$NEW\"" --idle 5 --no-packet-log)
bot --config "$TMP/old.json" "${CHANGE[@]}" > "$LOGS/password-change.log" 2>&1
CODE=$?
if [ "$CODE" -ne 0 ] && grep -q "на аккаунте нет персонажей" "$LOGS/password-change.log"; then
  bot --config "$TMP/old.json" --character Admin --create-character "${CHANGE[@]}" > "$LOGS/password-change.log" 2>&1
  CODE=$?
fi
if [ "$CODE" -eq 6 ]; then
  echo "ОК: старый пароль к аккаунту $LOGIN не подходит — менять нечего."
  exit 0
fi
[ "$CODE" -eq 0 ] || { tail -n 8 "$LOGS/password-change.log"; fail "бот не смог войти и сменить пароль (код $CODE). Лог: Logs/check/password-change.log"; }

echo "4/5 Вход с новым паролем и сохранение мира; старый пароль не подходит…"
bot --config "$TMP/new.json" --action "say:[save" --idle 5 --no-packet-log > "$LOGS/password-new.log" 2>&1
[ $? -eq 0 ] || fail "вход с новым паролем не прошёл — пароль не сменён. Лог: Logs/check/password-new.log"
bot --config "$TMP/old.json" --idle 1 --no-packet-log > "$LOGS/password-old.log" 2>&1
[ $? -eq 6 ] || fail "старый пароль всё ещё подходит. Лог: Logs/check/password-old.log"
AFTER="$(mtime "$ACCOUNTS")"
[ "$AFTER" -gt "$BEFORE" ] || fail "пароль сменён, но мир не сохранён (у аккаунта $LOGIN нет права [save) — после остановки сервера вернётся старый пароль. Смените пароль из-под аккаунта владельца."

echo "5/5 Остановка сервера, возврат настроек и запись нового пароля…"
stop_server
restore_config
mkdir -p "$SECRETS_DIR"
printf '%s\n' "$NEW" > "$SECRETS_DIR/$LOGIN.txt"
chmod 600 "$SECRETS_DIR/$LOGIN.txt"
echo "Новый пароль аккаунта $LOGIN: $NEW"
echo "Сохранён в remaster/secrets/$LOGIN.txt (вне git)."
echo "ОК"
