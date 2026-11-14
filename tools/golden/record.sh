#!/usr/bin/env bash
# Запись эталонных сеансов ClassicUO (PLAN Стадия 0, п. 6; docs/REFERENCE-CLIENT.md §4). Запускать на Mac.
#
#   tools/golden/record.sh [сценарий ...]      (по умолчанию все 8 по порядку)
#
# Скрипт запускает временную копию вашего сервера с пустым миром (ваши сохранения не трогаются), создаёт ботом двух
# персонажей и для каждого сценария: печатает, что сделать, запускает посредника и ClassicUO (remaster/classicuo),
# ждёт, пока вы выйдете из игры, закрывает ClassicUO и переводит запись в golden/<сценарий>.tsv (пароль — звёздочками).
# В сценарии 08-death убивает и воскрешает вас GM-бот. Ваш сервер должен быть остановлен и собран.
# Для самопроверки скрипта без Mac: GOLDEN_TEST_CLIENT — команда вместо ClassicUO (в ней доступны $CLIENT_JSON —
# настройки бота с логином сценария — и $SCENARIO).
set -u
source "$(dirname "$0")/server-copy.sh"
CUO="$(cd "$ROOT/../.." && pwd)/classicuo"
GOLDEN="$ROOT/golden"
ALL=(01-login 02-walk-20 03-speech 04-drag 05-container 06-gump 07-target 08-death)
if [ $# -gt 0 ]; then SCENARIOS=("$@"); else SCENARIOS=("${ALL[@]}"); fi
CUO_PID=""
PROXY_PID=""
FAILED=()
DONE=()

cleanup() {
  [ -n "$CUO_PID" ] && kill "$CUO_PID" 2>/dev/null
  [ -n "$PROXY_PID" ] && kill "$PROXY_PID" 2>/dev/null
  copy_cleanup
}
trap cleanup EXIT

memo() {
  local login="логин $OWNER, пароль $PASSWORD, персонаж Golden"
  case "$1" in
    01-login) echo "Войдите ($login). В мире подождите 10 секунд и выйдите.";;
    02-walk-20) echo "Войдите ($login). Пройдите 20 шагов шагом, не бегом (правая кнопка мыши — идти), в разные стороны. Выйдите.";;
    03-speech) echo "Войдите ($login). Напишите в чат: hello world — и Enter. Выйдите.";;
    04-drag) echo "Войдите ($login). Откройте рюкзак (кнопка BACKPACK на кукле), перетащите золото на землю рядом с собой, потом обратно в рюкзак. Выйдите.";;
    05-container) echo "Войдите ($login). Откройте рюкзак (кнопка BACKPACK на кукле) и закройте его (правый щелчок по окну). Выйдите.";;
    06-gump) echo "Войдите ($login). На кукле нажмите HELP — откроется окно помощи сервера; закройте его правым щелчком. Выйдите.";;
    07-target) echo "Войдите ($login). Напишите в чат: [add Bandage — и Enter; появится прицел — щёлкните по земле рядом. Выйдите.";;
    08-death) echo "Войдите под ДРУГИМ логином: ${OWNER}p, пароль $PASSWORD, персонаж Goldenplayer. Просто стойте: GM-бот сам убьёт и воскресит вас. Когда снова живы — выйдите.";;
    *) echo "Свой сценарий: войдите ($login), сделайте задуманное и выйдите.";;
  esac
}

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

