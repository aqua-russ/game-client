#!/usr/bin/env bash
# Проверка архитектуры (AGENTS.md §7): зависимости сборок, запрещённые API, размеры, .meta.
# Печатает «ОК» или список ошибок. Ключ --shrink-baseline убирает из списка долга то, что уже исправлено.
set -u
cd "$(dirname "$0")/../.." || exit 1

if ! command -v python3 >/dev/null 2>&1; then
  echo "ОШИБКА: не найден python3. На Mac выполните в Терминале: xcode-select --install"
  exit 1
fi

if python3 tools/check/architecture.py "$@"; then
  echo "ОК"
else
  echo "ОШИБКА: нарушены правила архитектуры (список выше, правила — AGENTS.md)."
  exit 1
fi
