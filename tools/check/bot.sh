#!/usr/bin/env bash
# Проверка бота против настоящего сервера ModernUO (задачи 1.5–1.15) одной командой: сам запускает сервер, гоняет
# бота и останавливает сервер. Печатает «ОК» или «ОШИБКА: …». Ваш файл с паролем (remaster/secrets) не трогается:
# проверка пишет временные настройки. Сервер должен быть остановлен: проверке нужен свежий запуск (список исключений
# 127.0.0.1 читается при старте). Подробности — Logs/check/bot-*.log, лог сервера — Logs/check/server.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
LOGS="$ROOT/Logs/check"
SERVER_DIR="${THEVERGE_SERVER_DIR:-$ROOT/../../server/ModernUO/Distribution}"
BOT_DLL="$ROOT/dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll"
PTY="$ROOT/tools/check/pty_dialog.py"
TMP="$(mktemp -d)"
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
cleanup() { stop_server; rm -rf "$TMP"; }
trap cleanup EXIT

# Бот запускается через dotnet и dll — одинаково на Mac и в Linux.
bot() { dotnet "$BOT_DLL" "$@"; }
expect_code() { # что проверяем, ожидаемый код, лог, команда…
  local what="$1" want="$2" log="$3"; shift 3
  "$@" > "$LOGS/$log" 2>&1
  local got=$?
  if [ "$got" -eq "$want" ]; then ok "$what (код $got)"; else fail "$what: код $got, ждали $want. Лог: Logs/check/$log"; tail -n 5 "$LOGS/$log"; fi
}

echo "1/6 Сборка бота…"
if ! dotnet build dotnet/TheVerge.Bot -nologo -v:q -tl:off > "$LOGS/bot-build.log" 2>&1; then
  echo "ОШИБКА: бот не собрался. Лог: Logs/check/bot-build.log"; exit 1
