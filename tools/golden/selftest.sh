#!/usr/bin/env bash
# Самопроверка инструментов записи эталонов (PLAN Стадия 0, п. 6). Печатает «ОК» или «ОШИБКА».
#
#   tools/golden/selftest.sh
#
# 1) посредник (proxy.py --self-test) и сравнение логов (diff.py --self-test) без сервера;
# 2) на временной копии вашего сервера с пустым миром наш бот входит в игру через посредника, говорит и открывает
#    рюкзак; запись посредника переводится в лог пакетов (поток сервера распаковывается нашим Huffman) и сравнивается
#    с собственным логом бота — пакеты каждого направления должны совпасть.
# Ваш сервер должен быть остановлен и собран; ваши сохранения не трогаются (проверяется в конце). Около минуты.
set -u
source "$(dirname "$0")/server-copy.sh"
FAILED=0
ok() { echo "  ОК: $1"; }
fail() { echo "  ОШИБКА: $1"; FAILED=1; }
trap copy_cleanup EXIT

echo "1/3 Посредник и сравнение без сервера…"
if OUT="$(python3 "$ROOT/tools/golden/proxy.py" --self-test 2>/dev/null)"; then ok "посредник ${OUT#Самопроверка посредника: ОК }"; else fail "$OUT"; fi
if OUT="$(python3 "$ROOT/tools/golden/diff.py" --self-test)"; then ok "сравнение логов ${OUT#Самопроверка сравнения: ОК }"; else fail "$OUT"; fi

echo "2/3 Временная копия сервера и персонажи…"
copy_prepare || exit 1
copy_start || exit 1
copy_characters || exit 1
ok "копия сервера запущена, персонажи созданы"

echo "3/3 Бот через посредника…"
CAPTURE="$LOGS/selftest.capture"
proxy_start "$CAPTURE" "$LOGS/selftest.status" || exit 1
bot --config "$TMP/gm.json" --port 2594 --action "say:hello world" --action use:backpack --idle 2 > "$LOGS/selftest-bot.log" 2>&1
CODE=$?
for _ in $(seq 1 20); do kill -0 "$PROXY_PID" 2>/dev/null || break; sleep 0.5; done
kill "$PROXY_PID" 2>/dev/null; wait "$PROXY_PID" 2>/dev/null
BOT_LOG="$(grep -o 'Лог пакетов: .*' "$LOGS/selftest-bot.log" | head -n 1 | sed 's/Лог пакетов: //')"
if [ "$CODE" -eq 0 ] && grep -q "game" "$LOGS/selftest.status" 2>/dev/null; then
  ok "бот вошёл в игру и вышел через посредника (вход и игровой сервер — оба через него)"
else
  fail "бот через посредника: код $CODE. Логи: Logs/golden/selftest-bot.log, selftest.capture.proxy.log"
fi
if OUT="$(bot --convert-capture "$CAPTURE" "$LOGS/selftest.tsv")"; then
  ok "$(echo "$OUT" | head -n 1 | sed 's/ Лог: .*//')"
else
  fail "запись не переведена в лог пакетов:"; echo "$OUT" | sed 's/^/  /'
fi
if [ -n "$BOT_LOG" ] && OUT="$(python3 "$ROOT/tools/golden/diff.py" "$BOT_LOG" "$LOGS/selftest.tsv")"; then
  ok "лог бота и запись посредника совпадают: $(echo "$OUT" | tr '\n' ';' | sed 's/;$//; s/;/; /')"
else
  fail "лог бота и запись посредника расходятся:"; echo "${OUT:-нет лога пакетов бота}" | sed 's/^/  /'
fi

copy_stop
if [ "$(saves_print)" = "$SAVES_BEFORE" ]; then ok "ваши сохранения не тронуты"; else fail "папка Saves вашего сервера изменилась"; fi
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше."; exit 1; fi
