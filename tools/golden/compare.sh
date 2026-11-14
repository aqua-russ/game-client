#!/usr/bin/env bash
# Наш бот против эталонов ClassicUO (PLAN Стадия 0, п. 6; docs/REFERENCE-CLIENT.md §5): те же сценарии на временной
# копии сервера с пустым миром, лог пакетов бота сравнивается с golden/<сценарий>.tsv по порядку пакетов каждого
# направления (tools/golden/diff.sh). Сценарии, которые бот уже умеет: 01-login, 03-speech, 05-container, 07-target,
# 08-death. Расхождения разбираются в golden/allowed-diffs.md.
#
#   tools/golden/compare.sh [--ids] [сценарий ...]
#
# Ваш сервер должен быть остановлен и собран; ваши сохранения не трогаются. Логи бота — Logs/golden/bot-*.tsv.
set -u
source "$(dirname "$0")/server-copy.sh"
GOLDEN="$ROOT/golden"
IDS=""
[ "${1:-}" = "--ids" ] && { IDS="--ids"; shift; }
ALL=(01-login 03-speech 05-container 07-target 08-death)
if [ $# -gt 0 ]; then SCENARIOS=("$@"); else SCENARIOS=("${ALL[@]}"); fi
trap copy_cleanup EXIT

run_bot() {
  local s="$1"
  case "$s" in
    01-login) bot --config "$TMP/gm.json" --idle 10;;
    03-speech) bot --config "$TMP/gm.json" --action "say:hello world" --idle 2;;
    05-container) bot --config "$TMP/gm.json" --action use:backpack --idle 2;;
    07-target) bot --config "$TMP/gm.json" --action "say:[add Bandage" --action target:here --action seen:0x0E21 --idle 2;;
    08-death)
      bot --config "$TMP/player.json" --action dead --action alive --idle 1 &
      local player=$!
      sleep 4
      copy_kill_and_res
      wait "$player";;
    *) echo "сценарий $s бот пока не умеет"; return 1;;
  esac
}

copy_prepare || exit 1
copy_start || exit 1
copy_characters || exit 1
for s in "${SCENARIOS[@]}"; do
  echo "=== $s ==="
  if [ ! -f "$GOLDEN/$s.tsv" ]; then echo "  нет эталона golden/$s.tsv — пропуск"; continue; fi
  run_bot "$s" > "$LOGS/bot-$s.out" 2>&1
  BOT_LOG="$(grep -o 'Лог пакетов: .*' "$LOGS/bot-$s.out" | head -n 1 | sed 's/Лог пакетов: //')"
  if [ -z "$BOT_LOG" ]; then echo "  ОШИБКА: бот не записал лог пакетов (Logs/golden/bot-$s.out)"; continue; fi
  cp "$BOT_LOG" "$LOGS/bot-$s.tsv"
  python3 "$ROOT/tools/golden/diff.py" "$GOLDEN/$s.tsv" "$LOGS/bot-$s.tsv" $IDS | sed 's/^/  /'
done
copy_stop
[ "$(saves_print)" = "$SAVES_BEFORE" ] && echo "ОК: ваши сохранения не тронуты" || echo "ОШИБКА: папка Saves вашего сервера изменилась"
