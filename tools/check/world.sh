#!/usr/bin/env bash
# Сверка мира бота с сервером (PLAN Стадия 2, часть А: задачи 2.7–2.11) одной командой. Печатает «ОК» или «ОШИБКА: …».
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются.
# В пустом мире сервер сам предлагает создать владельца — проверка отвечает за вас (случайный пароль, только на время
# проверки). Бот-GM сверяет свой мир с дампом сервера ([DumpVisible): сразу после входа, после [add Apple и открытия
# рюкзака, после [remove; затем обычный игрок пробует [DumpVisible — сервер не должен его выполнить.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Подробности — Logs/check/world-*.log, лог сервера — Logs/check/world-server.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
LOGS="$ROOT/Logs/check"
SERVER_DIR="${THEVERGE_SERVER_DIR:-$ROOT/../../server/ModernUO/Distribution}"
BOT_DLL="$ROOT/dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll"
TMP="$(mktemp -d)"
COPY="$TMP/server"
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
  if kill -0 "$SERVER_PID" 2>/dev/null; then
    echo "  ВНИМАНИЕ: копия сервера не остановилась сама за 30 с — остановлена принудительно (ваши данные не затронуты)."
    pkill -9 -P "$SERVER_PID" 2>/dev/null; kill -9 "$SERVER_PID" 2>/dev/null
  fi
  wait "$SERVER_PID" 2>/dev/null
  SERVER_PID=""
}
cleanup() { stop_server; rm -rf "$TMP"; }
trap cleanup EXIT
bot() { dotnet "$BOT_DLL" "$@"; }

echo "1/4 Сборка бота и временная копия сервера…"
if ! dotnet build dotnet/TheVerge.Bot -nologo -v:q -tl:off > "$LOGS/world-build.log" 2>&1; then
  echo "ОШИБКА: бот не собрался. Лог: Logs/check/world-build.log"; exit 1
fi
[ -f "$SERVER_DIR/ModernUO.dll" ] || { echo "ОШИБКА: нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
if port_open; then
  echo "ОШИБКА: сервер уже запущен (порт 2593 занят). Остановите его (Ctrl+C в его окне) и запустите проверку снова."; exit 1
fi
mkdir -p "$COPY"
rsync -a --exclude Saves --exclude Logs --exclude Backups --exclude Archives --exclude temp "$SERVER_DIR/" "$COPY/"
ok "копия сервера с пустым миром: $COPY"

OWNER="worldcheck$(python3 -c 'import secrets; print(secrets.token_hex(3))')"
PASSWORD="check-$(python3 -c 'import secrets; print(secrets.token_hex(6))')"
printf '{"login":"%s","password":"%s","character":"Worldcheck"}\n' "$OWNER" "$PASSWORD" > "$TMP/gm.json"
printf '{"login":"%sp","password":"%s","character":"Worldplayer"}\n' "$OWNER" "$PASSWORD" > "$TMP/player.json"
chmod 600 "$TMP/gm.json" "$TMP/player.json"

echo "2/4 Запуск копии сервера (до 90 с)…"
: > "$LOGS/world-server.log"
(cd "$COPY" && exec python3 "$ROOT/tools/check/pty_server.py" --log "$LOGS/world-server.log" \
  --answer "owner account now=y" --answer "Input Username=$OWNER" --answer "Input Password=$PASSWORD" -- dotnet ModernUO.dll) &
SERVER_PID=$!
for _ in $(seq 1 90); do grep -aq "Listening: " "$LOGS/world-server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
if ! grep -aq "Listening: " "$LOGS/world-server.log"; then
  echo "ОШИБКА: копия сервера не запустилась. Лог: Logs/check/world-server.log"; tail -n 15 "$LOGS/world-server.log"; exit 1
fi
grep -aq "Owner account created" "$LOGS/world-server.log" && ok "сервер запущен, временный владелец создан" || fail "владелец не создан (Logs/check/world-server.log)"

echo "3/4 Бот-GM сверяет свой мир с сервером…"
PARITY="$COPY/Logs/parity"
bot --config "$TMP/gm.json" --create-character --no-packet-log \
  --action "dump:$PARITY" \
  --action "say:[add Apple" --action target:here --action seen:0x09D0 --action use:backpack --action "dump:$PARITY" \
  --action "say:[remove" --action target:0x09D0 --action "dump:$PARITY" --idle 1 > "$LOGS/world-gm.log" 2>&1
CODE=$?
COUNT="$(grep -c 'Сверка с сервером: совпадает' "$LOGS/world-gm.log")"
if [ "$CODE" -eq 0 ] && [ "$COUNT" -eq 3 ]; then
  ok "мир бота совпал с сервером 3 раза: $(grep -o 'совпадает объектов [0-9]*' "$LOGS/world-gm.log" | sed 's/совпадает объектов //' | paste -sd, - | sed 's/,/, /g') объектов (вход; яблоко и рюкзак; после [remove)"
else
  fail "сверка бота с сервером: код $CODE, совпало $COUNT из 3. Лог: Logs/check/world-gm.log"
  grep -E "Расхождение|не может|ОШИБКА" "$LOGS/world-gm.log" | head -n 10
fi
grep -q "Рюкзак 0x" "$LOGS/world-gm.log" && ok "$(grep -o 'Рюкзак 0x[0-9A-F]* открыт: предметов [0-9]*' "$LOGS/world-gm.log" | head -n 1)" \
  || fail "рюкзак не открыт. Лог: Logs/check/world-gm.log"
grep -q "Предмет 0x09D0 на земле" "$LOGS/world-gm.log" && ok "[add Apple + прицел в клетку под ногами: яблоко появилось" \
  || fail "яблоко не появилось. Лог: Logs/check/world-gm.log"

echo "4/4 Обычный игрок пробует [DumpVisible…"
bot --config "$TMP/player.json" --create-character --no-packet-log --action "dump:$PARITY" > "$LOGS/world-player.log" 2>&1
if grep -q "сервер не записал дамп" "$LOGS/world-player.log"; then ok "игроку команда недоступна: дампа нет"; else fail "у игрока дамп записан или бот упал иначе. Лог: Logs/check/world-player.log"; fi

stop_server
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
