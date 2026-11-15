#!/usr/bin/env bash
# Стенд для глазной проверки движения в Unity (PLAN Стадия 3, задача 9): рядом с вами ходит бот-игрок. Печатает логин и
# пароль для Unity и держит стенд, пока вы смотрите. Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/walkdemo.sh [<папка с файлами данных>] [--no-wait]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic.
# Бот-GM кладёт боту-игроку эфирного коня и ставит его на улицу Британии у «Sweet Dreams Inn» — в четырёх клетках к
# востоку от стартовой точки нового персонажа города Britain (1496, 1628). Бот-игрок ходит прямоугольником 6 × 3 клетки
# (1500–1506, 1629–1632): три круга бегом и шагом пешком, потом садится на коня и около 15 минут ходит верхом (бег и шаг).
# Бот-GM стоит рядом (1494, 1630). Дальше скрипт ждёт Enter: за это время зайдите в Unity (шаги — в ответе агента или
# ниже на экране). --no-wait — без ожидания: бот проходит по кругу бегом, шагом и верхом, и скрипт проверяет, что
# сервер ни разу не отказал и GM показал все шаги бота (автоматическая проверка без Unity).
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/walkdemo-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=walkdemo
. tools/check/server-copy.sh
DATA=""
WAIT=1
for arg in "$@"; do
  case "$arg" in
    --no-wait) WAIT=0 ;;
    *) DATA="$arg" ;;
  esac
done
DATA="$(default_data "$DATA")"
LOOP_X=1500
LOOP_Y=1629
LOOP_Z=10
GM_X=1494
GM_Y=1630
PLAYER_PID=""
GM_PID=""

stop_bots() {
  for pid in "$PLAYER_PID" "$GM_PID"; do [ -n "$pid" ] && kill "$pid" 2>/dev/null; done
  for pid in "$PLAYER_PID" "$GM_PID"; do [ -n "$pid" ] && wait "$pid" 2>/dev/null; done
  PLAYER_PID=""; GM_PID=""
}

# Круг по прямоугольнику: восток 6, юг 3, запад 6, север 3 — возвращается в начало. $1 — run или walk.
LOOP=()
loop() { LOOP+=(--action "walk:e,6,$1" --action "walk:s,3,$1" --action "walk:w,6,$1" --action "walk:n,3,$1"); }

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/walkdemo.sh <папка>"; exit 1; }
echo "1/4 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server
copy_make "$DATA"
ok "копия сервера с пустым миром, файлы данных: $DATA"
copy_accounts walkdemo Demogm Walker
UNITY_LOGIN="verge$(python3 -c 'import secrets; print(secrets.token_hex(2))')"
UNITY_PASSWORD="demo-$(python3 -c 'import secrets; print(secrets.token_hex(4))')"

echo "2/4 Запуск копии сервера (до 90 с)…"
copy_start
ok "сервер запущен, временный владелец создан"

echo "3/4 Бот-игрок входит, GM ставит его на улицу у «Sweet Dreams Inn» (около 40 с)…"
bot --config "$TMP/player.json" --create-character --no-packet-log --idle 1 > "$LOGS/walkdemo-first.log" 2>&1
START="$(grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/walkdemo-first.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g')"
[ -n "$START" ] || { fail "бот-игрок не вошёл в мир. Лог: Logs/check/walkdemo-first.log"; stop_server; echo "ОШИБКА: см. сообщения выше."; exit 1; }

if [ "$WAIT" -eq 1 ]; then
  for _ in 1 2 3; do loop run; loop walk; done
  MOUNTED_LOOPS=125
  GM_IDLE=3600
else
  loop run; loop walk
  MOUNTED_LOOPS=1
  # GM стоит, пока бот-игрок ходит (около 25 с), и при выходе пишет, что видел («Чужой …»).
  GM_IDLE=45
fi
LOOP+=(--action use:0x20DD --action mounted)
for _ in $(seq 1 "$MOUNTED_LOOPS"); do loop run; [ "$WAIT" -eq 1 ] && { loop run; loop walk; }; done

bot --config "$TMP/player.json" --data "$DATA" \
  --action seenmob:0x0190 --action "at:$LOOP_X,$LOOP_Y" --action use:backpack "${LOOP[@]}" --idle 1 \
  > "$LOGS/walkdemo-player.log" 2>&1 &