record() {
  local s="$1" capture="$LOGS/$1.capture" status="$LOGS/$1.status"
  local login="$OWNER"; [ "$s" = "08-death" ] && login="${OWNER}p"
  proxy_start "$capture" "$status" || return 1
  if [ -n "${GOLDEN_TEST_CLIENT:-}" ]; then
    CLIENT_JSON="$TMP/gm.json"; [ "$s" = "08-death" ] && CLIENT_JSON="$TMP/player.json"
    SCENARIO="$s"
    (eval "$GOLDEN_TEST_CLIENT" > "$LOGS/classicuo-$s.log" 2>&1) &
  else
    settings "$login"
    (cd "$CUO" && exec ./ClassicUO.bin.osx > "$LOGS/classicuo-$s.log" 2>&1) &
  fi
  CUO_PID=$!
  [ "$s" = "08-death" ] && death_helper "$status" &
  while kill -0 "$PROXY_PID" 2>/dev/null; do
    if ! kill -0 "$CUO_PID" 2>/dev/null && [ ! -f "$status" ]; then
      echo "  ClassicUO закрылся, не войдя в игру. Его вывод: Logs/golden/classicuo-$s.log"; tail -n 5 "$LOGS/classicuo-$s.log"
      kill "$PROXY_PID" 2>/dev/null; break
    fi
    sleep 1
  done
  wait "$PROXY_PID" 2>/dev/null; PROXY_PID=""
  kill "$CUO_PID" 2>/dev/null; wait "$CUO_PID" 2>/dev/null; CUO_PID=""
  [ -f "$status" ] || return 1
  if bot --convert-capture "$capture" "$GOLDEN/$s.tsv" > "$LOGS/$s.convert.log" 2>&1; then
    echo "  ОК: $(head -n 1 "$LOGS/$s.convert.log" | sed 's/ Лог: .*//') → golden/$s.tsv"
  else
    echo "  ОШИБКА перевода записи (сама запись — Logs/golden/$s.capture, её стоит прислать):"; sed 's/^/    /' "$LOGS/$s.convert.log" | head -n 8
    return 1
  fi
}

death_helper() {
  for _ in $(seq 1 600); do [ -f "$1" ] && break; sleep 0.5; done
  [ -f "$1" ] || return
  sleep 10
  echo "  GM-бот идёт к вам: [kill, затем [res…"
  if copy_kill_and_res && grep -q "Смерть 0x" "$LOGS/gm-death.log"; then
    echo "  GM-бот вас убил и воскресил. Выйдите из игры."
  else
    echo "  ОШИБКА: GM-бот не справился (Logs/golden/gm-death.log). Выйдите из игры — сценарий можно повторить."
  fi
}

if [ -z "${GOLDEN_TEST_CLIENT:-}" ]; then
  [ "$(uname)" = "Darwin" ] || { echo "ОШИБКА: запись — на Mac (ClassicUO с окном). Здесь: $(uname)."; exit 1; }
  [ -x "$CUO/ClassicUO.bin.osx" ] || { echo "ОШИБКА: нет ClassicUO в $CUO (его ставит агент)."; exit 1; }
fi
GOLDEN="${GOLDEN_OUT:-$GOLDEN}"
mkdir -p "$GOLDEN"
echo "Подготовка: временная копия сервера и персонажи (до 2 минут)…"
copy_prepare || exit 1
copy_start || exit 1
copy_characters || exit 1
echo "Готово. Аккаунты только этой копии: GM — $OWNER / $PASSWORD (Golden), игрок — ${OWNER}p / $PASSWORD (Goldenplayer)."
echo "Выходить из игры: кукла (двойной щелчок по себе) → кнопка LOG OUT. Окно ClassicUO скрипт закроет сам."
for s in "${SCENARIOS[@]}"; do
  echo
  echo "=== $s ==="
  memo "$s"
  read -r -p "Enter — запустить ClassicUO, s — пропустить, q — закончить: " answer
  case "$answer" in s) continue;; q) break;; esac
  while true; do
    if record "$s"; then DONE+=("$s"); break; fi
    read -r -p "Сценарий $s не записан. r — повторить, Enter — дальше: " again
    [ "$again" = "r" ] || { FAILED+=("$s"); break; }
  done
done

copy_stop
echo
[ "$(saves_print)" = "$SAVES_BEFORE" ] && echo "ОК: ваши сохранения не тронуты" || echo "ОШИБКА: папка Saves вашего сервера изменилась"
echo "Записано: ${DONE[*]:-ничего}"
[ ${#FAILED[@]} -gt 0 ] && echo "Не записано: ${FAILED[*]}"
echo "Логи эталонов — в golden/ (пароль в них заменён звёздочками). Напишите агенту, что запись готова."
