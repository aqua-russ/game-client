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
CHECK=map
. tools/check/server-copy.sh
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
copy_build_bot
if ! bot --self-check --data "$DATA" > "$LOGS/map-data.log" 2>&1; then
  echo "ОШИБКА: клиент не читает папку:"; grep -E "ОШИБКА" "$LOGS/map-data.log"; exit 1
fi
grep -E "^Карта Felucca|^Tiledata:|^Дома и корабли:" "$LOGS/map-data.log" | sed 's/^/  /'
copy_check_server DumpMapData
copy_make "$DATA"
ok "копия сервера с пустым миром читает файлы из папки: $DATA"

copy_accounts mapcheck Mapcheck

echo "2/4 Запуск копии сервера (до 90 с)…"
copy_start
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
copy_saves_check "ваши сохранения не тронуты" "папка Saves вашего сервера изменилась во время проверки"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
