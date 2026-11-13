#!/usr/bin/env bash
# Сверка чтения файлов данных клиентом с сервером (PLAN Стадия 2, часть Б: 2.2–2.6, 2.11б). Печатает «ОК» или «ОШИБКА».
#
#   tools/check/map.sh <папка с файлами данных> [--cells N] [--seed S] [--self-test]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром, которая читает файлы из указанной папки: ваши сохранения,
# аккаунты и настройки не трогаются (проверяется в конце). Бот-GM говорит [DumpMapData — сервер записывает землю и статику
# N случайных клеток (по умолчанию 10 000; seed пишется в вывод), всю tiledata и все дома и корабли, — и бот сверяет
# с этим свои читатели. Для маленьких карт (статика меньше 1 МБ, как у заглушки) — ещё все клетки со статикой.
# --self-test: затем та же сверка с испорченной копией карты у клиента — должны быть названы клетки с расхождением.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Подробности — Logs/check/map-*.log, лог сервера — Logs/check/map-server.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
LOGS="$ROOT/Logs/check"
SERVER_DIR="${THEVERGE_SERVER_DIR:-$ROOT/../../server/ModernUO/Distribution}"
BOT_DLL="$ROOT/dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll"
CELLS=10000
SEED="$(python3 -c 'import random; print(random.randint(1, 2**31 - 1))')"
SELF_TEST=0
DATA=""
while [ $# -gt 0 ]; do
  case "$1" in
    --cells) CELLS="$2"; shift 2 ;;
    --seed) SEED="$2"; shift 2 ;;
    --self-test) SELF_TEST=1; shift ;;
    -h|--help) sed -n '2,13p' "$0"; exit 0 ;;
    *) DATA="$1"; shift ;;
  esac
done
[ -n "$DATA" ] || { echo "ОШИБКА: укажите папку с файлами данных: tools/check/map.sh <папка>"; exit 1; }
[ -d "$DATA" ] || { echo "ОШИБКА: папка не найдена: $DATA"; exit 1; }
DATA="$(cd "$DATA" && pwd)"
TMP="$(mktemp -d)"
COPY="$TMP/server"
SERVER_PID=""
FAILED=0
mkdir -p "$LOGS"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

ok() { echo "  ОК: $1"; }
fail() { echo "  ОШИБКА: $1"; FAILED=1; }
port_open() { python3 -c "import socket,sys; s=socket.socket(); s.settimeout(1); sys.exit(s.connect_ex(('127.0.0.1',2593)))"; }
saves_print() { [ -d "$SERVER_DIR/Saves" ] && (cd "$SERVER_DIR/Saves" && find . -type f -exec ls -ln {} + 2>/dev/null | awk '{print $5, $6, $7, $8, $9}' | sort | cksum) || echo "нет"; }
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
small_statics() {
  local f
  f="$(find "$DATA" -maxdepth 1 -iname statics0.mul | head -n 1)"
  [ -n "$f" ] && [ "$(wc -c < "$f")" -lt 1048576 ]
}
mapdump() { # $1 — папка данных клиента, $2 — лог
  local action="mapdump:$CELLS,$SEED,$COPY/Logs/parity"
  small_statics && action="$action,all-statics"
  bot --config "$TMP/gm.json" --create-character --no-packet-log --data "$1" --action "$action" > "$2" 2>&1
}

echo "1/4 Сборка бота, проверка папки и временная копия сервера…"
if ! dotnet build dotnet/TheVerge.Bot -nologo -v:q -tl:off > "$LOGS/map-build.log" 2>&1; then
  echo "ОШИБКА: бот не собрался. Лог: Logs/check/map-build.log"; exit 1
fi
if ! bot --self-check --data "$DATA" > "$LOGS/map-data.log" 2>&1; then
  echo "ОШИБКА: клиент не читает папку:"; grep -E "ОШИБКА" "$LOGS/map-data.log"; exit 1
