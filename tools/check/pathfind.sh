#!/usr/bin/env bash
# Живая проверка поиска пути A* (PLAN Стадия 3, п. 11) на временной копии сервера с настоящими файлами данных.
# Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/pathfind.sh [<папка с файлами данных>]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Ходит обычный игрок (у персонала сервер темп не проверяет), speedhack_detection включён.
#   1. Британия: обход стены/забора у таверны «Sweet Dreams Inn» (1496,1628 -> 1493,1637, склон Z 10->24->20), затем
#      длинный городской маршрут одним действием path: до (1469,1557,30) мимо неподвижной крысы от GM и подъём/спуск по
#      лестнице дома на Z=50 и обратно на Z=30.
#   2. Дом и дверь (1130,1577): GM ставит дом [ParityPlace house — игрок обходит дом снаружи; запертый в доме с закрытой
#      дверью честно останавливается с ошибкой «путь закрыт» (0 шагов); после открытия двери выходит наружу по ступенькам
#      (Z 7->4->0) и заходит обратно.
# Везде 0 отказов 0x21, 0 расхождений Z/клетки (resync) и 0 подозрений на спидхак. Около 2 минут.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/pathfind-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=pathfind
. tools/check/server-copy.sh
DATA="$(default_data "${1:-}")"
START_X=1496
START_Y=1628
START_Z=10
DOOR_CLOSED=0x06A5
DOOR_OPEN=0x06A6

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/pathfind.sh <папка>"; exit 1; }
echo "1/6 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server ParityPlace
copy_make "$DATA"
grep -A6 '"speedhack_detection"' "$COPY/Configuration/FeatureFlags/flags.json" | grep -q '"Enabled": true' \
  || { echo "ОШИБКА: на сервере выключен флаг speedhack_detection — темп не проверяется (TARGET.md §5)."; exit 1; }
ok "копия сервера с пустым миром, файлы данных: $DATA"
copy_accounts pathfind Pathgm Pathplayer

echo "2/6 Запуск копии сервера (до 90 с)…"
copy_start
ok "сервер запущен, временный владелец создан"

echo "3/6 Игрок входит в первый раз…"
bot --config "$TMP/player.json" --create-character --no-packet-log --idle 1 > "$LOGS/pathfind-first.log" 2>&1
START="$(grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/pathfind-first.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g')"
[ -n "$START" ] || { fail "игрок не вошёл в мир. Лог: Logs/check/pathfind-first.log"; stop_server; echo "ОШИБКА: см. сообщения выше."; exit 1; }
ok "игрок создан и стоит в ($START)"

echo "4/6 Британия: обход домов, стен, крысы и лестница Z (около 1 минуты)…"
bot --config "$TMP/player.json" --data "$DATA" --no-packet-log \
  --action seenmob:0x0190 --action "at:$START_X,$START_Y,$START_Z" \
  --action "path:1493,1637,20,run" --action resync \
  --action "path:1469,1557,30,run" --action resync \
  --action "path:1474,1557,50,walk" --action at:1474,1557,50 --action resync \
  --action "path:1469,1557,30,run" --action at:1469,1557,30 --action resync \
  --idle 1 > "$LOGS/pathfind-city.log" 2>&1 &
PLAYER_PID=$!
sleep 1
bot --config "$TMP/gm.json" --create-character --no-packet-log \
  --action "say:[go 1495 1627 12" --action at:1495,1627 \
  --action "say:[add Rat set CantWalk true" --action target:here --action seenmob:0x00EE \
  --action "say:[go 1130 1577 0" --action at:1130,1577 \
  --action "say:[ParityPlace house" --action seenmulti: \
  --action "say:[go 1130 1580 7" --action at:1130,1580,7 \
  --action "say:[set Locked false" --action "target:$DOOR_CLOSED" \
  --action "say:[go ${START//,/ }" --action "at:${START%,*}" --action seenmob:0x0190 \
  --action "say:[set X $START_X Y $START_Y Z $START_Z" --action target:mob:0x0190 \
  --idle 1 > "$LOGS/pathfind-gm-setup.log" 2>&1
GM_CODE=$?
wait "$PLAYER_PID"
CITY_CODE=$?
[ "$GM_CODE" -eq 0 ] && ok "GM поставил крысу в (1495, 1627), дом в (1130, 1577) и перенёс игрока в ($START_X, $START_Y, $START_Z)" \
  || fail "GM не подготовил сцену: код $GM_CODE. Лог: Logs/check/pathfind-gm-setup.log"
if [ "$CITY_CODE" -eq 0 ]; then
  ok "городской маршрут и лестница пройдены"
else
  fail "городской маршрут: код $CITY_CODE. Лог: Logs/check/pathfind-city.log"
  grep -E "ОШИБКА|путь закрыт|пределе поиска|сервер ставит|не оказался" "$LOGS/pathfind-city.log" | head -n 5
fi

echo "5/6 Дом и дверь: обход дома, отказ при закрытой двери и проход после открытия…"
bot --config "$TMP/player.json" --data "$DATA" --no-packet-log \
  --action seenmob:0x0190 --action "at:1130,1584,0" \
  --action "path:1130,1570,0,run" --action resync \
  --idle 1 > "$LOGS/pathfind-house-around.log" 2>&1 &
