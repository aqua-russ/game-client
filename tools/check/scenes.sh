#!/usr/bin/env bash
# Сценарии движения бота по настоящему серверу (PLAN Стадия 3, задача 10): маршрут, дверь, лестница.
# Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/scenes.sh [<папка с файлами данных>]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic.
# Ходит обычный игрок (у персонала сервер темп не проверяет), флаг speedhack_detection включён.
#   Маршрут (walk-route): бот-GM ставит игрока у «Sweet Dreams Inn» (1496, 1628 — стартовая точка города Britain), и тот идёт по улицам до дома на северо-западе — 10 отрезков по
#     прямой и диагонали, бегом и шагом, с подъёмами (Z от 10 до 30). Ни одного отказа сервера; в конце бот спрашивает
#     сервер, где персонаж (ресинхронизация), — клетка и высота совпадают с предсказанием клиента.
#   Дверь (door-test): бот-GM заранее ставит деревянную дверь поперёк улицы (1470, 1600). Закрытая — шаг в неё не уходит;
#     игрок открывает её, проходит насквозь, закрывает за собой — шаг назад снова не уходит; отказов нет.
#   Лестница (stairs-test): в доме (1469, 1557) деревянная лестница с карты на второй этаж: наверху (1474, 1557) высота 50,
#     внизу снова 30, обе — как у сервера (ресинхронизация); отказов нет.
# В журнале сервера нет подозрений на спидхак. Около 3 минут.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/scenes-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=scenes
. tools/check/server-copy.sh
DATA="$(default_data "${1:-}")"
START_X=1496
START_Y=1628
START_Z=10
DOOR="1470,1600,20"
DOOR_CLOSED=0x06A5   # DarkWoodDoor WestCW: закрытая 0x6A5, открытая 0x6A6 (Projects/UOContent/Items/Construction/Doors/Doors.cs)
DOOR_OPEN=0x06A6

PLAYER=(
  --action seenmob:0x0190 --action "at:$START_X,$START_Y,$START_Z"
  # Маршрут до двери
  --action goto:1490,1626,run --action goto:1480,1606,walk --action goto:1474,1600,run --action goto:1471,1600,walk
  # Дверь
  --action blocked:w --action "use:$DOOR_CLOSED" --action "near:$DOOR_OPEN" --action goto:1469,1600,walk
  --action "use:$DOOR_OPEN" --action "near:$DOOR_CLOSED" --action blocked:e
  # Маршрут до дома с лестницей
  --action goto:1466,1600,run --action goto:1460,1568,run --action goto:1456,1558,walk --action goto:1464,1555,run
  --action goto:1465,1557,walk --action goto:1469,1557,walk --action resync
  # Лестница
  --action goto:1474,1557,walk --action at:1474,1557,50 --action resync
  --action goto:1469,1557,run --action at:1469,1557,30 --action resync
)

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/scenes.sh <папка>"; exit 1; }
echo "1/5 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server
copy_make "$DATA"
grep -A6 '"speedhack_detection"' "$COPY/Configuration/FeatureFlags/flags.json" | grep -q '"Enabled": true' \
  || { echo "ОШИБКА: на сервере выключен флаг speedhack_detection — темп не проверяется (TARGET.md §5)."; exit 1; }
ok "копия сервера с пустым миром, файлы данных: $DATA"
copy_accounts scenes Scenegm Sceneplayer

echo "2/5 Запуск копии сервера (до 90 с)…"
copy_start
ok "сервер запущен, временный владелец создан"

echo "3/5 Игрок входит в первый раз…"
bot --config "$TMP/player.json" --create-character --no-packet-log --idle 1 > "$LOGS/scenes-first.log" 2>&1
START="$(grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/scenes-first.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g')"
[ -n "$START" ] || { fail "игрок не вошёл в мир. Лог: Logs/check/scenes-first.log"; stop_server; echo "ОШИБКА: см. сообщения выше."; exit 1; }
ok "игрок создан и стоит в ($START)"

