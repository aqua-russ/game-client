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
CHECK=movement
. tools/check/server-copy.sh
DATA="$(default_data "${1:-}")"

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/movement.sh <папка>"; exit 1; }
echo "1/4 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server DumpMove ParityPlace
copy_make "$DATA"
ok "копия сервера с пустым миром, файлы данных: $DATA"
copy_accounts move Movegm

echo "2/4 Запуск копии сервера (до 90 с)…"
copy_start
ok "сервер запущен, временный владелец создан"

echo "3/4 Сверка MovementCheck против [DumpMove на 100 000 точках (около 30 с)…"
ACTIONS=()
act() { ACTIONS+=(--action "$1"); }
act "say:[go 1495 1629 10"; act "at:1495,1629"
act "say:[ParityPlace house"; act seenmulti:
act "say:[add WoodenChest"; act target:here; act seen:0x0E43
act "say:[add Rat set CantWalk true"; act target:here; act seenmob:0x00EE
act "movedump:100000,1337,$PARITY"

bot --config "$TMP/gm.json" --create-character --no-packet-log --data "$DATA" "${ACTIONS[@]}" --idle 1 > "$LOGS/movement-gm.log" 2>&1
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
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