fi
grep -E "^Карта Felucca|^Tiledata:|^Дома и корабли:" "$LOGS/map-data.log" | sed 's/^/  /'
[ -f "$SERVER_DIR/ModernUO.dll" ] || { echo "ОШИБКА: нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
grep -aq "DumpMapData" "$SERVER_DIR/Assemblies/UOContent.dll" 2>/dev/null \
  || { echo "ОШИБКА: в собранном сервере нет команды [DumpMapData. Соберите заново: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
if port_open; then
  echo "ОШИБКА: сервер уже запущен (порт 2593 занят). Остановите его (Ctrl+C в его окне) и запустите проверку снова."; exit 1
fi
SAVES_BEFORE="$(saves_print)"
mkdir -p "$COPY"
rsync -a --exclude Saves --exclude Logs --exclude Backups --exclude Archives --exclude temp "$SERVER_DIR/" "$COPY/"
python3 - "$COPY/Configuration/modernuo.json" "$DATA" <<'PY'
import json, sys
path, data = sys.argv[1], sys.argv[2]
config = json.load(open(path))
config["dataDirectories"] = [data]
config.setdefault("settings", {})["clientData.clientVersion"] = "7.0.95.0"
json.dump(config, open(path, "w"), indent=2)
PY
ok "копия сервера с пустым миром читает файлы из папки: $DATA"

OWNER="mapcheck$(python3 -c 'import secrets; print(secrets.token_hex(3))')"
PASSWORD="check-$(python3 -c 'import secrets; print(secrets.token_hex(6))')"
printf '{"login":"%s","password":"%s","character":"Mapcheck"}\n' "$OWNER" "$PASSWORD" > "$TMP/gm.json"
chmod 600 "$TMP/gm.json"

echo "2/4 Запуск копии сервера (до 90 с)…"
: > "$LOGS/map-server.log"
(cd "$COPY" && exec python3 "$ROOT/tools/check/pty_server.py" --log "$LOGS/map-server.log" \
  --answer "owner account now=y" --answer "Input Username=$OWNER" --answer "Input Password=$PASSWORD" -- dotnet ModernUO.dll) &
SERVER_PID=$!
for _ in $(seq 1 90); do grep -aq "Listening: " "$LOGS/map-server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
if ! grep -aq "Listening: " "$LOGS/map-server.log"; then
  echo "ОШИБКА: копия сервера не запустилась. Лог: Logs/check/map-server.log"; tail -n 15 "$LOGS/map-server.log"; exit 1
fi
ok "сервер запущен"

echo "3/4 Бот-GM сверяет файлы данных с сервером (клеток $CELLS, seed $SEED)…"
mapdump "$DATA" "$LOGS/map-gm.log"
CODE=$?
grep -E "Клетки|Tiledata: сверено|Дома и корабли: сверено" "$LOGS/map-gm.log" | sed 's/^.*ИНФО /  /'
if [ "$CODE" -eq 0 ] && grep -q "Сверка карты с сервером: совпадает" "$LOGS/map-gm.log"; then
  ok "клетки, tiledata, дома и корабли совпадают с сервером"
else
  fail "сверка с сервером: код $CODE. Лог: Logs/check/map-gm.log"
  grep -E "Расхождение|ещё расхождений|ОШИБКА" "$LOGS/map-gm.log" | head -n 12
fi

if [ "$SELF_TEST" -eq 1 ]; then
  echo "    Самопроверка: у клиента испорченная копия карты…"
  mkdir -p "$TMP/broken"
  find "$DATA" -maxdepth 1 -type f \( -iname 'map0*' -o -iname 'staidx0.mul' -o -iname 'statics0.mul' -o -iname 'tiledata.mul' -o -iname 'multi*' \) \
    -exec cp {} "$TMP/broken/" \;
  python3 tools/check/corrupt_map.py "$TMP/broken" > /dev/null
  mapdump "$TMP/broken" "$LOGS/map-broken.log"
  if grep -q "Расхождение: клетка (" "$LOGS/map-broken.log"; then
    ok "испорченная карта поймана: $(grep -m 1 -o 'Расхождение: клетка ([0-9]*, [0-9]*)[^—]*' "$LOGS/map-broken.log")"
  else
    fail "испорченная карта не поймана. Лог: Logs/check/map-broken.log"
  fi
fi

echo "4/4 Остановка копии и ваши сохранения…"
stop_server
if [ "$(saves_print)" = "$SAVES_BEFORE" ]; then ok "ваши сохранения не тронуты"; else fail "папка Saves вашего сервера изменилась во время проверки"; fi
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
