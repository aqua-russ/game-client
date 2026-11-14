#!/usr/bin/env bash
# Сверка физики движения клиента MovementCheck против сервера ModernUO на 100 000 точках (PLAN Стадия 3, п. 3).
# Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/movement.sh [<папка с файлами данных>]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic.
# Бот-GM в Британии (1495, 1629, 10) ставит дом ([ParityPlace house), деревянный сундук (WoodenChest) и
# неподвижную крысу (Rat), после чего запрашивает [DumpMove 100000 1337 и сверяет все 100 000 кейсов в четырёх режимах
# (игрок, игрок с полной выносливостью — проходит сквозь моба, GM, призрак) против MovementCheck.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/movement-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
LOGS="$ROOT/Logs/check"
SERVER_DIR="${THEVERGE_SERVER_DIR:-$ROOT/../../server/ModernUO/Distribution}"
BOT_DLL="$ROOT/dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll"
DATA="${1:-}"
[ -z "$DATA" ] && [ -d "$ROOT/../../uo-data/Ultima Online Classic" ] && DATA="$ROOT/../../uo-data/Ultima Online Classic"
TMP="$(mktemp -d)"
COPY="$TMP/server"
PARITY="$COPY/Logs/parity"
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

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/movement.sh <папка>"; exit 1; }
echo "1/4 Сборка бота и временная копия сервера…"
if ! dotnet build dotnet/TheVerge.Bot -nologo -v:q -tl:off > "$LOGS/movement-build.log" 2>&1; then
  echo "ОШИБКА: бот не собрался. Лог: Logs/check/movement-build.log"; exit 1
fi
[ -f "$SERVER_DIR/ModernUO.dll" ] || { echo "ОШИБКА: нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
grep -aq "DumpMove" "$SERVER_DIR/Assemblies/UOContent.dll" 2>/dev/null \
  || { echo "ОШИБКА: в собранном сервере нет команды [DumpMove. Соберите заново: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
grep -aq "ParityPlace" "$SERVER_DIR/Assemblies/UOContent.dll" 2>/dev/null \
  || { echo "ОШИБКА: в собранном сервере нет команды [ParityPlace. Соберите заново: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
if port_open; then
  echo "ОШИБКА: сервер уже запущен (порт 2593 занят). Остановите его (Ctrl+C в его окне) и запустите проверку снова."; exit 1
fi
SAVES_BEFORE="$(saves_print)"
mkdir -p "$COPY"
rsync -a --exclude Saves --exclude Logs --exclude Backups --exclude Archives --exclude temp "$SERVER_DIR/" "$COPY/"
python3 - "$COPY/Configuration/modernuo.json" "$DATA" <<'PY'
import json, os, sys
path, data = sys.argv[1], sys.argv[2]
config = json.load(open(path))
config["dataDirectories"] = [os.path.abspath(data)]
config.setdefault("settings", {})["clientData.clientVersion"] = "7.0.95.0"
json.dump(config, open(path, "w"), indent=2)
PY
ok "копия сервера с пустым миром, файлы данных: $DATA"

OWNER="move$(python3 -c 'import secrets; print(secrets.token_hex(3))')"
PASSWORD="check-$(python3 -c 'import secrets; print(secrets.token_hex(6))')"
printf '{"login":"%s","password":"%s","character":"Movegm"}\n' "$OWNER" "$PASSWORD" > "$TMP/gm.json"
chmod 600 "$TMP/gm.json"

echo "2/4 Запуск копии сервера (до 90 с)…"
: > "$LOGS/movement-server.log"
(cd "$COPY" && exec python3 "$ROOT/tools/check/pty_server.py" --log "$LOGS/movement-server.log" \
  --answer "owner account now=y" --answer "Input Username=$OWNER" --answer "Input Password=$PASSWORD" -- dotnet ModernUO.dll) &
SERVER_PID=$!
for _ in $(seq 1 90); do grep -aq "Listening: " "$LOGS/movement-server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
if ! grep -aq "Listening: " "$LOGS/movement-server.log"; then
  echo "ОШИБКА: копия сервера не запустилась. Лог: Logs/check/movement-server.log"; tail -n 15 "$LOGS/movement-server.log"; exit 1
fi
ok "сервер запущен, временный владелец создан"

echo "3/4 Сверка MovementCheck против [DumpMove на 100 000 точках (около 30 с)…"
ACTIONS=()
act() { ACTIONS+=(--action "$1"); }
act "say:[go 1495 1629 10"; act "at:1495,1629"
act "say:[ParityPlace house"; act seenmulti:
act "say:[add WoodenChest"; act target:here; act seen:0x0E43
act "say:[add Rat set CantWalk true"; act target:here; act seenmob:0x00EE
act "movedump:100000,1337,$PARITY"

dotnet "$BOT_DLL" --config "$TMP/gm.json" --create-character --no-packet-log --data "$DATA" "${ACTIONS[@]}" --idle 1 > "$LOGS/movement-gm.log" 2>&1
CODE=$?
if [ "$CODE" -eq 0 ] && grep -q "Сверка физики движения с сервером: совпадает" "$LOGS/movement-gm.log" \
   && grep -q "сверено проверок 100000" "$LOGS/movement-gm.log"; then
  ok "физика движения совпала на 100000 проверках (режимы player, gm, ghost, shove)"
else
  fail "сверка MovementCheck с сервером: код $CODE. Лог: Logs/check/movement-gm.log"
  grep -E "Расхождение|не может|не появился|не оказался|ОШИБКА|не записал|проблема:" "$LOGS/movement-gm.log" | head -n 15
fi

echo "4/4 Остановка копии сервера и проверка сохранности данных…"
stop_server
if [ "$(saves_print)" = "$SAVES_BEFORE" ]; then ok "ваши Saves не изменились"; else fail "ваши Saves изменились"; fi
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
