#!/usr/bin/env bash
# Запись маршрута walk-route и ходьбы под лагом в ClassicUO через посредника с задержкой (PLAN Стадия 3, п. 12).
# Запускать на Mac. Ваши Saves не трогаются.
#
#   tools/golden/record-lag.sh [walk-route | walk-lag-100 | walk-lag-200]
#
# По умолчанию запускает все 3 сценария (~15 минут вашего участия):
#   1) walk-route    (0 мс): маршрут по Британии от Sweet Dreams Inn (1496,1628,10) к дому (1469,1557,30), вверх по
#                            лестнице (1474,1557,50) и вниз обратно;
#   2) walk-lag-100  (50±15 мс в каждую сторону = пинг 100±30 мс): поле (1130,1577), бег, шаг и бег верхом;
#   3) walk-lag-200  (100±15 мс в каждую сторону = пинг 200±30 мс): то же при пинге 200±30 мс.
# После каждого сценария запись переводится в Logs/golden/cuo-<сценарий>.tsv и сразу сверяется с картой (--check-walk).
set -u
source "$(dirname "$0")/server-copy.sh"
CUO="$(cd "$ROOT/../.." && pwd)/classicuo"
ALL=(walk-route walk-lag-100 walk-lag-200)
if [ $# -gt 0 ]; then SCENARIOS=("$@"); else SCENARIOS=("${ALL[@]}"); fi
CUO_PID=""
PROXY_PID=""
FAILED=0

cleanup() {
  [ -n "$CUO_PID" ] && kill "$CUO_PID" 2>/dev/null
  [ -n "$PROXY_PID" ] && kill "$PROXY_PID" 2>/dev/null
  copy_cleanup
}
trap cleanup EXIT

settings() {
  python3 - "$CUO/settings.json" "$1" "$(cd "$DATA_DEFAULT" && pwd)" <<'PY'
import json, sys
path, login, data = sys.argv[1], sys.argv[2], sys.argv[3]
json.dump({"username": login, "password": "", "ip": "127.0.0.1", "port": 2594, "ultimaonlinedirectory": data,
           "clientversion": "7.0.95.0", "encryption": 0, "plugins": [], "saveaccount": False, "autologin": False,
           "reconnect": False, "login_music": False, "lastservernum": 1, "is_win_maximized": False},
          open(path, "w"), indent=2)
PY
}

lag_for() {
  case "$1" in
    walk-route) echo "0,0" ;;
    walk-lag-100) echo "50,15" ;;
    walk-lag-200) echo "100,15" ;;
    *) echo "0,0" ;;
  esac
}

place_helper() {
  local s="$1" status="$2" start="$3"
  for _ in $(seq 1 600); do [ -f "$status" ] && break; sleep 0.5; done
  [ -f "$status" ] || return
  sleep 2
  if [ "$s" = "walk-route" ]; then
    bot --config "$TMP/gm.json" --no-packet-log \
      --action "say:[go ${start//,/ }" --action "at:${start%,*}" --action seenmob:0x0190 \
      --action "say:[set X 1496 Y 1628 Z 10" --action target:mob:0x0190 --idle 1 > "$LOGS/gm-place-$s.log" 2>&1
    echo "  GM-бот перенёс вас к старту маршрута в Британию (1496, 1628, 10). Идите к дому (1469, 1557) и на лестницу."
  else
    bot --config "$TMP/gm.json" --no-packet-log \
      --action "say:[go ${start//,/ }" --action "at:${start%,*}" --action seenmob:0x0190 \
      --action "say:[AddToPack EtherealHorse" --action target:mob:0x0190 \
      --action "say:[set X 1130 Y 1577 Z 0" --action target:mob:0x0190 --idle 1 > "$LOGS/gm-place-$s.log" 2>&1
    echo "  GM-бот дал вам коня и перенёс на ровное поле (1130, 1577, 0)."
  fi
}

memo() {
  local login="$1" char="$2" s="$3"
  echo "Войдите: логин $login, пароль $PASSWORD, персонаж $char (после входа GM-бот сам перенесёт вас на место)."
  case "$s" in
    walk-route)
      echo "Затем пройдите/пробегите на северо-запад по улице до дома (1469, 1557), поднимитесь по деревянной лестнице"
      echo "на второй этаж (1474, 1557), спуститесь обратно к подножию лестницы и выйдите через куклу (LOG OUT)." ;;
    walk-lag-100|walk-lag-200)
      echo "Затем пробегите ~30 шагов (в том числе с поворотами), пройдите шагом ~20, откройте рюкзак и дважды щёлкните"
      echo "по эфирному коню, пробегите верхом ~40 шагов и выйдите через куклу (LOG OUT)." ;;
  esac
}

