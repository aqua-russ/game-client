#!/usr/bin/env python3
"""Проверка архитектуры The Verge по правилам AGENTS.md §2–§4. Запуск: tools/check/architecture.sh

Что проверяется:
  * зависимости сборок (.asmdef) и noEngineReferences, файлы csc.rsp с nullable;
  * у каждого файла и папки в Assets/TheVerge есть .meta, лишних .meta нет;
  * запрещённые API по слоям (UnityEngine в Core/Network/Data, сеть и файлы в Core, Find/SendMessage/Input
    и синглтоны в Presentation/Bootstrap, async void в чистом коде);
  * синтаксис новее C# 9; namespace = путь папки; имя файла = имя типа;
  * размеры (§3.2): строки файла, типы в файле, строки и вложенность метода, параметры, члены интерфейса.

Старые нарушения прототипа записаны в architecture-baseline.txt: они не ошибка, пока не растут.
Новое нарушение или рост старого — ошибка. Добавлять строки в baseline можно только с согласия
пользователя (AGENTS.md §8); исправленные строки убирает ключ --shrink-baseline.
Совместим с Python 3.9.
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import csharp_scan  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets", "TheVerge")
DOTNET = os.path.join(ROOT, "dotnet")
BASELINE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "architecture-baseline.txt")

PURE = ("Core", "Network", "Data")
ALLOWED_PROJECT_REFS = {
    "TheVerge.Core": [],
    "TheVerge.Network": ["TheVerge.Core"],
    "TheVerge.Data": ["TheVerge.Core"],
    "TheVerge.Presentation": ["TheVerge.Core"],
    "TheVerge.Bootstrap": ["TheVerge.Core", "TheVerge.Network", "TheVerge.Data", "TheVerge.Presentation"],
    "TheVerge.Editor": None,  # Editor может ссылаться на всё
}

LIMITS = {"file-lines": 300, "types-per-file": 1, "method-lines": 50, "nesting": 3,
          "params": 6, "ctor-params": 7, "interface-members": 10}

PATTERNS = [
    # (правило, слои, регулярка, пояснение)
    ("unity-in-pure", PURE, r"\busing\s+Unity(?:Engine|Editor)\b|\bUnity(?:Engine|Editor)\.",
     "Core/Network/Data не знают про Unity (AGENTS §2.1)"),
    ("async-void", PURE, r"\basync\s+void\b", "async void запрещён (AGENTS §3.6)"),
    ("core-network", ("Core",), r"\bSystem\.Net\b", "Core не работает с сетью: только через порты (ADR 0008)"),
    ("core-file-io", ("Core",), r"\bSystem\.IO\.File\b|\bFile\.\w+\s*\(|\bFileStream\b|\bDirectory\.\w+\s*\(",
     "Core не читает файлы: только через порты Data (ADR 0008)"),
    ("core-clock", ("Core",), r"\bDateTime\.(?:Now|UtcNow)\b", "время в Core — только через IClock (AGENTS §3.1)"),
    ("unity-find", ("Presentation", "Bootstrap"),
     r"\bGameObject\.Find\w*\s*\(|\b[Tt]ransform\.Find\s*\(|\bFind(?:Any|First)?Objects?(?:OfType|ByType)\b",
     "поиск объектов в рантайме запрещён (AGENTS §4.1)"),
    ("send-message", ("Presentation", "Bootstrap"), r"\b(?:SendMessage(?:Upwards)?|BroadcastMessage)\s*\(",
     "SendMessage запрещён (AGENTS §4.1)"),
    ("legacy-input", ("Presentation", "Bootstrap"),
     r"\bUnityEngine\.Input\b|(?<![\w.])Input\.(?:Get\w+|mousePosition|anyKey\w*|touch\w*|inputString)",
     "старый Input запрещён, только Input System (AGENTS §4.1)"),
    ("singleton", ("Presentation", "Bootstrap"), r"\bstatic\s+(?:readonly\s+)?[\w<>,.\s]*?\bInstance\b",
     "синглтоны static Instance запрещены (AGENTS §4.1)"),
    ("presentation-knows-network", ("Presentation",), r"\busing\s+TheVerge\.(?:Network|Data)\b",
     "Presentation не знает про Network и Data (AGENTS §2.1)"),
]

CSHARP_LIMIT = [
    ("csharp-version", r"(?m)^\s*namespace\s+[\w.]+\s*;", "file-scoped namespace — это C# 10, Unity 6 понимает C# 9"),
    ("csharp-version", r"(?m)^\s*global\s+using\b", "global using — это C# 10"),
    ("csharp-version", r"\brecord\s+struct\b", "record struct — это C# 10"),
    ("csharp-version", r"(?m)^\s*(?:(?:public|internal|protected|private)\s+)*required\s+\w", "required — это C# 11"),
]


class Violation(object):
    def __init__(self, rule, path, subject, value, message):
        self.rule, self.path, self.subject, self.value, self.message = rule, path, subject, value, message

    def key(self):
        return (self.rule, self.path, self.subject)


def rel(path):
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def layer_of(path):
    r = rel(path)
    parts = r.split("/")
    if r.startswith("Assets/TheVerge/") and len(parts) > 3:
        return parts[2]
    if r.startswith("dotnet/") and len(parts) > 2:
        return parts[1].replace("TheVerge.", "")
    return None


def expected_namespace(path):
    r = rel(path)
    parts = r.split("/")
    if r.startswith("Assets/TheVerge/"):
        folders = parts[2:-1]
    elif r.startswith("dotnet/"):
        folders = [parts[1].replace("TheVerge.", "")] + parts[2:-1]
    else:
        return None
    return ".".join(["TheVerge"] + folders)


# ---------- сборки ----------

def check_asmdefs(out):
    guid_to_name = {}
    asmdefs = {}
    for dirpath, _, files in os.walk(ASSETS):
        for f in files:
            if f.endswith(".asmdef"):
                path = os.path.join(dirpath, f)
                with open(path, encoding="utf-8-sig") as fh:
                    data = json.load(fh)
                asmdefs[data.get("name", f)] = (path, data)
                guid = _meta_guid(path + ".meta")
                if guid:
                    guid_to_name[guid] = data.get("name")

    for name in ALLOWED_PROJECT_REFS:
        if name not in asmdefs:
            out.append(Violation("asmdef-missing", "Assets/TheVerge", name, 1, "нет сборки " + name))

    for name, (path, data) in sorted(asmdefs.items()):
        if name not in ALLOWED_PROJECT_REFS:
            out.append(Violation("asmdef-unknown", rel(path), name, 1,
                                 "новая сборка только с ADR и согласием (AGENTS §2.1)"))
            continue
        refs = [_resolve_ref(r, guid_to_name) for r in data.get("references", [])]
        allowed = ALLOWED_PROJECT_REFS[name]
        short = name.replace("TheVerge.", "")
        for ref in refs:
            if allowed is None:
                continue
            if ref.startswith("TheVerge.") and ref not in allowed:
                out.append(Violation("asmdef-reference", rel(path), ref, 1,
                                     "%s не может ссылаться на %s (AGENTS §2.1)" % (name, ref)))
            elif not ref.startswith("TheVerge.") and short in PURE:
                out.append(Violation("asmdef-reference", rel(path), ref, 1,
                                     "в чистые сборки внешние пакеты не добавляются (AGENTS §3.1)"))
        if short in PURE:
            if data.get("noEngineReferences") is not True:
                out.append(Violation("asmdef-engine", rel(path), name, 1, "нужно noEngineReferences: true"))
            rsp = os.path.join(os.path.dirname(path), "csc.rsp")
            if not os.path.isfile(rsp) or "-nullable:enable" not in open(rsp, encoding="utf-8").read():
                out.append(Violation("nullable-rsp", rel(path), name, 1,
                                     "рядом с .asmdef нужен csc.rsp со строкой -nullable:enable (ADR 0008)"))


def _meta_guid(meta):
    if not os.path.isfile(meta):
        return None
    m = re.search(r"^guid:\s*([0-9a-f]+)", open(meta, encoding="utf-8").read(), re.M)
    return m.group(1) if m else None


def _resolve_ref(ref, guid_to_name):
    if ref.startswith("GUID:"):
        return guid_to_name.get(ref[5:], ref)
    return ref


# ---------- .meta ----------

def check_meta(out):
    def hidden(name):
        return name.startswith(".") or name.endswith("~")

    if not os.path.isfile(ASSETS + ".meta"):
        out.append(Violation("meta-missing", rel(ASSETS), "", 1, "нет .meta у папки"))
    for dirpath, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if not hidden(d)]
        names = set(dirs) | set(f for f in files if not hidden(f))
        for name in sorted(names):
            if name.endswith(".meta"):
                if name[:-5] not in names:
                    out.append(Violation("meta-orphan", rel(os.path.join(dirpath, name)), "", 1,
                                         ".meta без файла: удалите его или верните файл"))
            elif name + ".meta" not in names:
                out.append(Violation("meta-missing", rel(os.path.join(dirpath, name)), "", 1,
                                     "нет .meta: Unity создаст новый GUID и сломает ссылки (AGENTS §4.2)"))


# ---------- исходники ----------

def source_files():
    for base in (ASSETS, DOTNET):
        for dirpath, dirs, files in os.walk(base):
            dirs[:] = [d for d in dirs if d not in ("bin", "obj", "TestResults") and not d.startswith(".")]
            for f in files:
                if f.endswith(".cs"):
                    yield os.path.join(dirpath, f)


def check_source(path, out):
    with open(path, encoding="utf-8-sig") as fh:
        text = fh.read()
    clean = csharp_scan.strip_code(text)
    r = rel(path)
    layer = layer_of(path)

    for rule, layers, pattern, message in PATTERNS:
        if layer in layers:
            hits = len(re.findall(pattern, clean))
            if hits:
                out.append(Violation(rule, r, "", hits, message))
    for rule, pattern, message in CSHARP_LIMIT:
        if re.search(pattern, clean):
            out.append(Violation(rule, r, message, 1, message))

    scan = csharp_scan.scan(text)
    check_naming(path, scan, out)
    exempt = path.endswith(".g.cs") or text.lstrip("﻿").startswith("// size-exempt:")
    if not exempt:
        check_sizes(r, text, scan, out)


def check_naming(path, scan, out):
    r = rel(path)
    expected = expected_namespace(path)
    for ns in scan.namespaces:
        if expected and ns != expected:
            out.append(Violation("namespace-folder", r, ns, 1, "namespace должен быть %s (AGENTS §3.3)" % expected))
    stem = os.path.basename(path)[:-3].split(".")[0]
    if scan.top_types and stem not in scan.top_types:
        out.append(Violation("file-name", r, stem, 1, "имя файла должно совпадать с именем типа (AGENTS §3.3)"))


def check_sizes(r, text, scan, out):
    lines = text.count("\n") + (0 if text.endswith("\n") or not text else 1)
    if lines > LIMITS["file-lines"]:
        out.append(Violation("file-lines", r, "", lines, "строк в файле %d > %d" % (lines, LIMITS["file-lines"])))
    types = len(scan.top_types)
    if types > LIMITS["types-per-file"]:
        out.append(Violation("types-per-file", r, "", types, "типов верхнего уровня %d > 1" % types))
    worst = {}
    for name, length, nest, params, is_ctor in scan.methods:
        for rule, value in (("method-lines", length), ("nesting", nest),
                            ("ctor-params" if is_ctor else "params", params)):
            if value > LIMITS[rule]:
                worst[(rule, name)] = max(worst.get((rule, name), 0), value)
    for (rule, name), value in sorted(worst.items()):
        out.append(Violation(rule, r, name, value, "%s: %d > %d" % (name, value, LIMITS[rule])))
    for name, members in scan.interfaces:
        if members > LIMITS["interface-members"]:
            out.append(Violation("interface-members", r, name, members,
                                 "%s: членов %d > %d" % (name, members, LIMITS["interface-members"])))


# ---------- базовая линия ----------

def load_baseline():
    entries = {}
    if not os.path.isfile(BASELINE):
        return entries
    for line in open(BASELINE, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = [p.strip() for p in line.split("|")]
        if len(parts) == 4:
            entries[(parts[0], parts[1], parts[2])] = int(parts[3])
    return entries


def write_baseline(rows):
    """rows: список (правило, файл, что, значение)."""
    header = [
        "# Известные нарушения кода прототипа (docs/TECH-DEBT.md). Формат: правило | файл | что | значение.",
        "# Проверка не ругается на эти строки, пока значение не растёт. Новые строки — только с согласия",
        "# пользователя (AGENTS.md §8). Исправил — запусти tools/check/architecture.sh --shrink-baseline.",
        "",
    ]
    body = ["%s | %s | %s | %d" % row for row in sorted(rows)]
    with open(BASELINE, "w", encoding="utf-8") as fh:
        fh.write("\n".join(header + body) + "\n")


def main(argv):
    violations = []
    check_asmdefs(violations)
    check_meta(violations)
    for path in sorted(source_files()):
        check_source(path, violations)

    if "--write-baseline" in argv:
        write_baseline([v.key() + (v.value,) for v in violations])
        print("baseline записан: %d строк" % len(violations))
        return 0

    baseline = load_baseline()
    errors, debt, seen = [], 0, set()
    for v in violations:
        seen.add(v.key())
        known = baseline.get(v.key())
        if known is None:
            errors.append("[%s] %s: %s" % (v.rule, v.path, v.message))
        elif v.value > known:
            errors.append("[%s] %s: выросло с %d до %d — %s" % (v.rule, v.path, known, v.value, v.message))
        else:
            debt += 1
    fixed = [k for k in baseline if k not in seen]

    if "--shrink-baseline" in argv:
        current = dict((v.key(), v.value) for v in violations)
        write_baseline([k + (min(current[k], value),) for k, value in baseline.items() if k in current])
        print("baseline: убрано исправленных строк: %d" % len(fixed))
        fixed = []

    for e in errors:
        print("ОШИБКА " + e)
    if fixed:
        print("Исправлено старых нарушений: %d. Уберите их из baseline: tools/check/architecture.sh --shrink-baseline"
              % len(fixed))
    print("Архитектура: новых нарушений %d, известный долг прототипа %d." % (len(errors), debt))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
