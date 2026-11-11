#!/usr/bin/env python3
"""Матрица пакетов docs/protocol/packets.csv: чтение и проверка правил.

Правила и смысл столбцов — docs/protocol/packets.md. Модуль используют проверка
tools/check/packets.sh и генератор tools/gen/packet_table.py, поэтому правила записаны в одном месте.
Запуск напрямую: сначала самопроверка на заведомо испорченных примерах, затем проверка настоящего файла.
Только стандартная библиотека, совместимо с Python 3.9.
"""
import csv
import io
import os
import re
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CSV_PATH = os.path.join(ROOT, "docs", "protocol", "packets.csv")
SERVER_ROOT = os.path.normpath(os.path.join(ROOT, "..", "..", "server", "ModernUO"))

COLUMNS = ["id", "sub", "dir", "name", "length", "condition", "server", "priority", "stage", "status",
           "source", "note"]


DIRECTIONS = ("c2s", "s2c")
SERVER_VALUES = ("да", "нет", "настройка", "игнорирует")
PRIORITIES = ("P0", "P1", "P2", "P3")
STATUSES = ("нужно", "отложено", "не используется")  # от лучшего к худшему
VARIABLE = "var"

_HEX = re.compile(r"^0x[0-9A-F]{2}$")
_NAME = re.compile(r"^[A-Z][A-Za-z0-9]*$")
_LENGTH = re.compile(r"^[1-9][0-9]{0,4}$")
_STAGE = re.compile(r"^(?:[1-9]|F)(?:\.[0-9]+а?)?$")
_SOURCE = re.compile(r"^Projects/[^#]+\.cs(?:#([A-Za-z_][A-Za-z0-9_]*))?$")


def read_rows(text):
    """Строки CSV как словари с номером строки файла (line). Без проверок."""
    rows = []
    for number, fields in enumerate(csv.reader(io.StringIO(text)), start=1):
        if number == 1 or not fields:
            continue
        row = dict(zip(COLUMNS, fields))
        row["line"] = number
        row["fields"] = len(fields)
        rows.append(row)
    return rows


def sort_key(row):
    """Порядок строк: номер, направление (c2s, s2c), подкоманда (общая строка первой)."""
    sub = int(row["sub"], 16) if row["sub"] else -1
    return int(row["id"], 16), DIRECTIONS.index(row["dir"]), sub


def validate(text, server_root=None):
    """Список ошибок (пустой — файл в порядке)."""
    errors = []
    if '"' in text:
        errors.append("кавычки в packets.csv запрещены: поля пишутся без запятых и кавычек")
    header = text.split("\n", 1)[0].rstrip("\r")
    if header != ",".join(COLUMNS):
        return errors + ["заголовок должен быть: " + ",".join(COLUMNS)]
    rows = read_rows(text)
    good = []
    for row in rows:
        row_errors = _check_fields(row)
        errors.extend(row_errors)
        if not row_errors:
            good.append(row)
    errors.extend(_check_unique(good))
    errors.extend(_check_order(good))
    errors.extend(_check_wrappers(good))
    if server_root:
        errors.extend(_check_sources(good, server_root))
    return errors


def _where(row):
    return "packets.csv:%d (%s %s %s)" % (row["line"], row["id"], row["sub"] or "-", row["dir"])