fi
[ -f "$SERVER_DIR/ModernUO.dll" ] || { echo "ОШИБКА: нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
if port_open; then
  echo "ОШИБКА: сервер уже запущен (порт 2593 занят). Остановите его (Ctrl+C в его окне) и запустите проверку снова."; exit 1
fi

# Ваш логин бота (tools/bot/setup.sh) — только читается. Проверка входит с ним и с заведомо неверным паролем.
SECRETS="${THEVERGE_SECRETS_DIR:-$ROOT/../../secrets}/bot.json"
LOGIN="$(python3 -c "import json,sys; print(json.load(open(sys.argv[1])).get('login',''))" "$SECRETS" 2>/dev/null)"
[ -n "$LOGIN" ] || { echo "ОШИБКА: нет логина бота в $SECRETS. Сохраните его: tools/bot/setup.sh"; exit 1; }
PASSWORD="check-$(python3 -c 'import secrets; print(secrets.token_hex(6))')"
printf '{"login":"%s","password":"%s"}\n' "$LOGIN" "$PASSWORD" > "$TMP/bot.json"
printf '{"login":"botcheck"}\n' > "$TMP/nopass.json"
chmod 600 "$TMP/bot.json" "$TMP/nopass.json"

echo "2/6 Скрытый ввод пароля и Ctrl+C (сервер не нужен)…"
expect_code "пароль спрошен и не показан на экране" 2 bot-password.log \
  python3 "$PTY" --secret "$PASSWORD" --answer "Пароль=$PASSWORD" -- dotnet "$BOT_DLL" --config "$TMP/nopass.json" --no-packet-log --port 1
expect_code "setup.sh: пароль не показан, файл записан" 0 bot-setup.log \
  env THEVERGE_SECRETS_DIR="$TMP/secrets" python3 "$PTY" --secret "$PASSWORD" \
  --answer "Логин=botcheck" --answer "Пароль=$PASSWORD" --answer "Персонаж=" --answer "Сервер=" --answer "Порт=" -- tools/bot/setup.sh
MODE="$(python3 -c "import os,stat,sys; print(oct(stat.S_IMODE(os.stat(sys.argv[1]).st_mode)))" "$TMP/secrets/bot.json" 2>/dev/null)"
[ "$MODE" = "0o600" ] && ok "файл с паролем доступен только владельцу" || fail "права файла с паролем: $MODE, ждали 0o600"
expect_code "Ctrl+C во время подключения" 130 bot-ctrl-c.log \
  python3 "$PTY" --ctrl-c-after 1.5 -- dotnet "$BOT_DLL" --config "$TMP/bot.json" --no-packet-log --host 10.255.255.1

echo "3/6 Запуск сервера (до 90 с)…"
(cd "$SERVER_DIR" && exec dotnet ModernUO.dll > "$LOGS/server.log" 2>&1 < /dev/null) &
SERVER_PID=$!
for _ in $(seq 1 90); do grep -q "Listening: " "$LOGS/server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
if ! grep -q "Listening: " "$LOGS/server.log" 2>/dev/null; then
  echo "ОШИБКА: сервер не запустился. Лог: Logs/check/server.log"; tail -n 15 "$LOGS/server.log"; exit 1
fi
grep -q "Manual allowlist loaded" "$LOGS/server.log" && ok "сервер запущен, список исключений 127.0.0.1 включён" \
  || fail "сервер запущен, но список исключений не включён (нет ip-allowlist.json с enabled: true — шаблон ConfigurationTemplates/theverge)"

echo "4/6 Бот против сервера…"
# Сначала вход с настоящим паролем, потом неверный: при автосоздании аккаунта неверный пароль первым создал бы
# аккаунт со случайным паролем.
bot --config "$SECRETS" > "$LOGS/bot-login.log" 2>&1
CODE=$?
if [ "$CODE" -eq 0 ] && grep -q "Игровой сервер: " "$LOGS/bot-login.log"; then
  ok "вход с вашим логином: $(grep -o 'Выбран шард «[^»]*»' "$LOGS/bot-login.log" | head -n 1), $(grep -oE 'Игровой сервер: [0-9.]*[0-9]:[0-9]+' "$LOGS/bot-login.log" | head -n 1) (код 0)"
elif [ "$CODE" -eq 6 ]; then
  fail "вход с вашим логином: сервер отказал ($(grep -o 'отказал во входе: [^(.]*' "$LOGS/bot-login.log" | head -n 1)). Если причина «сервер не пустил по своей причине» — аккаунт бота не создан: см. PROGRESS.md, «Вопросы пользователю». Лог: Logs/check/bot-login.log"
else
  fail "вход с вашим логином: код $CODE, ждали 0. Лог: Logs/check/bot-login.log"; tail -n 5 "$LOGS/bot-login.log"
fi
LOGIN_PACKETS="$(ls -t "$ROOT/Logs/packets"/*.tsv 2>/dev/null | head -n 1)"
if python3 - "$SECRETS" "$LOGS/bot-login.log" ${LOGIN_PACKETS:+"$LOGIN_PACKETS"} <<'PY'
import json, sys
secret = json.load(open(sys.argv[1])).get("password", "")
hexed = " ".join("%02X" % b for b in secret.encode("latin-1", "replace"))
sys.exit(1 if secret and any(secret in open(f, encoding="utf-8", errors="replace").read() or hexed in open(f, encoding="utf-8", errors="replace").read() for f in sys.argv[2:]) else 0)
PY
then ok "вашего пароля нет ни на экране, ни в логе пакетов"; else fail "ваш пароль попал на экран или в лог пакетов"; fi
expect_code "неверный пароль: сервер отказал, бот не завис" 6 bot-run.log bot --config "$TMP/bot.json"
grep -q "отказал во входе" "$LOGS/bot-run.log" && ok "причина отказа названа: $(grep -o 'отказал во входе: [^(.]*' "$LOGS/bot-run.log" | head -n 1)" \
  || fail "нет причины отказа в Logs/check/bot-run.log"
NEWEST="$(ls -t "$ROOT/Logs/packets"/*.tsv 2>/dev/null | head -n 1)"
if [ -n "$NEWEST" ] && grep -q "CONNECTED" "$NEWEST" && grep -q "0xEF" "$NEWEST" && grep -q "0x80" "$NEWEST" && grep -q "CLOSED" "$NEWEST"; then
  ok "лог пакетов записан (подключение, seed, логин, закрытие): ${NEWEST#$ROOT/}"
else fail "в логе пакетов нет CONNECTED, 0xEF, 0x80 и CLOSED"; fi
if grep -q "$PASSWORD" "$LOGS/bot-run.log" ${NEWEST:+"$NEWEST"}; then fail "пароль попал на экран или в лог"; else ok "пароля нет ни на экране, ни в логе"; fi

echo "5/6 Молчаливое подключение на 7 с, затем бот…"
python3 -c "import socket,time; s=socket.create_connection(('127.0.0.1',2593)); time.sleep(7); s.close()"
expect_code "после молчаливого подключения сервер по-прежнему отвечает боту (127.0.0.1 не заблокирован)" 6 bot-after-silent.log bot --config "$TMP/bot.json" --no-packet-log

echo "6/6 Остановка сервера, затем бот…"
stop_server
expect_code "сервер остановлен — бот честно сообщает, что не подключился" 2 bot-no-server.log bot --config "$TMP/bot.json" --no-packet-log

if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
