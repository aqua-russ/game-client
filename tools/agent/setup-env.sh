#!/usr/bin/env bash
# Среда АГЕНТА (Linux-машина на Mac): .NET SDK и кэш NuGet — один раз на диск, а не заново в каждой сессии.
# Вам на Mac этот скрипт не нужен: там свой .NET, проверки `tools/check/` работают как раньше.
# Что делает: проверяет `remaster/.agent-cache/` (вне git); если SDK там нет — скачивает с builds.dotnet.microsoft.com
# (с проверкой контрольной суммы) и распаковывает; пишет `remaster/.agent-cache/env.sh` с нужными переменными.
# Использование (агент, в начале сессии):  tools/agent/setup-env.sh && . ../../.agent-cache/env.sh
# Ключи: --check — только проверить, ничего не скачивать. Переменные: AGENT_CACHE (папка кэша), DOTNET_SDK_VERSION.
# Папку `.agent-cache` можно удалить в любой момент: следующий запуск скачает всё заново (около 1 ГБ).
set -eu
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
CACHE="${AGENT_CACHE:-$(cd "$ROOT/../.." && pwd)/.agent-cache}"
VER="${DOTNET_SDK_VERSION:-10.0.201}"
CHECK_ONLY=0
[ "${1:-}" = "--check" ] && CHECK_ONLY=1

case "$(uname -m)" in
  aarch64|arm64) RID="linux-arm64" ;;
  x86_64) RID="linux-x64" ;;
  *) echo "ОШИБКА: неизвестная архитектура $(uname -m)"; exit 1 ;;
esac
SDK_DIR="$CACHE/dotnet-$VER-$RID"

sdk_ok() { [ -x "$SDK_DIR/dotnet" ] && [ "$("$SDK_DIR/dotnet" --version 2>/dev/null)" = "$VER" ]; }

lookup() { # адрес и SHA-512 архива SDK из официального списка выпусков
  python3 - "$VER" "$RID" <<'PY'
import json, sys, urllib.request
ver, rid = sys.argv[1:3]
url = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/%s/releases.json" % ".".join(ver.split(".")[:2])
data = json.load(urllib.request.urlopen(url, timeout=30))
for r in data["releases"]:
    for s in [r.get("sdk") or {}] + (r.get("sdks") or []):
        if s.get("version") == ver:
            for f in s.get("files", []):
                if f.get("rid") == rid and f.get("name", "").endswith(".tar.gz"):
                    print(f["url"], f["hash"]); sys.exit(0)
sys.exit(1)
PY
}

if sdk_ok; then
  echo "  ОК: .NET SDK $VER уже в кэше ($SDK_DIR)"
elif [ "$CHECK_ONLY" -eq 1 ]; then
  echo "ОШИБКА: .NET SDK $VER нет в кэше. Запустите tools/agent/setup-env.sh без --check."; exit 1
else
  echo "Скачиваю .NET SDK $VER ($RID) в кэш — один раз…"
  mkdir -p "$CACHE"
  ARCHIVE="$CACHE/dotnet-sdk-$VER-$RID.tar.gz"
  if [ ! -f "$ARCHIVE" ]; then # архив, прошедший проверку, остаётся — если распаковка оборвётся, он не качается заново
    if INFO="$(lookup 2>/dev/null)"; then
      URL="${INFO% *}"; SHA="${INFO#* }"
    else
      URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/$VER/dotnet-sdk-$VER-$RID.tar.gz"; SHA=""
      echo "  ВНИМАНИЕ: список выпусков недоступен — контрольная сумма не проверена."
    fi
    curl -fsSL --retry 3 -o "$ARCHIVE.partial" "$URL" || { echo "ОШИБКА: не скачался $URL"; exit 1; }
    if [ -n "$SHA" ]; then
      [ "$(sha512sum "$ARCHIVE.partial" | cut -d' ' -f1)" = "$SHA" ] || { echo "ОШИБКА: контрольная сумма SDK не совпала"; rm -f "$ARCHIVE.partial"; exit 1; }
      echo "  ОК: контрольная сумма SDK совпала"
    fi
    mv "$ARCHIVE.partial" "$ARCHIVE"
  fi
  echo "  Распаковываю (на диске Mac это 1–3 минуты)…"
  rm -rf "$SDK_DIR.partial"; mkdir -p "$SDK_DIR.partial"
  tar -xzf "$ARCHIVE" -C "$SDK_DIR.partial"
  rm -rf "$SDK_DIR"; mv "$SDK_DIR.partial" "$SDK_DIR"; rm -f "$ARCHIVE"
  sdk_ok || { echo "ОШИБКА: SDK распакован, но dotnet --version не $VER"; exit 1; }
  echo "  ОК: .NET SDK $VER установлен в кэш"
fi

mkdir -p "$CACHE/nuget"
# Пути в env.sh считаются от места самого файла: путь к папке remaster в каждой сессии свой.
cat > "$CACHE/env.sh" <<'ENV'
# Создано tools/agent/setup-env.sh. Подключение: . <путь к этому файлу>
AGENT_CACHE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_ROOT="$AGENT_CACHE_DIR/@SDK@"
export PATH="$DOTNET_ROOT:$PATH"
export NUGET_PACKAGES="$AGENT_CACHE_DIR/nuget"
export TMPDIR="/tmp/theverge-agent-$(id -u)/t" DOTNET_CLI_HOME="/tmp/theverge-agent-$(id -u)/dnhome"
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
mkdir -p "$TMPDIR" "$DOTNET_CLI_HOME"
ENV
sed -i "s|@SDK@|dotnet-$VER-$RID|" "$CACHE/env.sh"
echo "  ОК: переменные записаны в $CACHE/env.sh"
echo "ОК. Подключить в этой оболочке: . \"$CACHE/env.sh\""