def _check_fields(row):
    where = _where(row) if row["fields"] >= 3 else "packets.csv:%d" % row["line"]
    if row["fields"] != len(COLUMNS):
        return ["%s: полей %d, нужно %d" % (where, row["fields"], len(COLUMNS))]
    out = []
    for column, value in row.items():
        if isinstance(value, str) and value != value.strip():
            out.append("%s: в поле %s пробелы по краям" % (where, column))
    checks = [
        ("id", _HEX.match(row["id"]), "номер вида 0xNN (A–F заглавные)"),
        ("sub", not row["sub"] or _HEX.match(row["sub"]), "пусто или 0xNN"),
        ("dir", row["dir"] in DIRECTIONS, "c2s или s2c"),
        ("name", _NAME.match(row["name"]), "имя латиницей с заглавной буквы (PascalCase)"),
        ("length", row["length"] == VARIABLE or _LENGTH.match(row["length"]), "число байт или var"),
        ("server", row["server"] in SERVER_VALUES, "одно из: " + ", ".join(SERVER_VALUES)),
        ("priority", row["priority"] in PRIORITIES + ("-",), "P0–P3 или -"),
        ("stage", row["stage"] == "-" or _STAGE.match(row["stage"]), "стадия или стадия.задача (2.9а) или -"),
        ("status", row["status"] in STATUSES, "одно из: " + ", ".join(STATUSES)),
        ("source", row["source"] == "-" or _SOURCE.match(row["source"]), "Projects/…/Файл.cs#Метод или -"),
    ]
    for column, ok, hint in checks:
        if not ok:
            out.append("%s: %s «%s» — нужно %s" % (where, column, row[column], hint))
    if out:
        return out
    return _check_status(row, where)


def _check_status(row, where):
    out = []
    if row["server"] == "игнорирует" and row["dir"] != "c2s":
        out.append("%s: «игнорирует» бывает только у пакетов клиента (c2s)" % where)
    if row["server"] == "нет" and row["status"] != "не используется":
        out.append("%s: сервер этот пакет не шлёт и не принимает — статус «не используется»" % where)
    if row["status"] == "нужно" and (row["priority"] == "-" or row["stage"] == "-"):
        out.append("%s: у статуса «нужно» должны быть приоритет и стадия" % where)
    if row["status"] == "отложено" and row["priority"] == "-":
        out.append("%s: у статуса «отложено» нужен приоритет P0–P3" % where)
    if row["status"] == "не используется" and (row["priority"] != "-" or row["stage"] != "-"):
        out.append("%s: у статуса «не используется» приоритет и стадия — «-»" % where)
    return out


def _check_unique(rows):
    out = []
    keys, names = {}, {}
    for row in rows:
        key = (row["id"], row["sub"], row["dir"])
        if key in keys:
            out.append("%s: повтор строки %d" % (_where(row), keys[key]))
        keys.setdefault(key, row["line"])
        name = (row["dir"], row["name"])
        if name in names:
            out.append("%s: имя «%s» уже есть в строке %d (имена в одном направлении не повторяются)"
                       % (_where(row), row["name"], names[name]))
        names.setdefault(name, row["line"])
    return out


def _check_order(rows):
    out = []
    for previous, row in zip(rows, rows[1:]):
        if sort_key(row) < sort_key(previous):
            out.append("%s: нарушен порядок строк (номер, затем c2s и s2c, затем подкоманда)" % _where(row))
    return out


def _check_wrappers(rows):
    out = []
    groups = {}
    for row in rows:
        groups.setdefault((row["id"], row["dir"]), []).append(row)
    for group in groups.values():
        parents = [r for r in group if not r["sub"]]
        subs = [r for r in group if r["sub"]]
        for row in parents:
            if row["source"] == "-" and not subs:
                out.append("%s: source «-» допустим только у общей строки пакета с подкомандами" % _where(row))
        for row in subs:
            if row["source"] == "-":
                out.append("%s: source «-» допустим только у общей строки пакета с подкомандами" % _where(row))
            if row["length"] != VARIABLE:
                out.append("%s: у подкоманды длина var (подкоманда идёт после поля длины)" % _where(row))
        if not subs:
            continue
        if len(parents) != 1:
            out.append("%s: у пакета с подкомандами нет общей строки (без подкоманды)" % _where(subs[0]))
            continue
        out.extend(_check_parent(parents[0], subs))
    return out


def _check_parent(parent, subs):
    out = []
    if parent["length"] != VARIABLE:
        out.append("%s: у общей строки пакета с подкомандами длина var" % _where(parent))
    status = min((r["status"] for r in subs), key=STATUSES.index)
    if parent["status"] != status:
        out.append("%s: статус общей строки должен быть «%s» (лучший у подкоманд)" % (_where(parent), status))
    used = [r["priority"] for r in subs if r["status"] != "не используется"]
    priority = min(used) if used else "-"
    if parent["priority"] != priority:
        out.append("%s: приоритет общей строки должен быть %s (лучший у подкоманд)" % (_where(parent), priority))
    return out


