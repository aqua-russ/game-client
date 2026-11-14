#!/usr/bin/env bash
# Проверки Стадии 4 на настоящем сервере (PLAN Стадия 4: 4.4 состояние мобов, 4.6 прицел, 4.7 стойка и атака,
# 4.8 журнал боя, 4.9 смерть и воскрешение).
# Печатает «ОК» или «ОШИБКА».
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
# 4.7: бот-GM переходит к игроку ([go) и ставит крысу, которая не ходит ([add Rat set CantWalk true …); бот-игрок
# включает боевую стойку, атакует крысу и ждёт её смерти: противник по серверу — эта крыса, её здоровье падает, остаётся
# труп, крыса тоже бьёт игрока.
# 4.8: журнал боя бота из того же боя: свои замахи (сервер шлёт замах только атакующему), анимации бота и крысы; события
# журнала совпадают по порядку с пакетами сервера в логе пакетов бота (tools/check/combat_order.py).
# 4.9 (сценарий death-res): бот-игрок ждёт своей смерти; бот-GM даёт ему волосы и бороду ([set), убивает ([kill),
# видит анимацию смерти и труп с вещами по слоям, воскрешает ([res). Игрок видит статус смерти, тело призрака, свой труп
# с волосами и бородой по слоям, после воскрешения — живое тело и здоровье; ответ на статус смерти он не шлёт.
# Сценарий combat-basic — раздел 4.7 (бой с крысой до её смерти).
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

echo "1/8 Сборка бота и временная копия сервера…"
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

echo "2/8 Запуск копии сервера (до 90 с)…"
: > "$LOGS/combat-server.log"
(cd "$COPY" && exec python3 "$ROOT/tools/check/pty_server.py" --log "$LOGS/combat-server.log" \
  --answer "owner account now=y" --answer "Input Username=$OWNER" --answer "Input Password=$PASSWORD" -- dotnet ModernUO.dll) &
SERVER_PID=$!
for _ in $(seq 1 90); do grep -aq "Listening: " "$LOGS/combat-server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
if ! grep -aq "Listening: " "$LOGS/combat-server.log"; then
  echo "ОШИБКА: копия сервера не запустилась. Лог: Logs/check/combat-server.log"; tail -n 15 "$LOGS/combat-server.log"; exit 1
fi
ok "сервер запущен, временный владелец создан"

echo "3/8 4.4 Состояние мобов: бот-GM меняет себе статы и сверяет состояние с сервером…"
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

echo "4/8 4.6 Прицел: бот-GM бинтуется быстрым бинтом и через курсор…"
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

echo "5/8 4.7 Стойка и атака (сценарий combat-basic): бот-игрок бьёт крысу до смерти…"
START="$(grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/combat-player.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g')"
if [ -z "$START" ]; then
  fail "не знаю, где стоит бот-игрок (нет строки входа в Logs/check/combat-player.log)"
else
  bot --config "$TMP/gm.json" --no-packet-log \
    --action "say:[go ${START//,/ }" --action "at:${START%,*}" \
    --action "say:[add Rat set CantWalk true HitsMaxSeed 10 Hits 10 Dex 200 Stam 200" --action target:here --action seenmob:0x00EE \
    --idle 1 > "$LOGS/combat-fight-gm.log" 2>&1
  CODE=$?
  if [ "$CODE" -ne 0 ]; then
    fail "бот-GM не поставил крысу рядом с игроком ($START): код $CODE. Лог: Logs/check/combat-fight-gm.log"
  fi
  bot --config "$TMP/player.json" --action seenmob:0x00EE --action fight:0x00EE --idle 1 \
    > "$LOGS/combat-fight.log" 2>&1
  CODE=$?
  RAT="$(grep -o 'Атака 0x[0-9A-F]*' "$LOGS/combat-fight.log" | head -n 1 | sed 's/Атака //')"
  if [ "$CODE" -eq 0 ] && grep -q "Стойка: боевая" "$LOGS/combat-fight.log" && [ -n "$RAT" ] \
    && grep -q "Противник по серверу: $RAT\.$" "$LOGS/combat-fight.log"; then
    ok "сервер включил боевую стойку; противник по серверу — атакованная крыса $RAT"
  else
    fail "стойка или противник: код $CODE. Лог: Logs/check/combat-fight.log"
    grep -E "ОШИБКА|не тот|не включил|не назвал" "$LOGS/combat-fight.log" | head -n 5
  fi
  if grep -Eq "Противник: здоровье [0-9]{1,2}/100" "$LOGS/combat-fight.log" \
    && grep -q "убит: труп" "$LOGS/combat-fight.log"; then
    ok "здоровье крысы падало ($(grep -o 'Противник: здоровье [0-9]*/100' "$LOGS/combat-fight.log" | sed 's/.*здоровье //' | paste -sd ' ' -)), крыса убита: анимация смерти, труп, моб удалён"
  else
    fail "крыса не умерла или её здоровье не приходило. Лог: Logs/check/combat-fight.log"
  fi
  if grep -q "Бот получил урон" "$LOGS/combat-fight.log"; then
    ok "крыса тоже била игрока: $(grep -o 'здоровье опускалось до [0-9/]*' "$LOGS/combat-fight.log" | head -n 1)"
  else
    fail "игрок урона не получил (крыса не попала ни разу). Лог: Logs/check/combat-fight.log"
  fi
fi

