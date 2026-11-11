#!/usr/bin/env python3
"""Сравнивает предупреждения сборки dotnet с известными (dotnet-warnings-baseline.txt).

Использование: dotnet_warnings.py <лог сборки> [--write-baseline]
Новое предупреждение — ошибка (AGENTS.md §7: «без новых предупреждений»). Номер строки в ключ не входит,
чтобы правка соседнего кода не делала старое предупреждение «новым». Совместим с Python 3.9.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
BASELINE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "dotnet-warnings-baseline.txt")
WARNING = re.compile(r"^\s*(?P<path>.+?)(?:\(\d+,\d+(?:,\d+,\d+)?\))?\s*:\s*warning\s+(?P<code>[A-Z]+\d+)\s*:\s*(?P<msg>.*?)(?:\s+\[[^\]]+\])?\s*$")
IGNORED = {"NU1900"}  # нет доступа к базе уязвимостей NuGet — это про сеть, а не про код


def parse(log_path):
    found = set()
    for line in open(log_path, encoding="utf-8", errors="replace"):
        m = WARNING.match(line)
        if not m or m.group("code") in IGNORED:
            continue
        path = m.group("path").strip()
        if os.path.isabs(path):
            path = os.path.relpath(os.path.normpath(path), ROOT)
        found.add("%s | %s | %s" % (m.group("code"), path.replace(os.sep, "/"), m.group("msg")))
    return found


def main(argv):
    found = parse(argv[0])
    if "--write-baseline" in argv:
        with open(BASELINE, "w", encoding="utf-8") as fh:
            fh.write("# Известные предупреждения сборки dotnet (код прототипа). Формат: код | файл | текст.\n")
            fh.write("# Новые строки — только с согласия пользователя (AGENTS.md §7–§8).\n")
            fh.write("".join(line + "\n" for line in sorted(found)))
        print("baseline предупреждений записан: %d" % len(found))
        return 0
    known = set()
    if os.path.isfile(BASELINE):
        known = set(l.strip() for l in open(BASELINE, encoding="utf-8") if l.strip() and not l.startswith("#"))
    new = sorted(found - known)
    for line in new:
        print("ОШИБКА новое предупреждение: " + line)
    return 1 if new else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
