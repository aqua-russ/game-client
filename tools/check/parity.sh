#!/usr/bin/env bash
# Сценарий world-parity (PLAN Стадия 2, п. 12): мир бота против дампа сервера в пяти точках и в отдельных случаях.
# Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/parity.sh [<папка с файлами данных>]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic, если она есть,
# иначе — та, что в настройках вашего сервера (вода и вторая карта нужны настоящие, на заглушке сценарий не пройдёт).
# В пустом мире сервер сам предлагает создать владельца — скрипт отвечает за вас (случайный пароль, только на время).
# Бот-GM по очереди сверяет свой мир с дампом сервера ([DumpVisible, 11 раз):
#   вход; город (яблоко и неподвижная крыса); крыса переставлена ([move) на 3 клетки; телепорт на 8 клеток в пределах
#   обзора; возврат и удаление ([remove) яблока и крысы; подземелье; вода; дом ([ParityPlace house); корабль
#   ([ParityPlace ship); смена карты ([go Trammel: пусто, яблоко на новой карте) и возврат на Фелукку (всё снова видно).
# Сверяются серийник, картинка, цвет, место, количество, контейнер, координаты и номер карты. Затем проверяется число
# объектов в каждой точке и то, что обычному игроку [ParityPlace недоступна.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Подробности — Logs/check/parity-*.log, лог сервера — Logs/check/parity-server.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=parity
. tools/check/server-copy.sh
DATA="$(default_data "${1:-}")"

# Точки Фелуккы (Distribution/Data/Locations/felucca.json и карта настоящих файлов): центр Британии, первый уровень
# Despise (высота 39 под землёй), море у Трисинка (вода, высота -5; корабль — в 47 клетках от неё), пустая сухая земля.
CITY="1475 1645 20"; CITY_NEAR="1483 1645 20"; CITY_MOVED="1478,1645,20"
DUNGEON="5501 570 59"
WATER="2183 2800 -5"
SHIP="2230 2800 -5"
HOUSE="1228 1705 0"

# Действия бота: список для одного запуска.
ACTIONS=()
act() { ACTIONS+=(--action "$1"); }
go() { set -- $1; act "say:[go $1 $2 $3"; act "at:$1,$2"; }
dump() { act "dump:$PARITY"; }
spawn() {
  act "say:[add Apple"; act target:here; act seen:0x09D0
  act "say:[add Rat set CantWalk true"; act target:here; act seenmob:0x00EE
}

echo "1/5 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server ParityPlace
copy_make "$DATA"
ok "копия сервера с пустым миром${DATA:+, файлы данных: $DATA}"

copy_accounts parity Paritygm Parityplayer

echo "2/5 Запуск копии сервера (до 90 с)…"
copy_start
ok "сервер запущен, временный владелец создан"

echo "3/5 Бот-GM сверяет мир в пяти точках и в отдельных случаях (около минуты)…"
dump                                                                   # d0 вход: основа
go "$CITY"; spawn; dump                                                # d1 город
act "say:[move"; act target:mob:0x00EE; act "target:$CITY_MOVED"; dump  # d2 моб переставлен
go "$CITY_NEAR"; dump                                                  # d3 телепорт в пределах обзора
go "$CITY"; act "say:[remove"; act target:0x09D0; act "say:[remove"; act target:mob:0x00EE; dump  # d4 удаление
go "$DUNGEON"; spawn; dump                                             # d5 подземелье
go "$WATER"; spawn; dump                                               # d6 вода
go "$HOUSE"; act "say:[ParityPlace house"; act seenmulti:; dump        # d7 дом
go "$SHIP"; act "say:[ParityPlace ship"; act seenmulti:; dump          # d8 корабль
go "$CITY"; spawn; act "say:[go Trammel"; act onmap:1; act use:backpack; dump  # d9 другая карта: пусто (рюкзак сервер присылает заново — открываем, как world.sh)
act "say:[add Apple"; act target:here; act seen:0x09D0; dump           # d10 яблоко на другой карте
act "say:[go Felucca"; act onmap:0; act seen:0x09D0; act seenmob:0x00EE; act use:backpack; dump  # d11 возврат: всё снова видно
bot --config "$TMP/gm.json" --create-character --no-packet-log "${ACTIONS[@]}" --idle 1 > "$LOGS/parity-gm.log" 2>&1
CODE=$?
COUNTS=($(grep -o 'совпадает объектов [0-9]*' "$LOGS/parity-gm.log" | sed 's/совпадает объектов //'))
if [ "$CODE" -eq 0 ] && [ "${#COUNTS[@]}" -eq 12 ]; then
  ok "мир бота совпал с сервером 12 раз; объектов: $(echo "${COUNTS[@]}" | sed 's/ /, /g')"
