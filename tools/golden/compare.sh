#!/usr/bin/env bash
# Наш бот против эталонов ClassicUO (PLAN Стадия 0, п. 6; docs/REFERENCE-CLIENT.md §5): те же сценарии на временной
# копии сервера с пустым миром. Для каждого — проверка по сути (должна совпасть): вход до 0x55 — пакет в пакет; в
# остальных — пакеты сценария бота есть у ClassicUO в том же порядке и той же длины. Затем — весь сеанс: какие пакеты
# бота у ClassicUO не нашлись (это должны быть только расхождения из golden/allowed-diffs.md). Печатает «ОК» или
# «ОШИБКА». Сценарии, которые бот уже умеет: 01-login, 03-speech, 05-container, 07-target, 08-death.
#
#   tools/golden/compare.sh [сценарий ...]
#
# Ваш сервер должен быть остановлен и собран; ваши сохранения не трогаются. Логи бота — Logs/golden/bot-*.tsv.
set -u
source "$(dirname "$0")/server-copy.sh"
GOLDEN="$ROOT/golden"
FAILED=0
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

# Что проверяется по сути: параметры diff.py для сценария.
core() {
  case "$1" in
    01-login) echo "--until 0x55";;
    03-speech) echo "--only 0xAD,0xAE --subset";;
    05-container) echo "--only 0x06,0x24,0x3C --subset";;
    07-target) echo "--only 0xAD,0x6C,0xF3 --subset";;
    08-death) echo "--only 0x2C,0x89,0x2E,0x3C,0xF3 --subset";;
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
  if [ -z "$BOT_LOG" ]; then echo "  ОШИБКА: бот не записал лог пакетов (Logs/golden/bot-$s.out)"; FAILED=1; continue; fi
  cp "$BOT_LOG" "$LOGS/bot-$s.tsv"
  # shellcheck disable=SC2046
  if OUT="$(python3 "$ROOT/tools/golden/diff.py" "$GOLDEN/$s.tsv" "$LOGS/bot-$s.tsv" $(core "$s"))"; then
    echo "  ОК по сути ($(core "$s")): $(echo "$OUT" | tr '\n' ';' | sed 's/;$//; s/;/; /')"
  else
    echo "  ОШИБКА по сути ($(core "$s")):"; echo "$OUT" | sed 's/^/    /'; FAILED=1
  fi
  echo "  весь сеанс — пакеты бота, которых у ClassicUO нет в том же порядке (разбор — golden/allowed-diffs.md):"
  python3 "$ROOT/tools/golden/diff.py" "$GOLDEN/$s.tsv" "$LOGS/bot-$s.tsv" --subset --ids | grep "в A нет" | sed 's/^ */    /' || echo "    нет"
done
copy_stop
[ "$(saves_print)" = "$SAVES_BEFORE" ] && echo "ОК: ваши сохранения не тронуты" || { echo "ОШИБКА: папка Saves вашего сервера изменилась"; FAILED=1; }
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
