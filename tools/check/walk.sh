#!/usr/bin/env bash
# Живая ходьба бота по настоящему серверу (PLAN Стадия 3, задачи 5–7 и критерии «Ходьба и бег», «Темп»).
# Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/walk.sh [<папка с файлами данных>]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic.
# Ходит обычный игрок (у персонала сервер темп не проверяет — MovementThrottle), флаг speedhack_detection включён.
# Бот-GM подходит к игроку, кладёт ему в рюкзак эфирного коня ([AddToPack EtherealHorse) и переносит его на ровное поле
# к западу от Британии (1130, 1577, 0). Игрок бежит на восток 40 шагов, идёт шагом на запад 25, садится на коня и бежит
# верхом на восток 50. Ожидается: ни одного отказа сервера; бег пешком — 5 шагов в секунду, верхом — 10 (±5%); паузы
# шага пешком — как у ClassicUO в эталоне golden/02-walk-20.tsv (±5%); в журнале сервера нет подозрений на спидхак.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/walk-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=walk
. tools/check/server-copy.sh
DATA="$(default_data "${1:-}")"
FIELD_X=1130
FIELD_Y=1577
FIELD_Z=0

# Строка «Ходьба …» из журнала бота: отказов 0 и темп в пределах ±5% от ожидаемого (шагов в секунду × 100).
check_walk() {
  local label="$1" pattern="$2" expected="$3" line rate rejects
  line="$(grep -o "Ходьба $pattern: .*" "$LOGS/walk-player.log" | head -n 1)"
  if [ -z "$line" ]; then fail "$label: бот не написал итог ходьбы. Лог: Logs/check/walk-player.log"; return; fi
  rejects="$(echo "$line" | sed -n 's/.*отказов сервера \([0-9]*\).*/\1/p')"
  rate="$(echo "$line" | sed -n 's/.*(\([0-9]*\),\([0-9][0-9]\) шага в секунду).*/\1\2/p')"
  if [ "${rejects:-1}" -eq 0 ] && [ -n "$rate" ] && [ $((rate * 100)) -ge $((expected * 95)) ] && [ $((rate * 100)) -le $((expected * 105)) ]; then
    ok "$label: ${line#Ходьба }"
  else
    fail "$label: нужно 0 отказов и $((expected / 100)) шагов в секунду ±5%. ${line#Ходьба }"
  fi
}

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/walk.sh <папка>"; exit 1; }
echo "1/5 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server
copy_make "$DATA"
grep -A6 '"speedhack_detection"' "$COPY/Configuration/FeatureFlags/flags.json" | grep -q '"Enabled": true' \
  || { echo "ОШИБКА: на сервере выключен флаг speedhack_detection — темп не проверяется (TARGET.md §5)."; exit 1; }
ok "копия сервера с пустым миром, файлы данных: $DATA; проверка темпа (speedhack_detection) включена"
copy_accounts walk Walkgm Walkplayer

echo "2/5 Запуск копии сервера (до 90 с)…"
copy_start
ok "сервер запущен, временный владелец создан"

echo "3/5 Игрок входит в первый раз…"
bot --config "$TMP/player.json" --create-character --no-packet-log --idle 1 > "$LOGS/walk-first.log" 2>&1
START="$(grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/walk-first.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g')"
[ -n "$START" ] || { fail "игрок не вошёл в мир. Лог: Logs/check/walk-first.log"; stop_server; echo "ОШИБКА: см. сообщения выше."; exit 1; }
ok "игрок создан и стоит в ($START)"

echo "4/5 GM переносит игрока на поле, игрок бежит, идёт шагом и бежит верхом (около 30 с)…"
bot --config "$TMP/player.json" --data "$DATA" \
  --action seenmob:0x0190 --action "at:$FIELD_X,$FIELD_Y" --action use:backpack \
  --action walk:e,40,run --action walk:w,25,walk \
  --action use:0x20DD --action mounted --action walk:e,50,run --idle 1 > "$LOGS/walk-player.log" 2>&1 &
PLAYER_PID=$!
sleep 1
bot --config "$TMP/gm.json" --create-character --no-packet-log \
  --action "say:[go ${START//,/ }" --action "at:${START%,*}" --action seenmob:0x0190 \
  --action "say:[AddToPack EtherealHorse" --action target:mob:0x0190 \
  --action "say:[set X $FIELD_X Y $FIELD_Y Z $FIELD_Z" --action target:mob:0x0190 --idle 1 > "$LOGS/walk-gm.log" 2>&1
GM_CODE=$?
wait "$PLAYER_PID"
CODE=$?
[ "$GM_CODE" -eq 0 ] || fail "бот-GM не перенёс игрока: код $GM_CODE. Лог: Logs/check/walk-gm.log"
if [ "$CODE" -eq 0 ]; then
  ok "игрок прошёл все действия"
else
  fail "игрок: код $CODE. Лог: Logs/check/walk-player.log"
  grep -E "ОШИБКА|не оказался|не сел|подтвердил|нужны" "$LOGS/walk-player.log" | head -n 5
fi
check_walk "бег пешком" "e, бег пешком" 500
check_walk "шаг пешком" "w, шаг пешком" 250
check_walk "бег верхом" "e, бег верхом" 1000
PACKETS="$(grep -o 'Лог пакетов: .*' "$LOGS/walk-player.log" | head -n 1 | sed 's/Лог пакетов: //')"
if [ -n "$PACKETS" ] && [ -f "$PACKETS" ] && TEMPO="$(python3 "$ROOT/tools/check/walk_tempo.py" "$PACKETS" "$ROOT/golden/02-walk-20.tsv")"; then
  ok "$TEMPO"
else
  fail "паузы шага пешком расходятся с эталоном ClassicUO больше чем на 5%${PACKETS:+ (лог пакетов: $PACKETS)}"
  [ -n "${TEMPO:-}" ] && echo "  $TEMPO"
fi
if grep -aqE "Speed hack|Movement queue overflow" "$LOGS/walk-server.log"; then
  fail "сервер отметил подозрение на спидхак или переполнение очереди шагов:"
  grep -aE "Speed hack|Movement queue overflow" "$LOGS/walk-server.log" | head -n 3
else
  ok "сервер не отметил подозрений на спидхак (журнал сервера: Logs/check/walk-server.log)"
fi

echo "5/5 Остановка копии сервера и проверка сохранности данных…"
stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
