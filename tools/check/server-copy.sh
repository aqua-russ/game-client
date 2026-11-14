# Временная копия сервера с пустым миром — общая часть проверок tools/check/*.sh, которые запускают сервер.
# Подключение из корня репозитория: CHECK=<имя проверки>; . tools/check/server-copy.sh
# Ваши сохранения, аккаунты и настройки не трогаются: сервер копируется без Saves, Logs, Backups, Archives; при выходе
# копия останавливается и удаляется. Логи — Logs/check/<имя>-build.log и <имя>-server.log.
# Задаёт ROOT, LOGS, SERVER_DIR, BOT_DLL, DATA_DEFAULT, TMP, COPY, PARITY, SERVER_PID, FAILED, SAVES_BEFORE и функции ниже.

ROOT="$(pwd)"
LOGS="$ROOT/Logs/check"
SERVER_DIR="${THEVERGE_SERVER_DIR:-$ROOT/../../server/ModernUO/Distribution}"
BOT_DLL="$ROOT/dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll"
DATA_DEFAULT="$ROOT/../../uo-data/Ultima Online Classic"
TMP="$(mktemp -d)"
COPY="$TMP/server"
PARITY="$COPY/Logs/parity"
SERVER_PID=""
FAILED=0
SAVES_BEFORE=""
mkdir -p "$LOGS"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

ok() { echo "  ОК: $1"; }
fail() { echo "  ОШИБКА: $1"; FAILED=1; }
port_open() { python3 -c "import socket,sys; s=socket.socket(); s.settimeout(1); sys.exit(s.connect_ex(('127.0.0.1',${1:-2593})))"; }
saves_print() { [ -d "$SERVER_DIR/Saves" ] && (cd "$SERVER_DIR/Saves" && find . -type f -exec ls -ln {} + 2>/dev/null | awk '{print $5, $6, $7, $8, $9}' | sort | cksum) || echo "нет"; }
bot() { dotnet "$BOT_DLL" "$@"; }

# Папка файлов данных по умолчанию (remaster/uo-data/Ultima Online Classic), если её не передали и она есть.
default_data() { [ -n "${1:-}" ] && echo "$1" || { [ -d "$DATA_DEFAULT" ] && echo "$DATA_DEFAULT"; } || true; }

# Собрать бота. Не собрался — ОШИБКА и выход.
copy_build_bot() {
  if ! dotnet build "$ROOT/dotnet/TheVerge.Bot" -nologo -v:q -tl:off > "$LOGS/$CHECK-build.log" 2>&1; then
    echo "ОШИБКА: бот не собрался. Лог: Logs/check/$CHECK-build.log"; exit 1
  fi
}

# Ваш сервер собран (и в нём есть GM-команды из аргументов, без «[») и остановлен; запоминает отпечаток папки Saves.
copy_check_server() {
  [ -f "$SERVER_DIR/ModernUO.dll" ] || { echo "ОШИБКА: нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
  local command
  for command in "$@"; do
    grep -aq "$command" "$SERVER_DIR/Assemblies/UOContent.dll" 2>/dev/null \
      || { echo "ОШИБКА: в собранном сервере нет команды [$command. Соберите заново: в папке server/ModernUO выполните dotnet build -c Release"; exit 1; }
  done
  if port_open; then
    echo "ОШИБКА: сервер уже запущен (порт 2593 занят). Остановите его (Ctrl+C в его окне) и запустите проверку снова."; exit 1
  fi
  SAVES_BEFORE="$(saves_print)"
}

# Копия сервера с пустым миром. $1 — папка файлов данных (необязательно): её путь — в dataDirectories копии.
# Версия клиента для сервера — 7.0.95.0 (TARGET.md § «Стенд»).
copy_make() {
  mkdir -p "$COPY"
  rsync -a --exclude Saves --exclude Logs --exclude Backups --exclude Archives --exclude temp "$SERVER_DIR/" "$COPY/"
  python3 - "$COPY/Configuration/modernuo.json" "${1:-}" <<'PY'
import json, os, sys
path, data = sys.argv[1], sys.argv[2]
config = json.load(open(path))
if data:
    config["dataDirectories"] = [os.path.abspath(data)]
config.setdefault("settings", {})["clientData.clientVersion"] = "7.0.95.0"
json.dump(config, open(path, "w"), indent=2)
PY
}

# Учётки копии со случайным паролем: владелец-GM ($TMP/gm.json, персонаж $2) и, если задан $3, обычный игрок
# ($TMP/player.json). $1 — начало логина.
copy_accounts() {
  OWNER="$1$(python3 -c 'import secrets; print(secrets.token_hex(3))')"
  PASSWORD="check-$(python3 -c 'import secrets; print(secrets.token_hex(6))')"
  printf '{"login":"%s","password":"%s","character":"%s"}\n' "$OWNER" "$PASSWORD" "$2" > "$TMP/gm.json"
  chmod 600 "$TMP/gm.json"
  if [ -n "${3:-}" ]; then
    printf '{"login":"%sp","password":"%s","character":"%s"}\n' "$OWNER" "$PASSWORD" "$3" > "$TMP/player.json"
    chmod 600 "$TMP/player.json"
  fi
}

# Запустить копию (до 90 с): владельца создаёт pty_server.py, отвечая на вопросы сервера. Не запустилась — ОШИБКА и выход.
copy_start() {
  : > "$LOGS/$CHECK-server.log"
  (cd "$COPY" && exec python3 "$ROOT/tools/check/pty_server.py" --log "$LOGS/$CHECK-server.log" \
    --answer "owner account now=y" --answer "Input Username=$OWNER" --answer "Input Password=$PASSWORD" -- dotnet ModernUO.dll) &
  SERVER_PID=$!
  for _ in $(seq 1 90); do grep -aq "Listening: " "$LOGS/$CHECK-server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
  if ! grep -aq "Listening: " "$LOGS/$CHECK-server.log"; then
    echo "ОШИБКА: копия сервера не запустилась. Лог: Logs/check/$CHECK-server.log"; tail -n 15 "$LOGS/$CHECK-server.log"; exit 1
  fi
}

stop_server() {
  [ -z "$SERVER_PID" ] && return
  kill -INT "$SERVER_PID" 2>/dev/null
  for _ in $(seq 1 60); do kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 0.5; done
  if kill -0 "$SERVER_PID" 2>/dev/null; then
    echo "  ВНИМАНИЕ: копия сервера не остановилась сама за 30 с — остановлена принудительно (ваши данные не затронуты)."
    pkill -9 -P "$SERVER_PID" 2>/dev/null; kill -9 "$SERVER_PID" 2>/dev/null
  fi
  wait "$SERVER_PID" 2>/dev/null
  SERVER_PID=""
}

# Папка Saves вашего сервера не изменилась за время проверки: $1 — текст «ОК», $2 — текст ошибки.
copy_saves_check() { if [ "$(saves_print)" = "$SAVES_BEFORE" ]; then ok "$1"; else fail "$2"; fi; }

cleanup() { stop_server; rm -rf "$TMP"; }
trap cleanup EXIT
