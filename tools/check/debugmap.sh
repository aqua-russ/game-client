#!/usr/bin/env bash
# Демо-стенд для отладочной карты в Unity (PLAN Стадия 2, п. 13). Печатает логин и пароль для Unity и держит стенд, пока
# вы смотрите карту. Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/debugmap.sh [<папка с файлами данных>] [--no-wait]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic.
# Бот-GM встаёт у «Sweet Dreams Inn» в Британии (стартовая точка нового персонажа города Britain: 1496, 1628), ставит рядом
# яблоко, две неподвижные крысы и дом (GM-команды [add и [ParityPlace), проверяет по настоящим файлам, что карта вокруг
# него собирается (действие бота scene:), и остаётся стоять — он тоже виден в Unity как моб.
# Дальше скрипт ждёт Enter: за это время зайдите в Unity (шаги — в ответе агента или ниже на экране). --no-wait — без
# ожидания (автоматическая проверка без Unity).
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/debugmap-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
LOGS="$ROOT/Logs/check"
SERVER_DIR="${THEVERGE_SERVER_DIR:-$ROOT/../../server/ModernUO/Distribution}"
BOT_DLL="$ROOT/dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll"
DATA=""
WAIT=1
for arg in "$@"; do
  case "$arg" in
    --no-wait) WAIT=0 ;;
    *) DATA="$arg" ;;
  esac
done
[ -z "$DATA" ] && [ -d "$ROOT/../../uo-data/Ultima Online Classic" ] && DATA="$ROOT/../../uo-data/Ultima Online Classic"
TMP="$(mktemp -d)"
COPY="$TMP/server"
SERVER_PID=""
BOT_PID=""
FAILED=0
mkdir -p "$LOGS"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

ok() { echo "  ОК: $1"; }
fail() { echo "  ОШИБКА: $1"; FAILED=1; }
port_open() { python3 -c "import socket,sys; s=socket.socket(); s.settimeout(1); sys.exit(s.connect_ex(('127.0.0.1',2593)))"; }
saves_print() { [ -d "$SERVER_DIR/Saves" ] && (cd "$SERVER_DIR/Saves" && find . -type f -exec ls -ln {} + 2>/dev/null | awk '{print $5, $6, $7, $8, $9}' | sort | cksum) || echo "нет"; }
stop_bot() { [ -n "$BOT_PID" ] && kill "$BOT_PID" 2>/dev/null; [ -n "$BOT_PID" ] && wait "$BOT_PID" 2>/dev/null; BOT_PID=""; }
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
cleanup() { stop_bot; stop_server; rm -rf "$TMP"; }
trap cleanup EXIT

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/debugmap.sh <папка>"; exit 1; }
echo "1/4 Сборка бота и временная копия сервера…"
if ! dotnet build dotnet/TheVerge.Bot -nologo -v:q -tl:off > "$LOGS/debugmap-build.log" 2>&1; then
  echo "ОШИБКА: бот не собрался. Лог: Logs/check/debugmap-build.log"; exit 1
fi
[ -f "$SERVER_DIR/ModernUO.dll" ] || { echo "ОШИБКА: нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
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

OWNER="demo$(python3 -c 'import secrets; print(secrets.token_hex(3))')"
PASSWORD="check-$(python3 -c 'import secrets; print(secrets.token_hex(6))')"
UNITY_LOGIN="verge$(python3 -c 'import secrets; print(secrets.token_hex(2))')"
UNITY_PASSWORD="demo-$(python3 -c 'import secrets; print(secrets.token_hex(4))')"
printf '{"login":"%s","password":"%s","character":"Demogm"}\n' "$OWNER" "$PASSWORD" > "$TMP/gm.json"
chmod 600 "$TMP/gm.json"

echo "2/4 Запуск копии сервера (до 90 с)…"
: > "$LOGS/debugmap-server.log"
(cd "$COPY" && exec python3 "$ROOT/tools/check/pty_server.py" --log "$LOGS/debugmap-server.log" \
  --answer "owner account now=y" --answer "Input Username=$OWNER" --answer "Input Password=$PASSWORD" -- dotnet ModernUO.dll) &
SERVER_PID=$!
for _ in $(seq 1 90); do grep -aq "Listening: " "$LOGS/debugmap-server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
if ! grep -aq "Listening: " "$LOGS/debugmap-server.log"; then
  echo "ОШИБКА: копия сервера не запустилась. Лог: Logs/check/debugmap-server.log"; tail -n 15 "$LOGS/debugmap-server.log"; exit 1
fi
ok "сервер запущен, временный владелец создан"

echo "3/4 Бот-GM расставляет объекты у «Sweet Dreams Inn» и проверяет карту (около 30 с)…"
ACTIONS=()
act() { ACTIONS+=(--action "$1"); }
go() { act "say:[go $1 $2"; act "at:$1,$2"; }
go 1496 1628
go 1493 1628; act "say:[add Apple"; act target:here; act seen:0x09D0
go 1499 1628; act "say:[add Rat set CantWalk true"; act target:here; act seenmob:0x00EE
go 1496 1624; act "say:[add Rat set CantWalk true"; act target:here; act seenmob:0x00EE
go 1500 1642; act "say:[ParityPlace house"; act seenmulti:
go 1496 1628; act scene:
dotnet "$BOT_DLL" --config "$TMP/gm.json" --create-character --no-packet-log --data "$DATA" "${ACTIONS[@]}" --idle 3600 > "$LOGS/debugmap-gm.log" 2>&1 &
BOT_PID=$!
for _ in $(seq 1 120); do grep -aq "Сцена вокруг" "$LOGS/debugmap-gm.log" 2>/dev/null && break; kill -0 "$BOT_PID" 2>/dev/null || break; sleep 1; done
SCENE="$(grep -a "Сцена вокруг" "$LOGS/debugmap-gm.log" | head -n 1)"
if [ -z "$SCENE" ]; then
  fail "бот не дошёл до сборки карты. Лог: Logs/check/debugmap-gm.log"
  grep -aE "не может|не появился|не оказался|ОШИБКА|не записал" "$LOGS/debugmap-gm.log" | head -n 8
else
  echo "  $SCENE"
  # Земля — весь квадрат обзора 37×37 (все клетки внутри карты); часть дома и стены в Британии должны быть.
  echo "$SCENE" | grep -q "земля 1369" && ok "земля: 37×37 клеток" || fail "земли не 1369 клеток"
  echo "$SCENE" | grep -qE "части домов [1-9]" && ok "части дома попали на карту" || fail "частей домов нет"
  echo "$SCENE" | grep -qE "стена [1-9]" && ok "стены (здания Британии) есть" || fail "стен нет"
  echo "$SCENE" | grep -qE "поверхность [1-9]" && ok "проходимая поверхность есть" || fail "поверхности нет"
fi

echo "4/4 Стенд готов"
if [ "$FAILED" -eq 0 ] && [ "$WAIT" -eq 1 ]; then
  cat <<TXT

  Сервер (временная копия): 127.0.0.1:2593
  Логин: $UNITY_LOGIN      Пароль: $UNITY_PASSWORD   (аккаунт создастся при первом входе)
  В Unity: создайте персонажа, город — Britain (стартовая точка 1496, 1628).
  Рядом стоят: яблоко, две крысы, дом на юге и бот-GM «Demogm».

TXT
  read -r -p "  Посмотрели? Нажмите Enter — стенд остановится. " _
fi
stop_bot
stop_server
if [ "$(saves_print)" = "$SAVES_BEFORE" ]; then ok "ваши Saves не изменились"; else fail "ваши Saves изменились"; fi
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
