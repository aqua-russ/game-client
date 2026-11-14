#!/usr/bin/env python3
"""Проверка «чистоты» проекта от чужого контента и лицензионных рисков (docs/LEGAL.md).

Режимы:
  (без ключей)      обычная проверка: новые остатки UO/EA и новые совпадения с кодом сервера — ошибка;
                    старые остатки из tools/check/legal-baseline.json только считаются (прогресс).
  --release         как выше, но любой остаток (клиент и сервер) — ошибка. Запускать перед выпуском.
  --update-baseline записать текущее состояние как новую «точку отсчёта» (только после осознанной замены).
Печатает «ОК» или список ошибок. Код выхода 0 — всё хорошо.
"""
import json, os, re, subprocess, sys

CLIENT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SERVER = os.path.abspath(os.path.join(CLIENT, "..", "..", "server", "ModernUO"))
BASELINE = os.path.join(CLIENT, "tools", "check", "legal-baseline.json")

# Слова, по которым узнаётся чужой контент: бренд, лор, названия городов и персонажей UO.
WORDS = [
    "Ultima", "Britannia", "Lord British", "Origin Systems", "Electronic Arts", "cliloc",
    "Trammel", "Felucca", "Blackthorn", "Mondain", "Minax", "Shadowlord", "Skara Brae", "Trinsic",
    "Moonglow", "Jhelom", "Magincia", "Nujel", "Vesper", "Minoc", "Ocllo", "Buccaneer's Den",
    "Serpent's Hold", "Empath Abbey", "Lycaeum",
]
WORD_RE = re.compile("|".join(re.escape(w) for w in WORDS), re.IGNORECASE)
UO_DATA_EXT = (".mul", ".uop", ".idx")
TEXT_EXT = (".cs", ".py", ".sh", ".md", ".json", ".csv", ".txt", ".cfg", ".xml", ".asmdef", ".unity", ".prefab", ".uxml", ".uss")
# где слова допустимы всегда: документы *.md (в них мы объясняем, что заменяем), папка docs/
CLIENT_SKIP_PREFIX = ("docs/", "Library/", "Logs/", "Temp/", "UserSettings/", "tools/check/legal")
SERVER_ROOTS = [os.path.join("Distribution", "Data"), os.path.join("Projects", "UOContent"), os.path.join("Projects", "Server")]


def git_files(repo):
    out = subprocess.run(["git", "-C", repo, "ls-files"], capture_output=True, text=True)
    return [l for l in out.stdout.splitlines() if l]


def read(path):
    try:
        with open(path, encoding="utf-8", errors="ignore") as f:
            return f.read()
    except OSError:
        return ""


def scan_words_client():
    res = {}
    for rel in git_files(CLIENT):
        if rel.startswith(CLIENT_SKIP_PREFIX) or rel.endswith((".meta", ".md")) or not rel.endswith(TEXT_EXT):
            continue
        n = len(WORD_RE.findall(read(os.path.join(CLIENT, rel))))
        if n:
            res[rel] = n
    return res


def scan_words_server():
    """Сервер: считаем по областям (сколько файлов содержит остатки и сколько всего совпадений)."""
    res = {}
    for root in SERVER_ROOTS:
        base = os.path.join(SERVER, root)
        files = hits = 0
        for dp, dn, fn in os.walk(base):
            dn[:] = [d for d in dn if d not in ("obj", "bin")]
            for name in fn:
                if not name.endswith(TEXT_EXT):
                    continue
                n = len(WORD_RE.findall(read(os.path.join(dp, name))))
                if n:
                    files += 1
                    hits += n
        res[root.replace(os.sep, "/")] = {"files": files, "hits": hits}
    return res


def tracked_uo_data():
    bad = []
    for repo, label in ((CLIENT, "клиент"), (SERVER, "сервер")):
        if os.path.isdir(repo):
            bad += [f"{label}: {f}" for f in git_files(repo) if f.lower().endswith(UO_DATA_EXT) or "cliloc" in os.path.basename(f).lower()]
    return bad


def norm(line):
    return re.sub(r"\s+", " ", line.strip())


def copied_server_code():
    """Строки кода сервера (≥70 знаков), найденные в коде клиента: признак копирования (GPL!)."""
    server_lines = set()
    for root in ("Projects/Server", "Projects/UOContent"):
        for dp, dn, fn in os.walk(os.path.join(SERVER, root)):
            dn[:] = [d for d in dn if d not in ("obj", "bin")]
            for name in fn:
                if name.endswith(".cs") and not name.endswith(".g.cs"):
                    for l in read(os.path.join(dp, name)).splitlines():
                        n = norm(l)
                        if len(n) >= 70 and not n.startswith(("//", "///", "*", "/*")):
                            server_lines.add(n)
    found = {}
    for rel in git_files(CLIENT):
        if not rel.endswith(".cs") or rel.endswith(".g.cs") or rel.startswith(("Library/", "Temp/")):
            continue
        for i, l in enumerate(read(os.path.join(CLIENT, rel)).splitlines(), 1):
            n = norm(l)
            if len(n) >= 70 and n in server_lines:
                found.setdefault(rel, []).append(i)
    return found