def _check_sources(rows, server_root):
    out = []
    texts = {}
    for row in rows:
        match = _SOURCE.match(row["source"])
        if not match:
            continue
        path = row["source"].split("#", 1)[0]
        full = os.path.join(server_root, *path.split("/"))
        if path not in texts:
            texts[path] = open(full, encoding="utf-8-sig").read() if os.path.isfile(full) else None
        if texts[path] is None:
            out.append("%s: файл %s не найден на сервере" % (_where(row), path))
        elif match.group(1) and not re.search(r"\b%s\b" % match.group(1), texts[path]):
            out.append("%s: метод %s не найден в %s" % (_where(row), match.group(1), path))
    return out


# ---------- самопроверка ----------

_SAMPLE = """id,sub,dir,name,length,condition,server,priority,stage,status,source,note
0x02,,c2s,MoveRequest,7,,да,P0,3.5,нужно,Projects/A.cs#Move,
0x22,,s2c,MoveAck,3,,да,P0,3.6,нужно,Projects/A.cs#Ack,
0x97,,s2c,MovePlayer,2,,нет,-,-,не используется,Projects/A.cs#Ack,
0xBF,,c2s,ExtendedCommand,var,,да,P1,1.9,нужно,Projects/A.cs#Move,
0xBF,0x0B,c2s,Language,var,,да,P1,1.9,нужно,Projects/A.cs#Move,
0xBF,0x24,c2s,Unknown24,var,,игнорирует,-,-,не используется,Projects/A.cs#Move,
"""

_ROW_LANG = "0xBF,0x0B,c2s,Language,var,,да,P1,1.9,нужно,Projects/A.cs#Move,"

# (что испорчено, замена (было, стало), часть ожидаемого сообщения об ошибке)
_BROKEN = [
    ("заголовок", ("id,sub,dir", "id,dir,sub"), "заголовок"),
    ("лишнее поле", ("0x22,,s2c,MoveAck,3,,да,P0,3.6,нужно,Projects/A.cs#Ack,",
                     "0x22,,s2c,MoveAck,3,,да,P0,3.6,нужно,Projects/A.cs#Ack,,"), "полей"),
    ("короткий номер", ("0x22,,s2c", "0x2,,s2c"), "id"),
    ("строчные буквы в номере", ("0xBF,0x0B", "0xbf,0x0B"), "id"),
    ("плохая подкоманда", ("0xBF,0x0B", "0xBF,0B"), "sub"),
    ("плохое направление", ("0x22,,s2c", "0x22,,s2s"), "dir"),
    ("имя не PascalCase", ("MoveAck", "move_ack"), "name"),
    ("нулевая длина", (",MoveAck,3,", ",MoveAck,0,"), "length"),
    ("длина с ведущим нулём", (",MoveAck,3,", ",MoveAck,03,"), "length"),
    ("длина словом", (",MoveAck,3,", ",MoveAck,три,"), "length"),
    ("плохое «шлёт ли сервер»", ("MoveAck,3,,да,", "MoveAck,3,,может,"), "server"),
    ("«игнорирует» у пакета сервера", ("MovePlayer,2,,нет,-,-,не используется",
                                       "MovePlayer,2,,игнорирует,-,-,не используется"), "игнорирует"),
    ("плохой приоритет", ("MoveAck,3,,да,P0,", "MoveAck,3,,да,P5,"), "priority"),
    ("плохая стадия", ("P0,3.6,нужно", "P0,3.x,нужно"), "stage"),
    ("плохой статус", ("P0,3.6,нужно", "P0,3.6,надо"), "status"),
    ("плохой путь", ("Projects/A.cs#Ack,\n0x97", "A.cs#Ack,\n0x97"), "source"),
    ("кавычки", ("MoveAck,3,,", 'MoveAck,3,"x",'), "кавычк"),
    ("повтор строки", (_ROW_LANG, _ROW_LANG + "\n" + _ROW_LANG), "повтор"),
    ("повтор имени", ("0x22,,s2c,MoveAck", "0x22,,s2c,MovePlayer"), "имя"),
    ("сервер не шлёт, а статус «нужно»", ("MovePlayer,2,,нет,-,-,не используется",
                                          "MovePlayer,2,,нет,P1,1.2,нужно"), "сервер"),
    ("«нужно» без стадии", ("P0,3.6,нужно", "P0,-,нужно"), "стадия"),
    ("«не используется» с приоритетом", ("MovePlayer,2,,нет,-,-,", "MovePlayer,2,,нет,P3,-,"), "приоритет"),
    ("«отложено» без приоритета", ("MovePlayer,2,,нет,-,-,не используется", "MovePlayer,2,,да,-,-,отложено"),
     "приоритет"),
    ("подкоманды без общей строки", ("0xBF,,c2s,ExtendedCommand,var,,да,P1,1.9,нужно,Projects/A.cs#Move,\n", ""),
     "общей строки"),
    ("подкоманда с постоянной длиной", ("Language,var", "Language,12"), "var"),
    ("общая строка с постоянной длиной", ("ExtendedCommand,var", "ExtendedCommand,12"), "var"),
    ("статус общей строки не сходится", ("ExtendedCommand,var,,да,P1,1.9,нужно",
                                         "ExtendedCommand,var,,да,-,-,не используется"), "общей строки"),
    ("приоритет общей строки не сходится", ("ExtendedCommand,var,,да,P1", "ExtendedCommand,var,,да,P2"),
     "общей строки"),
    ("нарушен порядок", ("0x02,,c2s,MoveRequest,7,,да,P0,3.5,нужно,Projects/A.cs#Move,\n0x22",
                         "0x22"), None),
    ("«-» вместо исходника у обычной строки", ("Projects/A.cs#Ack,\n0x97", "-,\n0x97"), "source"),
    ("файла нет на сервере", ("Projects/A.cs#Ack,\n0x97", "Projects/B.cs#Ack,\n0x97"), "не найден"),
    ("метода нет в файле", ("Projects/A.cs#Ack,\n0x97", "Projects/A.cs#Nope,\n0x97"), "метод"),
]