PLAYER_PID=$!
sleep 1
GM_ACTIONS=(--action "say:[go ${START//,/ }" --action "at:${START%,*}" --action seenmob:0x0190
  --action "say:[AddToPack EtherealHorse" --action target:mob:0x0190
  --action "say:[set X $LOOP_X Y $LOOP_Y Z $LOOP_Z" --action target:mob:0x0190
  --action "say:[go $GM_X $GM_Y $LOOP_Z" --action "at:$GM_X,$GM_Y")
bot --config "$TMP/gm.json" --create-character --no-packet-log "${GM_ACTIONS[@]}" --idle "$GM_IDLE" > "$LOGS/walkdemo-gm.log" 2>&1 &
GM_PID=$!

for _ in $(seq 1 120); do
  grep -aq "Ходьба " "$LOGS/walkdemo-player.log" 2>/dev/null && break
  kill -0 "$PLAYER_PID" 2>/dev/null || break
  sleep 1
done
if grep -aq "Ходьба " "$LOGS/walkdemo-player.log"; then
  ok "бот-игрок ходит по кругу: $(grep -a -o 'Ходьба .*' "$LOGS/walkdemo-player.log" | head -n 1 | sed 's/;.*//')"
else
  fail "бот-игрок не начал ходить. Лог: Logs/check/walkdemo-player.log"
  grep -aE "ОШИБКА|не оказался|не сел|подтвердил|нужны" "$LOGS/walkdemo-player.log" | head -n 5
fi

if [ "$FAILED" -eq 0 ] && [ "$WAIT" -eq 0 ]; then
  wait "$PLAYER_PID"; PLAYER_CODE=$?; PLAYER_PID=""
  [ "$PLAYER_CODE" -eq 0 ] && ok "бот-игрок прошёл круг бегом, шагом и верхом" || fail "бот-игрок: код $PLAYER_CODE. Лог: Logs/check/walkdemo-player.log"
  for _ in $(seq 1 60); do kill -0 "$GM_PID" 2>/dev/null || break; sleep 1; done
  stop_bots
  if REPORT="$(python3 - "$LOGS/walkdemo-player.log" "$LOGS/walkdemo-gm.log" <<'PY'
import re, sys
player, gm = (open(p, encoding="utf-8", errors="replace").read() for p in sys.argv[1:3])
walks = re.findall(r"Ходьба [^:]*: шагов (\d+), отказов сервера (\d+), ресинхронизаций (\d+)", player)
steps = sum(int(w[0]) for w in walks)
bad = [w for w in walks if w[1] != "0" or w[2] != "0"]
seen = re.search(r"Чужой 0x[0-9A-F]{8} тело 0x0190: пришло (\d+), показано (\d+), выброшено (\d+)", gm)
ok = True
if not walks or bad:
    ok = False
    print(f"  ОШИБКА: отказы или ресинхронизации у бота-игрока: {bad or 'ходьбы нет'}")
else:
    print(f"  ОК: бот-игрок: {len(walks)} отрезков, {steps} шагов, отказов сервера 0, ресинхронизаций 0")
if not seen:
    ok = False
    print("  ОШИБКА: GM не написал, что видел бота-игрока (строка «Чужой … тело 0x0190»)")
elif int(seen[1]) != steps or int(seen[2]) != steps or seen[3] != "0":
    ok = False
    print(f"  ОШИБКА: GM видел шагов {seen[1]}, показал {seen[2]}, выбросил {seen[3]}; бот сделал {steps}")
else:
    print(f"  ОК: GM увидел и показал все {steps} шагов бота-игрока")
sys.exit(0 if ok else 1)
PY
)"; then echo "$REPORT"; else echo "$REPORT"; FAILED=1; fi
fi

echo "4/4 Стенд готов"
if [ "$FAILED" -eq 0 ] && [ "$WAIT" -eq 1 ]; then
  cat <<TXT

  Сервер (временная копия): 127.0.0.1:2593
  Логин: $UNITY_LOGIN      Пароль: $UNITY_PASSWORD   (аккаунт создастся при первом входе)
  В Unity: создайте персонажа, город — Britain (стартовая точка 1496, 1628).
  В четырёх клетках к востоку бот «Walker» ходит прямоугольником: бегом и шагом, потом около 15 минут верхом.
  Рядом стоит бот-GM «Demogm». Не загораживайте боту дорогу: упрётся — остановится.

TXT
  read -r -p "  Посмотрели? Нажмите Enter — стенд остановится. " _
  if ! kill -0 "$PLAYER_PID" 2>/dev/null; then
    echo "  Бот-игрок закончил раньше: $(grep -aE "ОШИБКА|не подтвердил|Выход" "$LOGS/walkdemo-player.log" | tail -n 1)"
  fi
fi
stop_bots
stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
