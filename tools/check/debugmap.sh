#!/usr/bin/env bash
# Демо-стенд для отладочной карты в Unity (PLAN Стадия 2, п. 13). Печатает логин и пароль для Unity и держит стенд, пока
# вы смотрите карту. Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/debugmap.sh [<папка с файлами данных>] [--no-wait]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Папка с файлами данных — по умолчанию remaster/uo-data/Ultima Online Classic.
# Бот-GM встаёт у «Sweet Dreams Inn» в Британии (стартовая точка нового персонажа города Britain: 1496, 1628), ставит рядом
# яблоко, две неподвижные крысы и дом (GM-команды [add и [ParityPlace), проверяет по настоящим файлам, что карта вокруг
# него собирается (действие бота scene:), и остаётся стоять — он тоже виден в Unity как моб.
# Дальше скрипт ждёт Enter: за это время зайдите в Unity (шаги — в ответе агента или ниже на экране). --no-wait — без
# ожидания (автоматическая проверка без Unity).
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/debugmap-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=debugmap
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
BOT_PID=""

stop_bot() { [ -n "$BOT_PID" ] && kill "$BOT_PID" 2>/dev/null; [ -n "$BOT_PID" ] && wait "$BOT_PID" 2>/dev/null; BOT_PID=""; }
cleanup() { stop_bot; stop_server; rm -rf "$TMP"; }

[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/debugmap.sh <папка>"; exit 1; }
echo "1/4 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server ParityPlace
copy_make "$DATA"
ok "копия сервера с пустым миром, файлы данных: $DATA"

copy_accounts demo Demogm
UNITY_LOGIN="verge$(python3 -c 'import secrets; print(secrets.token_hex(2))')"
UNITY_PASSWORD="demo-$(python3 -c 'import secrets; print(secrets.token_hex(4))')"

echo "2/4 Запуск копии сервера (до 90 с)…"
copy_start
ok "сервер запущен, временный владелец создан"

echo "3/4 Бот-GM расставляет объекты у «Sweet Dreams Inn» и проверяет карту (около 30 с)…"
ACTIONS=()
act() { ACTIONS+=(--action "$1"); }
go() { act "say:[go $1 $2"; act "at:$1,$2"; }
go 1496 1628
go 1493 1628; act "say:[add Apple"; act target:here; act seen:0x09D0
go 1499 1628; act "say:[add Rat set CantWalk true"; act target:here; act seenmob:0x00EE
go 1496 1624; act "say:[add Rat set CantWalk true"; act target:here; act seenmob:0x00EE
go 1500 1642; act "say:[ParityPlace house"; act seenmulti:
go 1496 1628; act scene:
act inspect:1493,1628; act inspect:1499,1628; act inspect:1500,1642; act inspect:
dotnet "$BOT_DLL" --config "$TMP/gm.json" --create-character --no-packet-log --data "$DATA" "${ACTIONS[@]}" --idle 3600 > "$LOGS/debugmap-gm.log" 2>&1 &
BOT_PID=$!
for _ in $(seq 1 120); do grep -aq "Клетка (1496, 1628)" "$LOGS/debugmap-gm.log" 2>/dev/null && break; kill -0 "$BOT_PID" 2>/dev/null || break; sleep 1; done
SCENE="$(grep -a "Сцена вокруг" "$LOGS/debugmap-gm.log" | head -n 1)"
if [ -z "$SCENE" ]; then
  fail "бот не дошёл до сборки карты. Лог: Logs/check/debugmap-gm.log"
  grep -aE "не может|не появился|не оказался|ОШИБКА|не записал" "$LOGS/debugmap-gm.log" | head -n 8
else
  echo "  $SCENE"
  # Земля — весь квадрат обзора 37×37 (все клетки внутри карты); часть дома и стены в Британии должны быть.
  echo "$SCENE" | grep -q "земля 1369" && ok "земля: 37×37 клеток" || fail "земли не 1369 клеток"
  echo "$SCENE" | grep -qE "части домов [1-9]" && ok "части дома попали на карту" || fail "частей домов нет"
  echo "$SCENE" | grep -qE "стена [1-9]" && ok "стены (здания Британии) есть" || fail "стен нет"
  echo "$SCENE" | grep -qE "поверхность [1-9]" && ok "проходимая поверхность есть" || fail "поверхности нет"
  # Окно клетки (то же, что Unity показывает по клику): строки объектов нужной клетки из журнала бота.
  cell_lines() { awk -v h="Клетка ($1)" 'index($0, h) {f=1; next} /Действие:|Клетка \(|Стою в мире/ {f=0} f' "$LOGS/debugmap-gm.log"; }
  cell_lines "1493, 1628" | grep -aqE "земля: tile 0x[0-9A-F]{4}" && cell_lines "1493, 1628" | grep -aqE "предмет 0x[0-9A-F]{8}: tile 0x09D0" \
    && ok "окно клетки: яблоко и земля под ним" || fail "в окне клетки (1493, 1628) нет яблока или земли"
  cell_lines "1499, 1628" | grep -aqE "моб 0x[0-9A-F]{8}: тело 0x00EE" && ok "окно клетки: крыса" || fail "в окне клетки (1499, 1628) нет крысы"
  cell_lines "1500, 1642" | grep -aqE "часть дома 0x[0-9A-F]{8}: tile" && ok "окно клетки: части дома на его клетке" || fail "в окне клетки (1500, 1642) нет частей дома"
  cell_lines "1496, 1628" | grep -aqE "моб 0x[0-9A-F]{8}: тело" && ok "окно клетки: сам бот-GM" || fail "в окне клетки (1496, 1628) нет бота"
fi

echo "4/4 Стенд готов"
if [ "$FAILED" -eq 0 ] && [ "$WAIT" -eq 1 ]; then
  cat <<TXT

  Сервер (временная копия): 127.0.0.1:2593
  Логин: $UNITY_LOGIN      Пароль: $UNITY_PASSWORD   (аккаунт создастся при первом входе)
  В Unity: создайте персонажа, город — Britain (стартовая точка 1496, 1628).
  Рядом стоят: яблоко, две крысы, дом на юге и бот-GM «Demogm».

TXT
  read -r -p "  Посмотрели? Нажмите Enter — стенд остановится. " _
fi
stop_bot
stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
