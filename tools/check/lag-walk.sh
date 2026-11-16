#!/usr/bin/env bash
# Инструмент лага и замеры движения на настоящем сервере (PLAN Стадия 3, п. 12; ADR 0020).
# Печатает «ОК» или «ОШИБКА: …».
#
#   tools/check/lag-walk.sh [<папка данных>] [--quick | --full] [--only 0|100|200|400] [--seed <N>]
#
# Работает на ВРЕМЕННОЙ копии вашего сервера с пустым миром: ваши сохранения, аккаунты и настройки не трогаются
# (проверяется в конце). Флаг speedhack_detection включён; для 10 000 шагов включено
# stamina.useMountStaminaOnlyWhenOverloaded=True (ADR 0024 п. 8, до 5.7).
#   --quick (по умолчанию, ~4 мин): сверка golden/02-walk-20.tsv, маршрут walk-route по Британии и лестница,
#     прямые отрезки темпа (пешком и верхом) под пингом 0, 100 ±30, 200 ±30 и 400 ±60 мс (200 ±30 в каждую сторону),
#     а также чужие мобы при всплесках входящего лага до 500 мс (догонялка без лишних перескоков).
#   --full (~75 мин на все 3 режима): всё из --quick + по 10 000 шагов подряд (1000 пешком + 9000 верхом) для
#     0, 100 ±30 и 200 ±30 мс; --only N запускает один режим.
# Ваш сервер должен быть остановлен (порт 2593 свободен) и собран (dotnet build -c Release в server/ModernUO).
# Логи — Logs/check/lagwalk-*.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
CHECK=lagwalk
. tools/check/server-copy.sh

DATA=""
MODE="quick"
ONLY=""
SEED=1
while [ $# -gt 0 ]; do
  case "$1" in
    --quick) MODE="quick" ;;
    --full) MODE="full" ;;
    --only) ONLY="${2:-}"; shift ;;
    --seed) SEED="${2:-}"; shift ;;
    *) DATA="$1" ;;
  esac
  shift
done
case "$SEED" in ''|*[!0-9]*) echo "ОШИБКА: --seed — целое число, например --seed 5"; exit 1 ;; esac
case "$ONLY" in ''|0|100|200|400) ;; *) echo "ОШИБКА: --only — одно из 0, 100, 200, 400"; exit 1 ;; esac
DATA="$(default_data "$DATA")"
[ -n "$DATA" ] || { echo "ОШИБКА: не найдена папка файлов данных. Передайте её: tools/check/lag-walk.sh <папка>"; exit 1; }

# 0, 100±30 и 200±30 мс пинга = по 0, 50±15 и 100±15 мс в каждую сторону (TARGET.md §3.1); 400 = 200±30 в каждую.
lag_params() {
  case "$1" in
    0) echo "0 0 0 5.0" ;;
    100) echo "50 15 90 5.0" ;;
    200) echo "100 15 185 5.0" ;;
    400) echo "200 30 370 10.0" ;;
  esac
}

ROUTE=(
  --action seenmob:0x0190 --action "at:1496,1628,10" --action use:backpack
  --action goto:1490,1626,run --action goto:1480,1606,walk --action goto:1474,1600,run
  --action goto:1466,1600,run --action goto:1460,1568,run --action goto:1456,1558,walk
  --action goto:1464,1555,run --action goto:1465,1557,walk --action goto:1469,1557,walk --action resync
  --action goto:1474,1557,walk --action at:1474,1557,50 --action resync
  --action goto:1469,1557,run --action at:1469,1557,30 --action resync
)

make_full_actions() {
  local foot=1000 mounted=9000 chunk=1000 chunks=0 left
  FULL_ACTIONS=(--action seenmob:0x0190 --action "at:1496,1628,10" --action use:backpack)
  left="$foot"
  while [ "$left" -gt 0 ]; do
    chunks=$((chunks + 1))
    FULL_ACTIONS+=(--action "randomwalk:$chunk,$((SEED + chunks - 1)),1440,1580,1530,1670,mix" --action resync)
    left=$((left - chunk))
  done
  FULL_ACTIONS+=(--action use:0x20DD --action mounted)
  left="$mounted"
  while [ "$left" -gt 0 ]; do
    chunks=$((chunks + 1))
    FULL_ACTIONS+=(--action "randomwalk:$chunk,$((SEED + chunks - 1)),1440,1580,1530,1670,mix" --action resync)
    left=$((left - chunk))
  done
}

create_and_prep_player() {
  local tag="$1" cname="$2" cfg="$TMP/$1.json"
  printf '{"login":"%s_%s","password":"%s","character":"%s"}\n' "$OWNER" "$tag" "$PASSWORD" "$cname" > "$cfg"
  chmod 600 "$cfg"
  bot --config "$cfg" --create-character --no-packet-log --idle 1 > "$LOGS/lagwalk-init-$tag.log" 2>&1
  local start
  start="$(grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/lagwalk-init-$tag.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g')"
  [ -n "$start" ] || { fail "игрок $tag ($cname) не вошёл в мир"; return 1; }
  echo "$start"
}

