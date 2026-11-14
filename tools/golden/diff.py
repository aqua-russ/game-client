#!/usr/bin/env python3
"""Сравнение двух логов пакетов (PLAN Стадия 0, п. 6; docs/REFERENCE-CLIENT.md §5, шаг 1).

    diff.py A.tsv B.tsv [--ids] [--max N] [--only СПИСОК] [--until ПАКЕТ] [--subset]
    diff.py --self-test

Сравнивается последовательность «номер пакета, подкоманда, длина» — отдельно для пакетов клиента (C>S) и сервера (S>C):
как пакеты двух направлений перемежаются, зависит от времени и сравнению не подлежит. --ids — без длины (тексты речи
и имена у разных клиентов разной длины). --only 0xAD,0xBF/0x000B — сравнивать только эти пакеты. --until 0x55 —
только до первого такого пакета сервера включительно (в каждом логе). --subset — B должен быть частью A: те же
пакеты в том же порядке, а лишние пакеты A (действия интерфейса эталонного клиента) не считаются расхождением.
Печатает «совпадает» или расхождения с номерами строк; код выхода 0 — совпадает, 1 — нет, 2 — файл не прочитать.
"""
import difflib
import os
import sys
import tempfile

DIRECTIONS = ("C>S", "S>C")


def read_log(path, with_length=True, only=None, until=None):
    """Пакеты лога по направлениям: [(ключ, номер строки, описание)]."""
    packets = {d: [] for d in DIRECTIONS}
    with open(path, encoding="utf-8") as f:
        for number, line in enumerate(f, 1):
            if line.startswith("#"):
                continue
            cols = line.rstrip("\n").split("\t")
            if len(cols) < 6 or cols[1] not in DIRECTIONS:
                continue
            pid, sub, name, length = cols[2], cols[3], cols[4], cols[5]
            name_key = pid + ("/" + sub if sub != "-" else "")
            if only is None or name_key in only or pid in only:
                key = (pid, sub, length) if with_length else (pid, sub)
                packets[cols[1]].append((key, number, name_key + " " + name + " (" + length + " байт)"))
            if until is not None and cols[1] == "S>C" and name_key == until:
                break
    return packets


def subset_problems(a, b, max_listed=20):
    """Пакеты B, которых нет в A в том же порядке (A может содержать лишние); ненайденный пакет пропускается."""
    problems = []
    i = 0
    for packet in b:
        j = i
        while j < len(a) and a[j][0] != packet[0]:
            j += 1
        if j == len(a):
            problems.append("в A нет (или не в том порядке): B строка %d [%s]" % (packet[1], packet[2]))
            continue
        i = j + 1
    if len(problems) > max_listed:
        problems = problems[:max_listed] + ["… ещё: %d" % (len(problems) - max_listed)]
    return problems


def compare(a, b, max_listed=20):
    """Список строк расхождений одного направления; пусто — совпадает."""
    matcher = difflib.SequenceMatcher(a=[p[0] for p in a], b=[p[0] for p in b], autojunk=False)
    lines = []
    for op, a1, a2, b1, b2 in matcher.get_opcodes():
        if op == "equal":
            continue
        where_a = "A строки %s" % ", ".join(str(p[1]) for p in a[a1:a2][:3]) if a2 > a1 else "A —"
        where_b = "B строки %s" % ", ".join(str(p[1]) for p in b[b1:b2][:3]) if b2 > b1 else "B —"
        what_a = "; ".join(p[2] for p in a[a1:a2][:3]) + (" …" if a2 - a1 > 3 else "")
        what_b = "; ".join(p[2] for p in b[b1:b2][:3]) + (" …" if b2 - b1 > 3 else "")
        kind = {"replace": "разные", "delete": "только в A", "insert": "только в B"}[op]
        lines.append("%s: пакет %d: %s [%s] | %s [%s]" % (kind, a1 + 1, where_a, what_a or "—", where_b, what_b or "—"))
    if len(lines) > max_listed:
        lines = lines[:max_listed] + ["… ещё расхождений: %d" % (len(lines) - max_listed)]
    return lines


def run(path_a, path_b, with_length, max_listed, out=sys.stdout, only=None, until=None, subset=False):
    try:
        a = read_log(path_a, with_length, only, until)
        b = read_log(path_b, with_length, only, until)
    except OSError as e:
        print("ОШИБКА: не прочитать лог: %s" % e, file=out)
        return 2
    same = True
    for d in DIRECTIONS:
        lines = subset_problems(a[d], b[d], max_listed) if subset else compare(a[d], b[d], max_listed)
        if not lines:
            what = "все %d пакетов B есть в A по порядку (в A %d)" % (len(b[d]), len(a[d])) if subset else "совпадает (пакетов %d)" % len(a[d])
            print("%s: %s" % (d, what), file=out)
            continue
        same = False
        print("%s: расхождений %d (в A пакетов %d, в B %d)" % (d, len(lines), len(a[d]), len(b[d])), file=out)
        for line in lines:
            print("  " + line, file=out)
    return 0 if same else 1