create_scenario_player() {
  local tag="$1" char="Cuo$1" cfg="$TMP/cuo-$1.json"
  printf '{"login":"%s_%s","password":"%s","character":"%s"}\n' "$OWNER" "$tag" "$PASSWORD" "$char" > "$cfg"
  chmod 600 "$cfg"
  bot --config "$cfg" --create-character --no-packet-log --idle 1 > "$LOGS/create-$tag.log" 2>&1 || return 1
  grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/create-$tag.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g'
}

record_one() {
  local s="$1" login="$2" start="$3" capture="$LOGS/cuo-$s.capture" status="$LOGS/cuo-$s.status" tsv="$LOGS/cuo-$s.tsv"
  local lag
  lag="$(lag_for "$s")"
  rm -f "$status"
  python3 "$ROOT/tools/golden/proxy.py" --listen 2594 --lag "$lag" --capture "$capture" --until-game-closed --status "$status" \
    2> "$capture.proxy.log" &
  PROXY_PID=$!
  for _ in $(seq 1 50); do grep -q "посредник 127.0.0.1:2594" "$capture.proxy.log" 2>/dev/null && break; sleep 0.1; done
  settings "$login"
  (cd "$CUO" && exec ./ClassicUO.bin.osx > "$LOGS/classicuo-$s.log" 2>&1) &
  CUO_PID=$!
  place_helper "$s" "$status" "$start" &
  while kill -0 "$PROXY_PID" 2>/dev/null; do
    if ! kill -0 "$CUO_PID" 2>/dev/null && [ ! -f "$status" ]; then
      echo "  ClassicUO закрылся до входа в мир. Лог: Logs/golden/classicuo-$s.log"
      kill "$PROXY_PID" 2>/dev/null; break
    fi
    sleep 1
  done
  wait "$PROXY_PID" 2>/dev/null; PROXY_PID=""
  kill "$CUO_PID" 2>/dev/null; wait "$CUO_PID" 2>/dev/null; CUO_PID=""
  [ -f "$status" ] || return 1
  bot --convert-capture "$capture" "$tsv" > "$LOGS/cuo-$s.convert.log" 2>&1 || return 1
  echo "  Запись: $tsv"
  bot --check-walk "$tsv" --data "$DATA_DEFAULT" | sed 's/^/  /'
  python3 "$ROOT/tools/check/walk_tempo.py" "$tsv" | sed 's/^/  /' || true
}

[ "$(uname)" = "Darwin" ] || { echo "ОШИБКА: ClassicUO с окном запускается на Mac."; exit 1; }
[ -x "$CUO/ClassicUO.bin.osx" ] || { echo "ОШИБКА: нет ClassicUO в $CUO"; exit 1; }
copy_prepare || exit 1
copy_start || exit 1
bot --config "$TMP/gm.json" --create-character --no-packet-log --idle 1 > "$LOGS/create-gm.log" 2>&1 \
  || { echo "ОШИБКА: не создан GM"; exit 1; }
grep -A6 '"speedhack_detection"' "$COPY/Configuration/FeatureFlags/flags.json" | grep -q '"Enabled": true' \
  || echo "ВНИМАНИЕ: speedhack_detection выключен"

tags=(wa wb wc)
idx=0
for s in "${SCENARIOS[@]}"; do
  tag="${tags[$idx]:-wx}"
  idx=$((idx + 1))
  start="$(create_scenario_player "$tag")" || { echo "ОШИБКА: не создан персонаж для $s"; FAILED=1; continue; }
  echo
  echo "=== $s (посредник: --lag $(lag_for "$s")) ==="
  memo "${OWNER}_$tag" "Cuo$tag" "$s"
  read -r -p "Enter — запустить ClassicUO, s — пропустить, q — закончить: " answer
  case "$answer" in s) continue ;; q) break ;; esac
  record_one "$s" "${OWNER}_$tag" "$start" || FAILED=1
done

if grep -aqE "Speed hack|Movement queue overflow" "$LOGS/server.log"; then
  echo "ВНИМАНИЕ — сервер отметил спидхак у ClassicUO:"
  grep -aE "Speed hack|Movement queue overflow" "$LOGS/server.log" | head -n 5
fi
copy_stop
[ "$(saves_print)" = "$SAVES_BEFORE" ] && echo "ОК: ваши сохранения не тронуты" || { echo "ОШИБКА: Saves изменились"; FAILED=1; }
exit "$FAILED"
