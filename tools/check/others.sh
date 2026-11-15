#!/usr/bin/env bash
# Чужие мобы глазами бота (PLAN Стадия 3, задача 8 и критерий «Лаг, чужие»). Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/others.sh [<папка с файлами данных>]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic.
# Бот-GM кладёт игроку эфирного коня, переносит его на поле к западу от Британии, встаёт в нескольких клетках от его
# маршрута, ОДИН раз телепортирует его на 4 клетки ([set X) и ставит рядом крысу. Игрок бегает и ходит туда и обратно
# (бег, шаг, бег верхом — по 14 шагов), не уходя от GM дальше 8 клеток. GM смотрит и при выходе пишет по каждому чужому:
# сколько шагов пришло и показано, темп прихода против длительности показа, догонялки, перескоки (tools/check/others_check.py).
# Ожидается: GM увидел все шаги игрока и показал их все; темп показа совпадает с темпом прихода (±5%) в каждом режиме;
# перескок у игрока ровно один — телепорт; у крысы перескоков нет и показаны все её шаги (её темп только печатается:
# NPC ходит в своём темпе, сервер выбирает бит бега под длительность показа — BaseAI.ShouldRun).
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/others-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=others
. tools/check/server-copy.sh
DATA="$(default_data "${1:-}")"
FIELD_Y=1577
DROP_X=1124
TELE_X=1128
GM_X=1135
GM_Y=1581
RAT="1135,1590,0"

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/others.sh <папка>"; exit 1; }
echo "1/5 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server
copy_make "$DATA"
ok "копия сервера с пустым миром, файлы данных: $DATA"
copy_accounts others Othersgm Othersplayer

echo "2/5 Запуск копии сервера (до 90 с)…"
copy_start
ok "сервер запущен, временный владелец создан"

echo "3/5 Игрок входит в первый раз…"
bot --config "$TMP/player.json" --create-character --no-packet-log --idle 1 > "$LOGS/others-first.log" 2>&1
START="$(grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/others-first.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g')"
[ -n "$START" ] || { fail "игрок не вошёл в мир. Лог: Logs/check/others-first.log"; stop_server; echo "ОШИБКА: см. сообщения выше."; exit 1; }
ok "игрок создан и стоит в ($START)"

echo "4/5 Игрок бегает и ходит, GM смотрит, телепортирует его и ставит крысу (около 50 с)…"
bot --config "$TMP/player.json" --data "$DATA" \
  --action seenmob:0x0190 --action "at:$DROP_X,$FIELD_Y" --action "at:$TELE_X,$FIELD_Y" --action use:backpack \
  --action walk:e,14,run --action walk:w,14,walk \
  --action use:0x20DD --action mounted --action walk:e,14,run --action walk:w,14,run --idle 1 > "$LOGS/others-player.log" 2>&1 &
PLAYER_PID=$!
sleep 1
bot --config "$TMP/gm.json" --create-character --no-packet-log \
  --action "say:[go ${START//,/ }" --action "at:${START%,*}" --action seenmob:0x0190 \
  --action "say:[AddToPack EtherealHorse" --action target:mob:0x0190 \
  --action "say:[set X $DROP_X Y $FIELD_Y Z 0" --action target:mob:0x0190 \
  --action "say:[go $GM_X $GM_Y 0" --action "at:$GM_X,$GM_Y" --action seenmob:0x0190 \
  --action "say:[set X $TELE_X" --action target:mob:0x0190 \
  --action "say:[add Rat" --action "target:$RAT" --action seenmob:0x00EE --idle 40 > "$LOGS/others-gm.log" 2>&1
GM_CODE=$?
wait "$PLAYER_PID"
CODE=$?
[ "$GM_CODE" -eq 0 ] && ok "GM прошёл все действия" || fail "бот-GM: код $GM_CODE. Лог: Logs/check/others-gm.log"
if [ "$CODE" -eq 0 ]; then
  ok "игрок прошёл все действия"
else
  fail "игрок: код $CODE. Лог: Logs/check/others-player.log"
  grep -E "ОШИБКА|не оказался|не сел|подтвердил|нужны" "$LOGS/others-player.log" | head -n 5
fi
if REPORT="$(python3 "$ROOT/tools/check/others_check.py" "$LOGS/others-gm.log" "$LOGS/others-player.log")"; then
  echo "$REPORT"
else
  echo "$REPORT"
  FAILED=1
fi

echo "5/5 Остановка копии сервера и проверка сохранности данных…"
stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