def licenses_registered():
    """Каждый внешний пакет клиента записан в docs/LICENSES.md или docs/LEGAL.md."""
    text = (read(os.path.join(CLIENT, "docs", "LICENSES.md")) + read(os.path.join(CLIENT, "docs", "LEGAL.md"))).lower()
    pkgs = set()
    for rel in git_files(CLIENT):
        if rel.endswith(".csproj"):
            pkgs |= set(re.findall(r'PackageReference\s+Include="([^"]+)"', read(os.path.join(CLIENT, rel))))
    manifest = os.path.join(CLIENT, "Packages", "manifest.json")
    if os.path.exists(manifest):
        try:
            deps = json.loads(read(manifest)).get("dependencies", {})
            pkgs |= {k for k in deps if not k.startswith("com.unity.")}
        except ValueError:
            pass
    return sorted(p for p in pkgs if p.lower() not in text)


def main():
    args = set(sys.argv[1:])
    release, update = "--release" in args, "--update-baseline" in args
    errors = []

    if not os.path.isdir(SERVER):
        errors.append(f"не найден сервер: {SERVER}")

    # 1. файлы данных UO не в git
    for b in tracked_uo_data():
        errors.append(f"файл данных UO в git (запрещено, docs/LICENSES.md §3): {b}")

    # 2. слова-остатки
    cur_client = scan_words_client()
    cur_server = scan_words_server() if os.path.isdir(SERVER) else {}
    base = json.loads(read(BASELINE)) if os.path.exists(BASELINE) else {"client": {}, "server": {}}
    cur_copies = {rel: len(lines) for rel, lines in copied_server_code().items()}
    accepted = base.get("copies_accepted", {})  # файл -> причина (ложное срабатывание, решено человеком)

    if update:
        with open(BASELINE, "w", encoding="utf-8") as f:
            json.dump({"client": cur_client, "server": cur_server, "copies": {k: v for k, v in cur_copies.items() if k not in accepted},
                       "copies_accepted": accepted}, f, ensure_ascii=False, indent=1, sort_keys=True)
            f.write("\n")
        print("Точка отсчёта обновлена: tools/check/legal-baseline.json")
        return 0

    for rel, n in sorted(cur_client.items()):
        old = base["client"].get(rel, 0)
        if n > old:
            errors.append(f"клиент: в {rel} стало больше слов UO/EA ({old} → {n}). Замените на свои или объясните и обновите точку отсчёта.")
        elif release:
            errors.append(f"клиент (выпуск): остаток UO/EA в {rel}: {n}")
    for area, v in sorted(cur_server.items()):
        old = base["server"].get(area, {"hits": 0})["hits"]
        if v["hits"] > old:
            errors.append(f"сервер: в {area} стало больше слов UO/EA ({old} → {v['hits']}).")
        elif release and v["hits"]:
            errors.append(f"сервер (выпуск): остаток UO/EA в {area}: {v['hits']} совпадений в {v['files']} файлах")

    # 3. копирование кода сервера в клиент (GPL)
    for rel, n in sorted(cur_copies.items()):
        if rel in accepted:
            continue
        old = base.get("copies", {}).get(rel, 0)
        if n > old:
            errors.append("клиент: в %s %d строк совпадают с кодом сервера (GPL). Перепишите своими словами." % (rel, n))
        elif release:
            errors.append("клиент (выпуск): %s — строки совпадают с кодом сервера (%d). Нужно решение: переписать или записать причину в copies_accepted." % (rel, n))
    for rel in accepted:
        if rel not in cur_copies:
            errors.append("в copies_accepted лишний файл %s (совпадений уже нет) — уберите из точки отсчёта" % rel)

    # 4. лицензии пакетов
    for p in licenses_registered():
        errors.append(f"пакет {p} не записан в docs/LICENSES.md (добавьте лицензию)")

    # 5. файлы лицензий сервера
    for f in ("LICENSE", "THIRD-PARTY-NOTICES"):
        if os.path.isdir(SERVER) and not os.path.exists(os.path.join(SERVER, f)):
            errors.append(f"в сервере нет файла {f} (удалять нельзя, GPL-3.0)")

    # отчёт
    c_files, c_hits = len(cur_client), sum(cur_client.values())
    s_hits = sum(v["hits"] for v in cur_server.values())
    parts = ", ".join("%s: %d" % (k, v["hits"]) for k, v in cur_server.items())
    print("Остатки UO/EA: клиент — %d в %d файлах; сервер — %d совпадений (%s). Цель перед выпуском — 0." % (c_hits, c_files, s_hits, parts))
    if errors:
        print("ОШИБКА:")
        for e in errors:
            print(" -", e)
        return 1
    print("ОК")
    return 0


if __name__ == "__main__":
    sys.exit(main())
