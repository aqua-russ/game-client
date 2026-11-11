#!/usr/bin/env bash
# Проверка матрицы пакетов docs/protocol/packets.csv (формат, повторы, допустимые значения, пути к
# исходникам сервера). Правила — docs/protocol/packets.md, код правил — tools/gen/packets_csv.py.
# Печатает «ОК» или список ошибок.
set -u
cd "$(dirname "$0")/../.." || exit 1

if ! command -v python3 >/dev/null 2>&1; then
  echo "ОШИБКА: не найден python3. На Mac выполните в Терминале: xcode-select --install"
  exit 1
fi

if python3 tools/gen/packets_csv.py; then
  echo "ОК"
else
  echo "ОШИБКА: матрица пакетов не прошла проверку (список выше, правила — docs/protocol/packets.md)."
  exit 1
fi