else
  fail "сверка бота с сервером: код $CODE, совпало ${#COUNTS[@]} из 12. Лог: Logs/check/parity-gm.log"
  grep -E "Расхождение|не может|не появился|не оказался|ОШИБКА|не записал" "$LOGS/parity-gm.log" | head -n 12
fi

echo "4/5 Число объектов в каждой точке…"
if [ "${#COUNTS[@]}" -eq 12 ]; then
  BASE="${COUNTS[0]}"
  # Крыса на сервере носит свой рюкзак (виден боту как надетый предмет), поэтому «яблоко + крыса» — это 3 объекта.
  SPAWN=3
  check_count() { # номер, условие, ожидаемое, описание
    local n="${COUNTS[$1]}"
    if [ "$n" "$2" "$3" ]; then ok "$4: $n"; else fail "$4: $n объектов, ожидалось $2 $3"; fi
  }
  check_count 1 -eq $((BASE + SPAWN)) "город (яблоко, крыса и её рюкзак)"
  check_count 2 -eq $((BASE + SPAWN)) "крыса переставлена, число прежнее"
  check_count 3 -eq $((BASE + SPAWN)) "телепорт на 8 клеток, всё ещё в обзоре"
  check_count 4 -eq "$BASE" "после [remove остался только персонаж"
  check_count 5 -eq $((BASE + SPAWN)) "подземелье (старое осталось позади)"
  check_count 6 -eq $((BASE + SPAWN)) "вода (старое осталось позади)"
  check_count 7 -ge $((BASE + 3)) "дом (дом, вывеска, дверь)"
  check_count 8 -ge $((BASE + 4)) "корабль (корабль, рулевой, трюм, сходни); дом остался позади"
  # После [ParityPlace у персонажа появляется банковский ящик, а рюкзак открыт (сервер присылает его заново при смене карты):
  # эти объекты есть и на другой карте, и на Фелукке. Поэтому сравниваем между собой, а не с BASE.
  check_count 9 -eq $((COUNTS[11] - SPAWN)) "другая карта: яблоко и крыса Фелуккы не видны, остался только персонаж"
  check_count 10 -eq $((COUNTS[9] + 1)) "яблоко на другой карте"
  check_count 11 -eq $((COUNTS[9] + SPAWN)) "после возврата на Фелукку яблоко и крыса снова видны"
  check_count 9 -gt "$BASE" "на другой карте у персонажа есть открытый рюкзак"
fi
grep -q "Мир на карте 1" "$LOGS/parity-gm.log" && grep -q "Мир на карте 0" "$LOGS/parity-gm.log" \
  && ok "карты сменились туда и обратно (0 → 1 → 0)" || fail "бот не увидел смену карты. Лог: Logs/check/parity-gm.log"
[ "$(grep -c 'Дом или корабль (multi)' "$LOGS/parity-gm.log")" -eq 2 ] && ok "бот увидел и дом, и корабль" \
  || fail "дом или корабль не появились у бота. Лог: Logs/check/parity-gm.log"

echo "5/5 Обычный игрок пробует [ParityPlace…"
PLACED_BEFORE="$(grep -ac 'Parity place:' "$LOGS/parity-server.log")"
bot --config "$TMP/player.json" --create-character --no-packet-log --action "say:[ParityPlace house" --idle 2 > "$LOGS/parity-player.log" 2>&1
PLACED_AFTER="$(grep -ac 'Parity place:' "$LOGS/parity-server.log")"
if [ "$PLACED_BEFORE" -eq 2 ] && [ "$PLACED_AFTER" -eq 2 ]; then ok "GM поставил дом и корабль, у игрока команда не сработала"; else fail "команд [ParityPlace на сервере: до игрока $PLACED_BEFORE, после $PLACED_AFTER (ждали 2 и 2). Лог: Logs/check/parity-server.log"; fi

stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