echo "4/5 GM ставит дверь и переносит игрока; игрок идёт маршрутом, через дверь и по лестнице (около 1,5 минуты)…"
bot --config "$TMP/gm.json" --create-character --no-packet-log \
  --action "say:[go 1471 1602 20" --action at:1471,1602 --action "say:[add DarkWoodDoor WestCW" \
  --action "target:$DOOR" --action "near:$DOOR_CLOSED" > "$LOGS/scenes-door.log" 2>&1 \
  && ok "GM поставил дверь в ($DOOR)" || fail "бот-GM не поставил дверь. Лог: Logs/check/scenes-door.log"
bot --config "$TMP/player.json" --data "$DATA" "${PLAYER[@]}" --idle 1 > "$LOGS/scenes-player.log" 2>&1 &
PLAYER_PID=$!
sleep 1
bot --config "$TMP/gm.json" --no-packet-log \
  --action "say:[go ${START//,/ }" --action "at:${START%,*}" --action seenmob:0x0190 \
  --action "say:[set X $START_X Y $START_Y Z $START_Z" --action target:mob:0x0190 --idle 1 > "$LOGS/scenes-gm.log" 2>&1
GM_CODE=$?
wait "$PLAYER_PID"
CODE=$?
[ "$GM_CODE" -eq 0 ] && ok "GM перенёс игрока в ($START_X, $START_Y, $START_Z)" || fail "бот-GM не перенёс игрока: код $GM_CODE. Лог: Logs/check/scenes-gm.log"
if [ "$CODE" -eq 0 ]; then
  ok "игрок прошёл все действия"
else
  fail "игрок: код $CODE. Лог: Logs/check/scenes-player.log"
  grep -E "ОШИБКА|не дошёл|путь закрыт|шаг ушёл|сервер ставит|не оказался|не появился|нужны" "$LOGS/scenes-player.log" | head -n 5
fi
ROUTES="$(grep -c 'Маршрут до .*отказов сервера 0, ресинхронизаций 0\.' "$LOGS/scenes-player.log")"
ROUTES_ALL="$(grep -c 'Маршрут до ' "$LOGS/scenes-player.log")"
ROUTES_WANT="$(printf '%s\n' "${PLAYER[@]}" | grep -c '^goto:')"
if [ "$ROUTES" -eq "$ROUTES_WANT" ] && [ "$ROUTES_ALL" -eq "$ROUTES_WANT" ]; then
  ok "маршрут: все $ROUTES_WANT отрезков без отказов сервера и ресинхронизаций"
else
  fail "маршрут: без отказов $ROUTES из $ROUTES_WANT отрезков (пройдено $ROUTES_ALL)"
  grep 'Маршрут до ' "$LOGS/scenes-player.log" | grep -v 'отказов сервера 0, ресинхронизаций 0\.' | head -n 3
fi
BLOCKED="$(grep -c 'Преграда .*: шаг не ушёл\.' "$LOGS/scenes-player.log")"
[ "$BLOCKED" -eq 2 ] && ok "дверь: закрытая не пускает (до и после), открытая пропускает" || fail "дверь: преграда сработала $BLOCKED раз из 2"
SYNCS="$(grep -c 'Сверка с сервером: .*как предсказано\.' "$LOGS/scenes-player.log")"
if [ "$SYNCS" -eq 3 ]; then
  ok "сверка с сервером: после маршрута, наверху и внизу лестницы клетка и высота как у сервера"
  grep -o 'Персонаж в клетке (1474, 1557, [0-9-]*)' "$LOGS/scenes-player.log" | head -n 1 | sed 's/^/  /'
else
  fail "сверка с сервером совпала $SYNCS раз из 3"
fi
if grep -aqE "Speed hack|Movement queue overflow" "$LOGS/scenes-server.log"; then
  fail "сервер отметил подозрение на спидхак или переполнение очереди шагов:"
  grep -aE "Speed hack|Movement queue overflow" "$LOGS/scenes-server.log" | head -n 3
else
  ok "сервер не отметил подозрений на спидхак (журнал сервера: Logs/check/scenes-server.log)"
fi

echo "5/5 Остановка копии сервера и проверка сохранности данных…"
stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