def _broken_order(text):
    lines = text.split("\n")
    lines[1], lines[2] = lines[2], lines[1]
    return "\n".join(lines)


def self_test():
    """Проверка проверяет себя: верный пример без ошибок, каждый испорченный — с ожидаемой ошибкой."""
    problems = []
    with tempfile.TemporaryDirectory() as server:
        os.makedirs(os.path.join(server, "Projects"))
        with open(os.path.join(server, "Projects", "A.cs"), "w", encoding="utf-8") as fh:
            fh.write("class A { void Move() {} void Ack() {} }\n")
        errors = validate(_SAMPLE, server)
        if errors:
            problems.append("верный пример дал ошибки: " + "; ".join(errors))
        for what, (old, new), expected in _BROKEN:
            if expected is None:
                text, expected = _broken_order(_SAMPLE), "порядок"
            else:
                if old not in _SAMPLE:
                    problems.append("самопроверка устарела: в примере нет «%s» (%s)" % (old, what))
                    continue
                text = _SAMPLE.replace(old, new, 1)
            errors = validate(text, server)
            if not any(expected in e for e in errors):
                problems.append("не поймана ошибка «%s» (ждали «%s», получили: %s)"
                                % (what, expected, "; ".join(errors) or "ничего"))
    return problems


def main():
    problems = self_test()
    if problems:
        for p in problems:
            print("ОШИБКА самопроверки: " + p)
        return 1
    with open(CSV_PATH, encoding="utf-8") as fh:
        text = fh.read()
    server = SERVER_ROOT if os.path.isdir(SERVER_ROOT) else None
    if server is None:
        print("Внимание: не найден сервер %s — пути к исходникам не проверены." % SERVER_ROOT)
    errors = validate(text, server)
    for e in errors:
        print("ОШИБКА: " + e)
    if errors:
        return 1
    rows = read_rows(text)
    print("Матрица пакетов: строк %d, ошибок нет." % len(rows))
    return 0


if __name__ == "__main__":
    sys.exit(main())
