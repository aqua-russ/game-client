#!/usr/bin/env bash
# Проверки Стадии 4 на настоящем сервере (PLAN Стадия 4: 4.4 состояние мобов, 4.6 прицел). Печатает «ОК» или «ОШИБКА».
#
#   tools/check/combat.sh [<папка с файлами данных>]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic, если она есть,
# иначе — та, что в настройках вашего сервера. В пустом мире сервер сам предлагает создать владельца — скрипт отвечает
# за вас (случайный пароль, только на время проверки).
# 4.4: бот-GM ставит крысу ([add Rat), меняет себе силу, здоровье и яд ([set) и сверяет состояние своё и крысы с
# сервером ([DumpMobile); обычный игрок команду выполнить не может.
# 4.6: бот-GM кладёт под ноги 5 бинтов ([add Bandage 5), ранит себя ([set hits 30), бинтуется быстрым бинтом без курсора
# (бинтов становится 4) и обычным — двойной клик и ответ на курсор «польза» (бинтов 3).
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Подробности — Logs/check/combat-*.log, лог сервера — Logs/check/combat-server.log.
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
bot() { dotnet "$BOT_DLL" "$@"; }

echo "1/5 Сборка бота и временная копия сервера…"
if ! dotnet build dotnet/TheVerge.Bot -nologo -v:q -tl:off > "$LOGS/combat-build.log" 2>&1; then
  echo "ОШИБКА: бот не собрался. Лог: Logs/check/combat-build.log"; exit 1
fi
[ -f "$SERVER_DIR/ModernUO.dll" ] || { echo "ОШИБКА: нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
grep -aq "DumpMobile" "$SERVER_DIR/Assemblies/UOContent.dll" 2>/dev/null \
  || { echo "ОШИБКА: в собранном сервере нет команды [DumpMobile. Соберите заново: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
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
if data:
    config["dataDirectories"] = [os.path.abspath(data)]
config.setdefault("settings", {})["clientData.clientVersion"] = "7.0.95.0"
json.dump(config, open(path, "w"), indent=2)
PY
ok "копия сервера с пустым миром${DATA:+, файлы данных: $DATA}"

OWNER="combat$(python3 -c 'import secrets; print(secrets.token_hex(3))')"
PASSWORD="check-$(python3 -c 'import secrets; print(secrets.token_hex(6))')"
printf '{"login":"%s","password":"%s","character":"Combatgm"}\n' "$OWNER" "$PASSWORD" > "$TMP/gm.json"
printf '{"login":"%sp","password":"%s","character":"Combatplayer"}\n' "$OWNER" "$PASSWORD" > "$TMP/player.json"
chmod 600 "$TMP/gm.json" "$TMP/player.json"

echo "2/5 Запуск копии сервера (до 90 с)…"
: > "$LOGS/combat-server.log"
(cd "$COPY" && exec python3 "$ROOT/tools/check/pty_server.py" --log "$LOGS/combat-server.log" \
  --answer "owner account now=y" --answer "Input Username=$OWNER" --answer "Input Password=$PASSWORD" -- dotnet ModernUO.dll) &
SERVER_PID=$!
for _ in $(seq 1 90); do grep -aq "Listening: " "$LOGS/combat-server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
if ! grep -aq "Listening: " "$LOGS/combat-server.log"; then
  echo "ОШИБКА: копия сервера не запустилась. Лог: Logs/check/combat-server.log"; tail -n 15 "$LOGS/combat-server.log"; exit 1
fi
ok "сервер запущен, временный владелец создан"

echo "3/5 4.4 Состояние мобов: бот-GM меняет себе статы и сверяет состояние с сервером…"
bot --config "$TMP/gm.json" --create-character --no-packet-log \
  --action "say:[add Rat" --action target:here --action seenmob:0x00EE \
  --action "say:[set str 60" --action target:self \
  --action "say:[set hits 30" --action target:self \
  --action "say:[set poison lesser" --action target:self \
  --action "statusdump:$PARITY" --idle 1 > "$LOGS/combat-status.log" 2>&1
CODE=$?
if [ "$CODE" -eq 0 ] && grep -q "Сверка состояния с сервером: совпадает" "$LOGS/combat-status.log"; then
  ok "$(grep -o 'сверено мобов [0-9]*' "$LOGS/combat-status.log" | head -n 1): свой персонаж и крыса совпадают с сервером"
else
  fail "сверка состояния: код $CODE. Лог: Logs/check/combat-status.log"
  grep -E "Расхождение|ОШИБКА|не появился|не записал" "$LOGS/combat-status.log" | head -n 10
fi
if grep -q "Статус: .*здоровье 30/.*сила 60" "$LOGS/combat-status.log"; then
  ok "$(grep -o 'Статус: .*' "$LOGS/combat-status.log" | head -n 1)"
else
  fail "статус бота не обновился после [set (сила 60, здоровье 30). Лог: Logs/check/combat-status.log"
fi
bot --config "$TMP/player.json" --create-character --no-packet-log --action "statusdump:$PARITY" > "$LOGS/combat-player.log" 2>&1
if grep -q "сервер не записал дамп состояния" "$LOGS/combat-player.log"; then ok "обычному игроку [DumpMobile недоступна"; else fail "у игрока дамп записан или бот упал иначе. Лог: Logs/check/combat-player.log"; fi

echo "4/5 4.6 Прицел: бот-GM бинтуется быстрым бинтом и через курсор…"
bot --config "$TMP/gm.json" --no-packet-log \
  --action "say:[add Bandage 5" --action target:here --action seen:0x0E21,5 \
  --action "say:[set hits 30" --action target:self \
  --action bandage:self --action seen:0x0E21,4 \
  --action use:0x0E21 --action target:self --action seen:0x0E21,3 --idle 1 > "$LOGS/combat-target.log" 2>&1
CODE=$?
if [ "$CODE" -eq 0 ] && grep -q "Быстрый бинт" "$LOGS/combat-target.log" && grep -q "количество 4" "$LOGS/combat-target.log"; then
  ok "быстрый бинт без курсора: лечение началось, бинтов 5 → 4"
else
  fail "быстрый бинт: код $CODE. Лог: Logs/check/combat-target.log"
  grep -E "ОШИБКА|не появился|нет рядом" "$LOGS/combat-target.log" | head -n 5
fi
if [ "$CODE" -eq 0 ] && grep -q "self» на курсор [0-9]* (польза)" "$LOGS/combat-target.log" && grep -q "количество 3" "$LOGS/combat-target.log"; then
  ok "обычный бинт: двойной клик, курсор сервера «польза», ответ «себя» — бинтов 4 → 3"
else
  fail "бинт через курсор не сработал. Лог: Logs/check/combat-target.log"
fi

echo "5/5 Остановка копии и ваши сохранения…"
stop_server
if [ "$(saves_print)" = "$SAVES_BEFORE" ]; then ok "ваши сохранения не тронуты"; else fail "папка Saves вашего сервера изменилась во время проверки"; fi
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
