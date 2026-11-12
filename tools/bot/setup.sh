#!/usr/bin/env bash
# Настройка бота: логин, пароль, персонаж и сервер сохраняются в remaster/secrets/bot.json — вне обоих git-репозиториев,
# доступ только у владельца. Пароль вводится скрыто и не попадает в историю Терминала.
# Запуск из папки client/The Verge: tools/bot/setup.sh   (повторный запуск перезаписывает файл)
set -u
cd "$(dirname "$0")/../.." || exit 1
SECRETS="${THEVERGE_SECRETS_DIR:-$(cd ../.. && pwd)/secrets}"
FILE="$SECRETS/bot.json"

if ! command -v python3 >/dev/null 2>&1; then
  echo "ОШИБКА: не найден python3. На Mac выполните в Терминале: xcode-select --install"
  exit 1
fi

mkdir -p "$SECRETS" && chmod 700 "$SECRETS" || { echo "ОШИБКА: не удалось создать папку $SECRETS"; exit 1; }
if git -C "$SECRETS" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "ОШИБКА: папка $SECRETS внутри git-репозитория — пароль мог бы попасть в git. Файл не записан."
  exit 1
fi

read -r -p "Логин: " LOGIN
read -r -s -p "Пароль (не отображается): " PASSWORD
echo
read -r -p "Персонаж (Enter — не указывать): " CHARACTER
read -r -p "Сервер [127.0.0.1]: " HOST
read -r -p "Порт [2593]: " PORT
HOST="${HOST:-127.0.0.1}"
PORT="${PORT:-2593}"

if [ -z "$LOGIN" ] || [ -z "$PASSWORD" ]; then
  echo "ОШИБКА: логин и пароль не могут быть пустыми. Файл не записан."
  exit 1
fi
if ! [[ "$PORT" =~ ^[0-9]+$ ]] || [ "$PORT" -lt 1 ] || [ "$PORT" -gt 65535 ]; then
  echo "ОШИБКА: порт — число от 1 до 65535. Файл не записан."
  exit 1
fi

# Значения передаются через окружение, а не аргументами: аргументы видны в списке процессов.
umask 077
BOT_LOGIN="$LOGIN" BOT_PASSWORD="$PASSWORD" BOT_CHARACTER="$CHARACTER" BOT_HOST="$HOST" BOT_PORT="$PORT" \
python3 - "$FILE" <<'PY' || { echo "ОШИБКА: не удалось записать $FILE"; exit 1; }
import json, os, sys
data = {
    "host": os.environ["BOT_HOST"],
    "port": int(os.environ["BOT_PORT"]),
    "login": os.environ["BOT_LOGIN"],
    "password": os.environ["BOT_PASSWORD"],
    "character": os.environ["BOT_CHARACTER"],
}
with open(sys.argv[1], "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")
PY
chmod 600 "$FILE"
echo "Сохранено: $FILE (читать может только ваш пользователь)."
echo "Запуск бота: tools/bot/run.sh"