def self_test():
    head = "#TheVerge packet log v1\n"
    rows = [
        "0\tC>S\t0xEF\t-\tLoginSeed\t21\t-\tEF",
        "5\tC>S\t0x80\t-\tAccountLogin\t62\tmasked\t80",
        "9\tS>C\t0xA8\t-\tServerList\t46\t-\tA8",
        "12\tC>S\t0xA0\t-\tSelectServer\t3\t-\tA0",
        "15\tS>C\t0x8C\t-\tServerRedirect\t11\t-\t8C",
        "20\tC>S\t0xBF\t0x0F\tClientInfo\t9\t-\tBF",
    ]
    folder = tempfile.mkdtemp()

    def write(name, lines):
        path = os.path.join(folder, name)
        with open(path, "w", encoding="utf-8") as f:
            f.write(head + "\n".join(lines) + "\n")
        return path

    base = write("a.tsv", rows)
    reordered = rows[:2] + [rows[3], rows[2]] + rows[4:]
    cases = [
        ("тот же лог", write("same.tsv", rows), True, 0),
        ("другой порядок направлений", write("order.tsv", reordered), True, 0),
        ("убран пакет", write("removed.tsv", rows[:3] + rows[4:]), True, 1),
        ("лишний пакет", write("extra.tsv", rows + ["30\tS>C\t0x1D\t-\tRemoveEntity\t5\t-\t1D"]), True, 1),
        ("другая длина", write("length.tsv", rows[:2] + ["9\tS>C\t0xA8\t-\tServerList\t52\t-\tA8"] + rows[3:]), True, 1),
        ("другая длина без --ids", write("length2.tsv", rows[:2] + ["9\tS>C\t0xA8\t-\tServerList\t52\t-\tA8"] + rows[3:]), False, 0),
        ("другая подкоманда", write("sub.tsv", rows[:5] + ["20\tC>S\t0xBF\t0x24\tUnknown\t9\t-\tBF"]), True, 1),
    ]
    failed = []
    for name, path, with_length, expected in cases:
        sink = open(os.devnull, "w")
        code = run(base, path, with_length, 20, sink)
        sink.close()
        if code != expected:
            failed.append("%s: код %d, ждали %d" % (name, code, expected))
    part = write("part.tsv", [rows[1], rows[4]])
    swapped = write("swapped.tsv", [rows[4], rows[2]])
    extra = [
        ("часть A по порядку (--subset)", base, part, {"subset": True}, 0),
        ("не по порядку (--subset)", base, write("wrong.tsv", [rows[3], rows[1]]), {"subset": True}, 1),
        ("только 0x80 и 0x8C (--only)", base, part, {"only": {"0x80", "0x8C"}}, 0),
        ("подкоманда в --only", base, write("bf.tsv", [rows[5]]), {"only": {"0xBF/0x0F"}}, 0),
        ("до 0xA8 (--until)", base, write("until.tsv", rows[:3]), {"until": "0xA8"}, 0),
        ("до 0x8C, но другой порядок сервера", base, swapped, {"until": "0x8C", "only": {"0x8C", "0xA8"}}, 1),
    ]
    for name, a, b, options, expected in extra:
        sink = open(os.devnull, "w")
        code = run(a, b, True, 20, sink, **options)
        sink.close()
        if code != expected:
            failed.append("%s: код %d, ждали %d" % (name, code, expected))
    cases += extra
    if failed:
        print("ОШИБКА самопроверки сравнения: " + "; ".join(failed))
        return 1
    print("Самопроверка сравнения: ОК (%d случаев: совпадение, порядок направлений, убранный, лишний, длина, подкоманда, "
          "часть по порядку, выбор пакетов, «до пакета»)" % len(cases))
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    with_length = "--ids" not in argv
    values = {}
    for option in ("--max", "--only", "--until"):
        if option in argv:
            values[option] = argv[argv.index(option) + 1]
    paths = [a for i, a in enumerate(argv) if not a.startswith("--") and (i == 0 or argv[i - 1] not in values)]
    if len(paths) != 2:
        print(__doc__)
        return 2
    only = set(values["--only"].split(",")) if "--only" in values else None
    return run(paths[0], paths[1], with_length, int(values.get("--max", 20)), sys.stdout, only, values.get("--until"),
               "--subset" in argv)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