char_tag() {
  case "$1" in
    0) echo "a" ;;
    100) echo "b" ;;
    200) echo "c" ;;
    400) echo "d" ;;
  esac
}

move_player_by_gm() {
  local cfg="$1" start="$2" x="$3" y="$4" z="$5" horse="$6" tag="$7"
  local gm_acts=(--action "say:[go ${start//,/ }" --action "at:${start%,*}" --action seenmob:0x0190)
  if [ "$horse" -eq 1 ]; then
    gm_acts+=(--action "say:[AddToPack EtherealHorse" --action target:mob:0x0190)
  fi
  gm_acts+=(--action "say:[set X $x Y $y Z $z" --action target:mob:0x0190 --idle 1)
  bot --config "$TMP/gm.json" --no-packet-log "${gm_acts[@]}" > "$LOGS/lagwalk-gm-$tag.log" 2>&1
}

run_quick_level() {
  local lvl="$1" hd hj min_rtt pct ctag
  read -r hd hj min_rtt pct <<< "$(lag_params "$lvl")"
  ctag="$(char_tag "$lvl")"
  echo "  Режим пинга $lvl мс (±$((hj * 2)) мс; в одну сторону $hd ±$hj мс)…"
  local start
  start="$(create_and_prep_player "q$lvl" "Walker$ctag")" || return
  local cfg="$TMP/q$lvl.json"
  bot --config "$cfg" --data "$DATA" --lag "$hd,$hj" --lag-seed "$SEED" "${ROUTE[@]}" --idle 1 \
    > "$LOGS/lagwalk-route-$lvl.log" 2>&1 &
  local pid=$!
  sleep 2
  move_player_by_gm "$cfg" "$start" 1496 1628 10 1 "route-$lvl" || fail "GM: маршрут $lvl"
  wait "$pid" || fail "маршрут $lvl: бот вышел с ошибкой (Logs/check/lagwalk-route-$lvl.log)"
  python3 "$ROOT/tools/check/lag_report.py" player "$LOGS/lagwalk-route-$lvl.log" "$min_rtt" "$pct" 100 || FAILED=1

  local pkt_log
  pkt_log="$(grep -o 'Лог пакетов: .*' "$LOGS/lagwalk-route-$lvl.log" | head -n 1 | sed 's/Лог пакетов: //')"
  if [ -n "$pkt_log" ] && [ -f "$pkt_log" ]; then
    if OUT="$(bot --check-walk "$pkt_log" --data "$DATA")"; then
      ok "сверка шагов и Z по карте ($lvl мс): $(echo "$OUT" | head -n 1)"
    else
      fail "сверка шагов ($lvl мс): $OUT"
    fi
  fi

  bot --config "$cfg" --data "$DATA" --no-packet-log --lag "$hd,$hj" --lag-seed "$SEED" \
    --action seenmob:0x0190 --action "at:1130,1577" --action use:backpack \
    --action walk:e,40,run --action walk:w,25,walk --action use:0x20DD --action mounted --action walk:e,50,run --idle 1 \
    > "$LOGS/lagwalk-field-$lvl.log" 2>&1 &
  pid=$!
  sleep 2
  move_player_by_gm "$cfg" "1469,1557,30" 1130 1577 0 0 "field-$lvl" || fail "GM: поле $lvl"
  wait "$pid" || fail "отрезки на поле $lvl: бот вышел с ошибкой (Logs/check/lagwalk-field-$lvl.log)"
  python3 "$ROOT/tools/check/lag_report.py" player "$LOGS/lagwalk-field-$lvl.log" "$min_rtt" "$pct" 115 || FAILED=1
}

run_full_level() {
  local lvl="$1" hd hj min_rtt pct ctag
  read -r hd hj min_rtt pct <<< "$(lag_params "$lvl")"
  ctag="$(char_tag "$lvl")"
  echo "  Полный прогон 10 000 шагов при пинге $lvl мс (±$((hj * 2)) мс; около 25 минут)…"
  local start
  start="$(create_and_prep_player "f$lvl" "Full$ctag")" || return
  local cfg="$TMP/f$lvl.json"
  make_full_actions
  bot --config "$cfg" --data "$DATA" --no-packet-log --lag "$hd,$hj" --lag-seed "$SEED" "${FULL_ACTIONS[@]}" --idle 1 \
    > "$LOGS/lagwalk-full-$lvl.log" 2>&1 &
  local pid=$!
  sleep 2
  move_player_by_gm "$cfg" "$start" 1496 1628 10 1 "full-$lvl" || fail "GM: 10000 шагов $lvl"
  wait "$pid" || fail "10000 шагов ($lvl мс): бот вышел с ошибкой (Logs/check/lagwalk-full-$lvl.log)"
  python3 "$ROOT/tools/check/lag_report.py" player "$LOGS/lagwalk-full-$lvl.log" "$min_rtt" "$pct" 10000 || FAILED=1
}