echo "6/8 4.8 Журнал боя: события того же боя и их порядок…"
SUMMARY="$(grep -o 'Журнал боя: событий .*' "$LOGS/combat-fight.log" | head -n 1)"
SWINGS="$(echo "$SUMMARY" | sed -n 's/.*замахов бота по противнику \([0-9]*\).*/\1/p')"
MINE="$(echo "$SUMMARY" | sed -n 's/.*анимаций бота \([0-9]*\).*/\1/p')"
THEIRS="$(echo "$SUMMARY" | sed -n 's/.*противника \([0-9]*\), урона.*/\1/p')"
if [ "${SWINGS:-0}" -gt 0 ] && [ "${MINE:-0}" -gt 0 ] && [ "${THEIRS:-0}" -gt 0 ]; then
  ok "${SUMMARY#Журнал боя: }"
else
  fail "в журнале боя нет замахов бота или анимаций бота и крысы. Лог: Logs/check/combat-fight.log"
  [ -n "$SUMMARY" ] && echo "  $SUMMARY"
fi
PACKETS="$(grep -o 'Лог пакетов: .*' "$LOGS/combat-fight.log" | head -n 1 | sed 's/Лог пакетов: //')"
if [ -n "$PACKETS" ] && [ -f "$PACKETS" ] && ORDER="$(python3 "$ROOT/tools/check/combat_order.py" "$LOGS/combat-fight.log" "$PACKETS")"; then
  ok "$ORDER"
else
  fail "порядок журнала боя не совпал с пакетами сервера${PACKETS:+ (лог пакетов: $PACKETS)}"
  [ -n "${ORDER:-}" ] && echo "  $ORDER"
fi

echo "7/8 4.9 Смерть и воскрешение (сценарий death-res): бот-GM убивает бота-игрока и воскрешает…"
if [ -z "$START" ]; then
  fail "не знаю, где стоит бот-игрок"
else
  : > "$LOGS/combat-death.log"
  bot --config "$TMP/player.json" --action dead --action alive --idle 1 > "$LOGS/combat-death.log" 2>&1 &
  PLAYER_BOT=$!
  for _ in $(seq 1 30); do grep -q "Действие: dead" "$LOGS/combat-death.log" 2>/dev/null && break; sleep 0.5; done
  bot --config "$TMP/gm.json" --no-packet-log \
    --action "say:[go ${START//,/ }" --action "at:${START%,*}" --action seenmob:0x0190 \
    --action "say:[set HairItemID 8251 FacialHairItemID 8256" --action target:mob:0x0190 \
    --action "say:[kill" --action target:mob:0x0190 --action deathof:0x0190 \
    --action "say:[res" --action target:mob:0x0192 --idle 1 > "$LOGS/combat-death-gm.log" 2>&1
  GM_CODE=$?
  wait "$PLAYER_BOT"
  CODE=$?
  DEATH="$(grep -o 'Смерть 0x[0-9A-F]*: анимация смерти.*' "$LOGS/combat-death-gm.log" | head -n 1)"
  if [ "$GM_CODE" -eq 0 ] && echo "$DEATH" | grep -q "Hair → .*FacialHair → .*стал призраком"; then
    ok "GM видит смерть игрока: ${DEATH%.}"
  else
    fail "GM не увидел смерть игрока с трупом и вещами по слоям: код $GM_CODE. Лог: Logs/check/combat-death-gm.log"
    grep -E "ОШИБКА|Смерть" "$LOGS/combat-death-gm.log" | head -n 5
  fi
  OWN="$(grep -o 'Смерть: свой труп .*' "$LOGS/combat-death.log" | head -n 1)"
  if grep -q "Смерть: статус смерти (0x2C)" "$LOGS/combat-death.log" && grep -q "Смерть: тело призрака 0x0192" "$LOGS/combat-death.log" \
    && echo "$OWN" | grep -q "Hair → .*(в трупе).*FacialHair → .*(в трупе)"; then
    ok "игрок: статус смерти от сервера, тело призрака 0x0192; ${OWN#Смерть: }"
  else
    fail "игрок не увидел своей смерти (статус, призрак, свой труп с вещами). Лог: Logs/check/combat-death.log"
    grep -E "ОШИБКА|Смерть" "$LOGS/combat-death.log" | head -n 5
  fi
  if [ "$CODE" -eq 0 ] && grep -q "Жив: тело 0x0190, здоровье [1-9]" "$LOGS/combat-death.log"; then
    ok "после [res игрок снова жив: $(grep -o 'Жив: .*' "$LOGS/combat-death.log" | head -n 1 | sed 's/Жив: //; s/\.$//')"
  else
    fail "игрок не ожил после [res: код $CODE. Лог: Logs/check/combat-death.log"
  fi
  PACKETS="$(grep -o 'Лог пакетов: .*' "$LOGS/combat-death.log" | head -n 1 | sed 's/Лог пакетов: //')"
  if [ -n "$PACKETS" ] && [ -f "$PACKETS" ] && grep -q "S>C	0x2C" "$PACKETS" && ! grep -q "C>S	0x2C" "$PACKETS"; then
    ok "на статус смерти (0x2C) клиент не ответил — сервер такой ответ игнорирует"
  else
    fail "в логе пакетов игрока нет статуса смерти или есть ответ на него${PACKETS:+ ($PACKETS)}"
  fi
fi

echo "8/8 Остановка копии и ваши сохранения…"
stop_server
if [ "$(saves_print)" = "$SAVES_BEFORE" ]; then ok "ваши сохранения не тронуты"; else fail "папка Saves вашего сервера изменилась во время проверки"; fi
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
