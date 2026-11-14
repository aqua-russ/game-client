# Временная копия сервера с пустым миром для записи и сравнения эталонов (tools/golden/record.sh, compare.sh).
# Подключается через «source»: задаёт ROOT, LOGS, SERVER_DIR, BOT_DLL, TMP, COPY, OWNER, PASSWORD, SERVER_PID и функции.
# Ваши сохранения, аккаунты и настройки не трогаются: сервер копируется без Saves, Logs, Backups, Archives.
# Владелец копии создаётся при первом запуске (сервер спрашивает — отвечает pty_server.py), пароль случайный.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LOGS="$ROOT/Logs/golden"
SERVER_DIR="${THEVERGE_SERVER_DIR:-$ROOT/../../server/ModernUO/Distribution}"
BOT_DLL="$ROOT/dotnet/bin/TheVerge.Bot/Debug/net10.0/TheVerge.Bot.dll"
DATA_DEFAULT="$ROOT/../../uo-data/Ultima Online Classic"
SERVER_PID=""
mkdir -p "$LOGS"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

port_open() { python3 -c "import socket,sys; s=socket.socket(); s.settimeout(1); sys.exit(s.connect_ex(('127.0.0.1',$1)))"; }
saves_print() { [ -d "$SERVER_DIR/Saves" ] && (cd "$SERVER_DIR/Saves" && find . -type f -exec ls -ln {} + 2>/dev/null | awk '{print $5, $6, $7, $8, $9}' | sort | cksum) || echo "нет"; }
bot() { dotnet "$BOT_DLL" "$@"; }

# Собрать бота, проверить, что ваш сервер остановлен и собран. Печатает ошибку и возвращает 1.
copy_prepare() {
  if ! dotnet build "$ROOT/dotnet/TheVerge.Bot" -nologo -v:q -tl:off > "$LOGS/build.log" 2>&1; then
    echo "ОШИБКА: бот не собрался. Лог: Logs/golden/build.log"; return 1
  fi
  [ -f "$SERVER_DIR/ModernUO.dll" ] || { echo "ОШИБКА: нет собранного сервера в $SERVER_DIR. Соберите: в папке server/ModernUO выполните dotnet build -c Release"; return 1; }
  if port_open 2593; then
    echo "ОШИБКА: сервер уже запущен (порт 2593 занят). Остановите его (Ctrl+C в его окне) и запустите снова."; return 1
  fi
  [ -d "$DATA_DEFAULT" ] || { echo "ОШИБКА: нет папки файлов игры $DATA_DEFAULT"; return 1; }
  SAVES_BEFORE="$(saves_print)"
}

# Запустить копию: TMP, COPY, OWNER, PASSWORD, SERVER_PID. До 90 с.
copy_start() {
  TMP="$(mktemp -d)"
  COPY="$TMP/server"
  mkdir -p "$COPY"
  rsync -a --exclude Saves --exclude Logs --exclude Backups --exclude Archives --exclude temp "$SERVER_DIR/" "$COPY/"
  python3 - "$COPY/Configuration/modernuo.json" "$DATA_DEFAULT" <<'PY'
import json, os, sys
path, data = sys.argv[1], sys.argv[2]
config = json.load(open(path))
config["dataDirectories"] = [os.path.abspath(data)]
config.setdefault("settings", {})["clientData.clientVersion"] = "7.0.95.0"
json.dump(config, open(path, "w"), indent=2)
PY
  OWNER="golden$(python3 -c 'import secrets; print(secrets.token_hex(2))')"
  PASSWORD="$(python3 -c 'import secrets; print(secrets.token_hex(4))')"
  printf '{"login":"%s","password":"%s","character":"Golden"}\n' "$OWNER" "$PASSWORD" > "$TMP/gm.json"
  printf '{"login":"%sp","password":"%s","character":"Goldenplayer"}\n' "$OWNER" "$PASSWORD" > "$TMP/player.json"
  chmod 600 "$TMP/gm.json" "$TMP/player.json"
  : > "$LOGS/server.log"
  (cd "$COPY" && exec python3 "$ROOT/tools/check/pty_server.py" --log "$LOGS/server.log" \
    --answer "owner account now=y" --answer "Input Username=$OWNER" --answer "Input Password=$PASSWORD" -- dotnet ModernUO.dll) &
  SERVER_PID=$!
  for _ in $(seq 1 90); do grep -aq "Listening: " "$LOGS/server.log" 2>/dev/null && break; kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 1; done
  if ! grep -aq "Listening: " "$LOGS/server.log"; then
    echo "ОШИБКА: копия сервера не запустилась. Лог: Logs/golden/server.log"; tail -n 15 "$LOGS/server.log"; return 1
  fi
}

# Персонажи копии ботом: Golden (владелец, GM) и Goldenplayer (обычный игрок). Где стоит игрок — PLAYER_AT «x,y,z».
copy_characters() {
  bot --config "$TMP/gm.json" --create-character --no-packet-log > "$LOGS/create-gm.log" 2>&1 || { echo "ОШИБКА: не создан персонаж GM. Лог: Logs/golden/create-gm.log"; return 1; }
  bot --config "$TMP/player.json" --create-character --no-packet-log > "$LOGS/create-player.log" 2>&1 || { echo "ОШИБКА: не создан персонаж игрока. Лог: Logs/golden/create-player.log"; return 1; }
  PLAYER_AT="$(grep -o 'персонаж 0x[0-9A-F]* ([0-9]*, [0-9]*, -*[0-9]*)' "$LOGS/create-player.log" | head -n 1 | sed 's/.*(\(.*\))/\1/; s/ //g')"
}

# GM-бот убивает и воскрешает игрока, который стоит в PLAYER_AT (сценарий 08-death).
copy_kill_and_res() {
  bot --config "$TMP/gm.json" --no-packet-log \
    --action "say:[go ${PLAYER_AT//,/ }" --action "at:${PLAYER_AT%,*}" --action seenmob:0x0190 \
    --action "say:[kill" --action target:mob:0x0190 --action deathof:0x0190 \
    --action "say:[res" --action target:mob:0x0192 --idle 1 > "$LOGS/gm-death.log" 2>&1
}

# Посредник 127.0.0.1:2594 → копия сервера, пишет запись в $1 до конца игрового сеанса; статус «game» — в $2.
# PROXY_PID. Ждёт, пока посредник начнёт слушать (без пробного подключения — оно попало бы в запись).
proxy_start() {
  rm -f "$2"
  python3 "$ROOT/tools/golden/proxy.py" --listen 2594 --capture "$1" --until-game-closed --status "$2" 2> "$1.proxy.log" &
  PROXY_PID=$!
  for _ in $(seq 1 50); do grep -q "посредник 127.0.0.1:2594" "$1.proxy.log" 2>/dev/null && return 0; sleep 0.1; done
  echo "ОШИБКА: посредник не запустился. Лог: $1.proxy.log"; return 1
}

copy_stop() {
  [ -z "$SERVER_PID" ] && return
  kill -INT "$SERVER_PID" 2>/dev/null
  for _ in $(seq 1 60); do kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 0.5; done
  if kill -0 "$SERVER_PID" 2>/dev/null; then
    pkill -9 -P "$SERVER_PID" 2>/dev/null; kill -9 "$SERVER_PID" 2>/dev/null
  fi
  wait "$SERVER_PID" 2>/dev/null
  SERVER_PID=""
}

copy_cleanup() { copy_stop; [ -n "${TMP:-}" ] && rm -rf "$TMP"; }
