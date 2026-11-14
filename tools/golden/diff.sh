#!/usr/bin/env bash
# Сравнение двух логов пакетов по порядку пакетов каждого направления (номер, подкоманда, длина).
#   tools/golden/diff.sh A.tsv B.tsv [--ids] [--max N]
#   tools/golden/diff.sh --self-test
# Печатает «совпадает» или расхождения с номерами строк. Подробности — docs/REFERENCE-CLIENT.md §5.
exec python3 "$(dirname "$0")/diff.py" "$@"
