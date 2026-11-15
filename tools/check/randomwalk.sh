#!/usr/bin/env bash
# Случайная ходьба бота по настоящему серверу (PLAN Стадия 3, задача 10, random-walk; критерий «Ходьба и бег» без лага).
# Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/randomwalk.sh [<папка с файлами данных>] [--quick] [--seed <N>]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic.
# Ходит обычный игрок (у персонала сервер темп не проверяет), флаг speedhack_detection включён; во временной копии
# включено stamina.useMountStaminaOnlyWhenOverloaded=True (ADR 0024 п. 8; на стенде и в шаблоне — Стадия 5.7).
# Бот-GM ставит игрока у «Sweet Dreams Inn» в Британии (1496, 1628) и кладёт ему эфирного коня. Игрок ходит случайно внутри
# квадрата города (1440, 1580)–(1530, 1670): дома, заборы, деревья, склоны (высота от 0 до 30). Сторону выбирает только
# ту, куда пускает его проверка шага, держит 1–8 шагов, бегом и шагом вперемешку. Сначала 1000 шагов пешком, потом садится
# на коня — ещё 9000 верхом; после каждой тысячи спрашивает сервер, где персонаж (ресинхронизация), — клетка и высота
# должны совпасть с предсказанием клиента. Ожидается: 10 000 шагов, ни одного отказа сервера и ни одной ресинхронизации
# из-за рассинхрона, все сверки совпали, в журнале сервера нет подозрений на спидхак. Около 25 минут.
# --quick — 1000 шагов (100 пешком и 900 верхом), около 3 минут. --seed <N> — номер первой прогулки (по умолчанию 1;
# каждая следующая тысяча — следующий номер): с другим номером — другие пути.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/randomwalk-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=randomwalk
. tools/check/server-copy.sh
DATA=""
TOTAL=10000
SEED=1
while [ $# -gt 0 ]; do
  case "$1" in
    --quick) TOTAL=1000 ;;
    --seed) SEED="${2:-}"; shift ;;
    *) DATA="$1" ;;
  esac
  shift
done
case "$SEED" in ''|*[!0-9]*) echo "ОШИБКА: --seed — целое число, например --seed 5"; exit 1 ;; esac
DATA="$(default_data "$DATA")"
START_X=1496
START_Y=1628
START_Z=10
SQUARE="1440,1580,1530,1670"
CHUNK=1000

# Шаги пешком — десятая часть, остальные верхом; кусками по тысяче, после каждого — сверка с сервером.
FOOT=$((TOTAL / 10))
MOUNTED=$((TOTAL - FOOT))
CHUNKS=0
ACTIONS=(--action seenmob:0x0190 --action "at:$START_X,$START_Y,$START_Z" --action use:backpack)
add_chunks() {
  local left="$1" steps
  while [ "$left" -gt 0 ]; do
    steps=$(( left < CHUNK ? left : CHUNK ))
    CHUNKS=$((CHUNKS + 1))
    ACTIONS+=(--action "randomwalk:$steps,$((SEED + CHUNKS - 1)),$SQUARE,mix" --action resync)
    left=$((left - steps))
  done
}
add_chunks "$FOOT"
ACTIONS+=(--action use:0x20DD --action mounted)
add_chunks "$MOUNTED"

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/randomwalk.sh <папка>"; exit 1; }
echo "1/5 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server
copy_make "$DATA"
python3 - "$COPY/Configuration/modernuo.json" <<'PY'
import json, sys
path = sys.argv[1]
with open(path, encoding="utf-8") as f:
    config = json.load(f)
config.setdefault("settings", {})["stamina.useMountStaminaOnlyWhenOverloaded"] = "True"
with open(path, "w", encoding="utf-8") as f:
    json.dump(config, f, indent=2)
PY
grep -A6 '"speedhack_detection"' "$COPY/Configuration/FeatureFlags/flags.json" | grep -q '"Enabled": true' \
  || { echo "ОШИБКА: на сервере выключен флаг speedhack_detection — темп не проверяется (TARGET.md §5)."; exit 1; }
ok "копия сервера с пустым миром, файлы данных: $DATA"
copy_accounts randomwalk Randomgm Wanderer

