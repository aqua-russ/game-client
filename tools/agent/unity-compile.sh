#!/usr/bin/env bash
# Компиляция Unity-сборок (Presentation, Bootstrap) в среде агента настоящими библиотеками Unity, без запуска редактора.
# Печатает «ОК» или «ОШИБКА: …». На Mac пользователя вместо этого — tools/check/unity.sh (сам Unity в batchmode).
#
#   tools/agent/unity-compile.sh
#
# Нужно: (1) к сессии агента подключена папка редактора Scripting — она видна как ~/mnt/Scripting (путь на Mac:
# /Applications/Unity/Hub/Editor/<версия>/Unity.app/Contents/Resources/Scripting; другой путь — переменная UNITY_SCRIPTING);
# (2) проект хоть раз открывали в Unity — нужны его файлы сборки (Library/Bee/artifacts/*/TheVerge.*.rsp) и пакеты;
# (3) .NET из tools/agent/setup-env.sh. Берётся тот же список ссылок и define, что у Unity, но свежие Core, Network и
# Data собираются dotnet, а исходники — текущие из Assets/. Анализаторы и генераторы Unity не запускаются, поэтому
# окончательное слово — за tools/check/unity.sh на Mac. Результат — в /tmp, проект не меняется.
set -u
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
SCRIPTING="${UNITY_SCRIPTING:-$HOME/mnt/Scripting}"
ENV_SH="$ROOT/../../.agent-cache/env.sh"

[ -d "$SCRIPTING/Managed/UnityEngine" ] || { echo "ОШИБКА: нет папки Scripting редактора Unity ($SCRIPTING). Подключите её к сессии (см. начало скрипта)."; exit 1; }
[ -f "$ENV_SH" ] && . "$ENV_SH"
OUT="${TMPDIR:-/tmp}/theverge-unity-compile"
command -v dotnet >/dev/null || { echo "ОШИБКА: нет dotnet. Запустите tools/agent/setup-env.sh"; exit 1; }
CSC="$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | tail -n 1)"
[ -n "$CSC" ] || { echo "ОШИБКА: в .NET SDK нет компилятора csc.dll"; exit 1; }
DAG="$(ls -d Library/Bee/artifacts/*.dag 2>/dev/null | while read -r d; do [ -f "$d/TheVerge.Bootstrap.rsp" ] && echo "$d"; done | head -n 1)"
[ -n "$DAG" ] || { echo "ОШИБКА: нет файлов сборки Unity (Library/Bee/artifacts/*/TheVerge.Bootstrap.rsp). Откройте проект в Unity один раз."; exit 1; }

echo "1/3 Сборка Core, Network, Data (dotnet)…"
for p in Core Network Data; do
  dotnet build "dotnet/TheVerge.$p.csproj" -v q -nologo > "$OUT.$p.log" 2>&1 \
    || { echo "ОШИБКА: dotnet build TheVerge.$p — см. $OUT.$p.log"; tail -n 20 "$OUT.$p.log"; exit 1; }
done
mkdir -p "$OUT"

# rsp Unity → rsp для этой машины: пути редактора — на подключённую папку, свои сборки — свежие, исходники — текущие.
make_rsp() {
  local asm="$1" rsp="$OUT/$1.rsp"
  python3 - "$DAG/TheVerge.$asm.rsp" "$rsp" "$asm" "$SCRIPTING" "$ROOT" "$OUT" <<'PY'
import os, re, sys
src, dst, asm, scripting, root, out = sys.argv[1:]
unity_scripting = re.compile(r'/Applications/Unity/Hub/Editor/[^/]+/Unity\.app/Contents/Resources/Scripting')
fresh = {"Core": f"{root}/dotnet/bin/TheVerge.Core/Debug/netstandard2.1/TheVerge.Core.dll",
         "Network": f"{root}/dotnet/bin/TheVerge.Network/Debug/netstandard2.1/TheVerge.Network.dll",
         "Data": f"{root}/dotnet/bin/TheVerge.Data/Debug/netstandard2.1/TheVerge.Data.dll",
         "Presentation": f"{out}/TheVerge.Presentation.dll"}
lines = []
for line in open(src, encoding="utf-8").read().splitlines():
    if line.startswith('"') or line.startswith(("-out:", "-refout:", "-analyzer:", "/additionalfile:")):
        continue
    m = re.match(r'-r:"Library/Bee/artifacts/[^"]*/TheVerge\.(\w+)\.ref\.dll"', line)
    if m:
        line = f'-r:"{fresh[m.group(1)]}"'
    elif line.startswith('-r:"/Applications/'):
        if not unity_scripting.search(line):
            continue  # расширения редактора для платформ сборки — рантайм-сборкам не нужны
        line = unity_scripting.sub(scripting, line)
    elif line.startswith('-r:"Library/'):
        line = '-r:"' + os.path.join(root, line[4:-1]) + '"'
    lines.append(line)
lines.append(f'-out:"{out}/TheVerge.{asm}.dll"')
base = f"{root}/Assets/TheVerge/{asm}"
for d, _, files in sorted(os.walk(base)):
    for f in sorted(files):
        if f.endswith(".cs"):
            lines.append('"' + os.path.join(d, f) + '"')
open(dst, "w", encoding="utf-8").write("\n".join(lines) + "\n")
PY
}

compile() {
  local asm="$1"
  make_rsp "$asm"
  if dotnet "$CSC" "@$OUT/$asm.rsp" > "$OUT/$asm.log" 2>&1; then
    local warnings; warnings="$(grep -c "warning CS" "$OUT/$asm.log")"
    echo "  ОК: TheVerge.$asm (предупреждений: $warnings)"
    grep "warning CS" "$OUT/$asm.log" | sed "s|$ROOT/||" | head -n 20
  else
    echo "  ОШИБКА: TheVerge.$asm не компилируется:"
    grep -E "error CS" "$OUT/$asm.log" | sed "s|$ROOT/||" | head -n 40
    FAILED=1
  fi
}

FAILED=0
echo "2/3 TheVerge.Presentation…"
compile Presentation
if [ "$FAILED" -eq 0 ]; then
  echo "3/3 TheVerge.Bootstrap…"
  compile Bootstrap
fi
if [ "$FAILED" -eq 0 ]; then echo "ОК"; else echo "ОШИБКА: см. сообщения выше (журналы — $OUT/*.log)."; exit 1; fi
