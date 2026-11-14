#!/usr/bin/env bash
# Проверка чистоты проекта от чужого контента и лицензионных рисков (docs/LEGAL.md).
# Ключи: --release (перед выпуском: любой остаток — ошибка), --update-baseline (новая точка отсчёта).
set -u
cd "$(dirname "$0")/../.." || exit 1
if ! command -v python3 >/dev/null 2>&1; then
  echo "ОШИБКА: не найден python3. На Mac выполните в Терминале: xcode-select --install"
  exit 1
fi
exec python3 -I tools/check/legal.py "$@"