echo "2/5 Запуск копии сервера (до 90 с)…"
copy_start
ok "сервер запущен, временный владелец создан"

echo "3/5 Игрок входит в первый раз…"
bot --config "$TMP/player.json" --create-character --no-packet-log --idle 1 > "$LOGS/randomwalk-first.log" 2>&1
START="$(grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/randomwalk-first.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g')"
[ -n "$START" ] || { fail "игрок не вошёл в мир. Лог: Logs/check/randomwalk-first.log"; stop_server; echo "ОШИБКА: см. сообщения выше."; exit 1; }
ok "игрок создан и стоит в ($START)"

if [ "$TOTAL" -ge 10000 ]; then MINUTES="около 25 минут"; else MINUTES="около 3 минут"; fi
echo "4/5 Игрок ходит случайно: $FOOT шагов пешком и $MOUNTED верхом ($MINUTES)…"
bot --config "$TMP/player.json" --data "$DATA" --no-packet-log "${ACTIONS[@]}" --idle 1 > "$LOGS/randomwalk-player.log" 2>&1 &
PLAYER_PID=$!
sleep 1
bot --config "$TMP/gm.json" --create-character --no-packet-log \
  --action "say:[go ${START//,/ }" --action "at:${START%,*}" --action seenmob:0x0190 \
  --action "say:[AddToPack EtherealHorse" --action target:mob:0x0190 \
  --action "say:[set X $START_X Y $START_Y Z $START_Z" --action target:mob:0x0190 --idle 1 > "$LOGS/randomwalk-gm.log" 2>&1
GM_CODE=$?
wait "$PLAYER_PID"
CODE=$?
[ "$GM_CODE" -eq 0 ] && ok "GM дал игроку коня и перенёс его в ($START_X, $START_Y, $START_Z)" || fail "бот-GM не перенёс игрока: код $GM_CODE. Лог: Logs/check/randomwalk-gm.log"
if [ "$CODE" -eq 0 ]; then
  ok "игрок прошёл все действия"
else
  fail "игрок: код $CODE. Лог: Logs/check/randomwalk-player.log"
  grep -E "ОШИБКА|застрял|пройдено|сервер ставит|не оказался|не сел|нужны" "$LOGS/randomwalk-player.log" | head -n 5
fi
if REPORT="$(python3 - "$LOGS/randomwalk-player.log" "$TOTAL" "$CHUNKS" <<'PY'
import re, sys
text = open(sys.argv[1], encoding="utf-8", errors="replace").read()
total, chunks = int(sys.argv[2]), int(sys.argv[3])
walks = re.findall(r"Случайная ходьба: шагов (\d+) \(бегом (\d+), шагом (\d+)\), отрезков (\d+), отказов сервера (\d+), ресинхронизаций (\d+)", text)
syncs = len(re.findall(r"Сверка с сервером: .*как предсказано\.", text))
steps = sum(int(w[0]) for w in walks)
runs = sum(int(w[1]) for w in walks)
rejects = sum(int(w[4]) for w in walks)
resyncs = sum(int(w[5]) for w in walks)
segments = sum(int(w[3]) for w in walks)
print(f"шагов {steps} из {total} (бегом {runs}, шагом {steps - runs}, отрезков {segments}), отказов сервера {rejects}, "
      f"ресинхронизаций из-за рассинхрона {resyncs}, сверок с сервером совпало {syncs} из {chunks}")
sys.exit(0 if steps == total and rejects == 0 and resyncs == 0 and syncs == chunks and len(walks) == chunks else 1)
PY
)"; then
  ok "$REPORT"
else
  fail "${REPORT:-итог прогулки не найден в журнале}"
fi
if grep -aqE "Speed hack|Movement queue overflow" "$LOGS/randomwalk-server.log"; then
  fail "сервер отметил подозрение на спидхак или переполнение очереди шагов:"
  grep -aE "Speed hack|Movement queue overflow" "$LOGS/randomwalk-server.log" | head -n 3
else
  ok "сервер не отметил подозрений на спидхак (журнал сервера: Logs/check/randomwalk-server.log)"
fi

echo "5/5 Остановка копии сервера и проверка сохранности данных…"
stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