run_others_spike() {
  echo "  Чужие мобы при всплесках лага до 500 мс (пешком, бегом и верхом + крыса)…"
  local start
  start="$(create_and_prep_player "oth" "Walkeroth")" || return
  local cfg="$TMP/oth.json"
  bot --config "$cfg" --data "$DATA" --no-packet-log \
    --action seenmob:0x0190 --action "at:1124,1577" --action use:backpack --action seenmob:0x00EE \
    --action walk:e,14,run --action walk:w,14,run --action walk:e,14,walk --action walk:w,14,walk \
    --action use:0x20DD --action mounted \
    --action walk:e,14,run --action walk:w,14,run --action walk:e,14,run --action walk:w,14,run \
    --action walk:e,14,run --action walk:w,14,run --idle 1 \
    > "$LOGS/lagwalk-others-player.log" 2>&1 &
  local pid=$!
  sleep 1
  bot --config "$TMP/gm.json" --no-packet-log --lag 50,15 --lag-seed "$SEED" \
    --action "say:[go ${start//,/ }" --action "at:${start%,*}" --action seenmob:0x0190 \
    --action "say:[AddToPack EtherealHorse" --action target:mob:0x0190 \
    --action "say:[set X 1124 Y 1577 Z 0" --action target:mob:0x0190 \
    --action "say:[go 1135 1581 0" --action "at:1135,1581" \
    --action "say:[add Rat" --action "target:1135,1585,0" --action seenmob:0x00EE \
    --action "waitsteps:0x0190,5" --action "lagspike:500" \
    --action "waitsteps:0x0190,18" --action "lagspike:500" \
    --action "waitsteps:0x0190,22" --action "lagspike:500" \
    --action "waitsteps:0x0190,18" --action "lagspike:500" --idle 20 \
    > "$LOGS/lagwalk-others-gm.log" 2>&1
  local gm_code=$?
  wait "$pid"
  local p_code=$?
  [ "$gm_code" -eq 0 ] || fail "бот-GM (всплески): код $gm_code (Logs/check/lagwalk-others-gm.log)"
  [ "$p_code" -eq 0 ] || fail "игрок (всплески): код $p_code (Logs/check/lagwalk-others-player.log)"
  python3 "$ROOT/tools/check/lag_report.py" others "$LOGS/lagwalk-others-gm.log" "$LOGS/lagwalk-others-player.log" || FAILED=1
}

echo "1/5 Сборка бота и временная копия сервера…"
copy_build_bot
copy_check_server
copy_make "$DATA"
python3 - "$COPY/Configuration/modernuo.json" <<'PY'
import json, sys
path = sys.argv[1]
with open(path, encoding="utf-8") as f:
    config = json.load(f)
s = config.setdefault("settings", {})
s["stamina.useMountStaminaOnlyWhenOverloaded"] = "True"
s["accountHandler.maxAccountsPerIP"] = "20"
with open(path, "w", encoding="utf-8") as f:
    json.dump(config, f, indent=2)
PY
grep -A6 '"speedhack_detection"' "$COPY/Configuration/FeatureFlags/flags.json" | grep -q '"Enabled": true' \
  || { echo "ОШИБКА: на сервере выключен флаг speedhack_detection."; exit 1; }
ok "копия сервера с пустым миром, speedhack_detection включён; файлы данных: $DATA"
copy_accounts lagwalk Laggm

echo "2/5 Сверка имеющегося эталона ClassicUO (golden/02-walk-20.tsv)…"
if OUT="$(bot --check-walk "$ROOT/golden/02-walk-20.tsv" --data "$DATA")"; then
  ok "$(echo "$OUT" | head -n 1)"
else
  fail "сверка golden/02-walk-20.tsv: $OUT"
fi

echo "3/5 Запуск копии сервера и создание GM…"
copy_start
bot --config "$TMP/gm.json" --create-character --no-packet-log --idle 1 > "$LOGS/lagwalk-init-gm.log" 2>&1 \
  || { fail "GM не вошёл в мир"; stop_server; exit 1; }
ok "сервер запущен, GM готов"

echo "4/5 Замеры движения под лагом (режим: $MODE)…"
if [ -n "$ONLY" ]; then
  LEVELS=("$ONLY")
elif [ "$MODE" = "full" ]; then
  LEVELS=(0 100 200)
else
  LEVELS=(0 100 200 400)
fi

for lvl in "${LEVELS[@]}"; do
  run_quick_level "$lvl"
  if [ "$MODE" = "full" ]; then
    run_full_level "$lvl"
  fi
done
run_others_spike

if grep -aqE "Speed hack|Movement queue overflow" "$LOGS/lagwalk-server.log"; then
  fail "сервер отметил подозрение на спидхак или переполнение очереди:"
  grep -aE "Speed hack|Movement queue overflow" "$LOGS/lagwalk-server.log" | head -n 5
else
  ok "сервер не отметил подозрений на спидхак и переполнений очереди (Logs/check/lagwalk-server.log)"
fi

echo "5/5 Остановка копии сервера и проверка сохранности данных…"
stop_server
copy_saves_check "ваши Saves не изменились" "ваши Saves изменились"
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
