#!/usr/bin/env bash
# Сверка мира бота с сервером (PLAN Стадия 2, часть А: задачи 2.7–2.11) одной командой. Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/world.sh [<папка с файлами данных>]   (по умолчанию remaster/uo-data/Ultima Online Classic)
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются.
# В пустом мире сервер сам предлагает создать владельца — проверка отвечает за вас (случайный пароль, только на время
# проверки). Бот-GM сверяет свой мир с дампом сервера ([DumpVisible): сразу после входа, после [add Apple и открытия
# рюкзака, после [remove; затем обычный игрок пробует [DumpVisible — сервер не должен его выполнить.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Подробности — Logs/check/world-*.log, лог сервера — Logs/check/world-server.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=world
. tools/check/server-copy.sh
DATA="$(default_data "${1:-}")"

echo "1/4 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server
copy_make "$DATA"
ok "копия сервера с пустым миром${DATA:+, файлы данных: $DATA}"
copy_accounts worldcheck Worldcheck Worldplayer

echo "2/4 Запуск копии сервера (до 90 с)…"
copy_start
grep -aq "Owner account created" "$LOGS/world-server.log" && ok "сервер запущен, временный владелец создан" || fail "владелец не создан (Logs/check/world-server.log)"

echo "3/4 Бот-GM сверяет свой мир с сервером…"
bot --config "$TMP/gm.json" --create-character --no-packet-log \
  --action "dump:$PARITY" \
  --action "say:[add Apple" --action target:here --action seen:0x09D0 --action use:backpack --action "dump:$PARITY" \
  --action "say:[remove" --action target:0x09D0 --action "dump:$PARITY" --idle 1 > "$LOGS/world-gm.log" 2>&1
CODE=$?
COUNT="$(grep -c 'Сверка с сервером: совпадает' "$LOGS/world-gm.log")"
if [ "$CODE" -eq 0 ] && [ "$COUNT" -eq 3 ]; then
  ok "мир бота совпал с сервером 3 раза: $(grep -o 'совпадает объектов [0-9]*' "$LOGS/world-gm.log" | sed 's/совпадает объектов //' | paste -sd, - | sed 's/,/, /g') объектов (вход; яблоко и рюкзак; после [remove)"
else
  fail "сверка бота с сервером: код $CODE, совпало $COUNT из 3. Лог: Logs/check/world-gm.log"
  grep -E "Расхождение|не может|ОШИБКА" "$LOGS/world-gm.log" | head -n 10
fi
grep -q "Рюкзак 0x" "$LOGS/world-gm.log" && ok "$(grep -o 'Рюкзак 0x[0-9A-F]* открыт: предметов [0-9]*' "$LOGS/world-gm.log" | head -n 1)" \
  || fail "рюкзак не открыт. Лог: Logs/check/world-gm.log"
grep -q "Предмет 0x09D0 на земле" "$LOGS/world-gm.log" && ok "[add Apple + прицел в клетку под ногами: яблоко появилось" \
  || fail "яблоко не появилось. Лог: Logs/check/world-gm.log"

echo "4/4 Обычный игрок пробует [DumpVisible…"
bot --config "$TMP/player.json" --create-character --no-packet-log --action "dump:$PARITY" > "$LOGS/world-player.log" 2>&1
if grep -q "сервер не записал дамп" "$LOGS/world-player.log"; then ok "игроку команда недоступна: дампа нет"; else fail "у игрока дамп записан или бот упал иначе. Лог: Logs/check/world-player.log"; fi

stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