PLAYER_PID=$!
sleep 1
bot --config "$TMP/gm.json" --no-packet-log \
  --action "say:[go 1469 1557 30" --action at:1469,1557 --action seenmob:0x0190 \
  --action "say:[set X 1130 Y 1584 Z 0" --action target:mob:0x0190 \
  --idle 1 > "$LOGS/pathfind-gm-move1.log" 2>&1 \
  || fail "GM не перенёс игрока к дому. Лог: Logs/check/pathfind-gm-move1.log"
wait "$PLAYER_PID"
AROUND_CODE=$?
[ "$AROUND_CODE" -eq 0 ] && ok "обход дома (multi) снаружи пройден" || fail "обход дома: код $AROUND_CODE. Лог: Logs/check/pathfind-house-around.log"

bot --config "$TMP/player.json" --data "$DATA" --no-packet-log \
  --action seenmob:0x0190 --action "at:1130,1578,7" \
  --action "near:$DOOR_CLOSED" \
  --action "path:1130,1584,0,run" \
  --idle 1 > "$LOGS/pathfind-closed-door.log" 2>&1 &
PLAYER_PID=$!
sleep 1
bot --config "$TMP/gm.json" --no-packet-log \
  --action "say:[go 1130 1570 0" --action at:1130,1570 --action seenmob:0x0190 \
  --action "say:[set X 1130 Y 1578 Z 7" --action target:mob:0x0190 \
  --idle 1 > "$LOGS/pathfind-gm-move2.log" 2>&1 \
  || fail "GM не перенёс игрока внутрь дома. Лог: Logs/check/pathfind-gm-move2.log"
wait "$PLAYER_PID"
CLOSED_CODE=$?
if [ "$CLOSED_CODE" -ne 0 ] && grep -q "путь закрыт: из (1130, 1578, 7)" "$LOGS/pathfind-closed-door.log"; then
  ok "закрытая дверь: бот честно остановился («путь закрыт» в (1130, 1578, 7))"
else
  fail "закрытая дверь: ожидался отказ «путь закрыт» (код $CLOSED_CODE). Лог: Logs/check/pathfind-closed-door.log"
fi

bot --config "$TMP/player.json" --data "$DATA" --no-packet-log \
  --action "at:1132,1581,0" \
  --action "path:1130,1581,4,walk" \
  --action "near:$DOOR_CLOSED" --action "use:$DOOR_CLOSED" --action "near:$DOOR_OPEN" \
  --action "path:1130,1577,7,walk" --action resync \
  --action "path:1130,1584,0,run" --action resync \
  --idle 1 > "$LOGS/pathfind-open-door.log" 2>&1
OPEN_CODE=$?
[ "$OPEN_CODE" -eq 0 ] && ok "открытая дверь: бот зашёл в дом через открытую дверь (Z 0->4->7) и вышел обратно (Z 7->4->0)" \
  || fail "открытая дверь: код $OPEN_CODE. Лог: Logs/check/pathfind-open-door.log"

SUMMARY="$(python3 - "$LOGS/pathfind-city.log" "$LOGS/pathfind-house-around.log" "$LOGS/pathfind-open-door.log" <<'PY'
import re, sys
paths = sys.argv[1:]
total_routes, clean_routes, total_syncs, total_steps, bad_stats = 0, 0, 0, 0, []
for p in paths:
    txt = open(p, encoding="utf-8", errors="replace").read()
    routes = re.findall(r"Путь A\* до .*отказов сервера (\d+), ресинхронизаций (\d+)\.", txt)
    total_routes += len(routes)
    clean_routes += sum(1 for r, s in routes if r == "0" and s == "0")
    total_syncs += len(re.findall(r"Сверка с сервером: .*как предсказано\.", txt))
    m = re.search(r"Движение: шагов (\d+), поворотов \d+, отказов сервера (\d+),", txt)
    if not m or int(m.group(1)) == 0 or int(m.group(2)) != 0:
        bad_stats.append(p)
    else:
        total_steps += int(m.group(1))
print(f"маршрутов {clean_routes}/{total_routes}, шагов {total_steps}, сверок Z/клетки {total_syncs}/7, отказов 0")
sys.exit(0 if total_routes == 8 and clean_routes == 8 and total_syncs == 7 and not bad_stats else 1)
PY
)"
if [ $? -eq 0 ]; then
  ok "сводка: $SUMMARY"
else
  fail "сводка маршрутов или сверок не сошлась ($SUMMARY; см. Logs/check/pathfind-*.log)"
fi

if grep -aqE "Speed hack|Movement queue overflow" "$LOGS/pathfind-server.log"; then
  fail "сервер отметил подозрение на спидхак или переполнение очереди шагов:"
  grep -aE "Speed hack|Movement queue overflow" "$LOGS/pathfind-server.log" | head -n 3
else
  ok "сервер не отметил подозрений на спидхак (журнал сервера: Logs/check/pathfind-server.log)"
fi

echo "6/6 Остановка копии сервера и проверка сохранности данных…"
stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
