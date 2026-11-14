#!/usr/bin/env python3
"""Сравнение двух логов пакетов (PLAN Стадия 0, п. 6; docs/REFERENCE-CLIENT.md §5, шаг 1).

    diff.py A.tsv B.tsv [--ids] [--max N]
    diff.py --self-test

Сравнивается последовательность «номер пакета, подкоманда, длина» — отдельно для пакетов клиента (C>S) и сервера (S>C):
как пакеты двух направлений перемежаются, зависит от времени и сравнению не подлежит. --ids — без длины (тексты речи
и имена у разных клиентов разной длины). Печатает «совпадает» или расхождения с номерами строк; код выхода 0 —
совпадает, 1 — нет, 2 — файл не прочитать.
"""
import difflib
import os
import sys
import tempfile

DIRECTIONS = ("C>S", "S>C")


def read_log(path, with_length=True):
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
            key = (pid, sub, length) if with_length else (pid, sub)
            label = pid + ("/" + sub if sub != "-" else "") + " " + name + " (" + length + " байт)"
            packets[cols[1]].append((key, number, label))
    return packets


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


def run(path_a, path_b, with_length, max_listed, out=sys.stdout):
    try:
        a, b = read_log(path_a, with_length), read_log(path_b, with_length)
    except OSError as e:
        print("ОШИБКА: не прочитать лог: %s" % e, file=out)
        return 2
    same = True
    for d in DIRECTIONS:
        lines = compare(a[d], b[d], max_listed)
        if not lines:
            print("%s: совпадает (пакетов %d)" % (d, len(a[d])), file=out)
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
    if failed:
        print("ОШИБКА самопроверки сравнения: " + "; ".join(failed))
        return 1
    print("Самопроверка сравнения: ОК (%d случаев: совпадение, порядок направлений, убранный, лишний, длина, подкоманда)" % len(cases))
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    with_length = "--ids" not in argv
    max_listed = 20
    if "--max" in argv:
        max_listed = int(argv[argv.index("--max") + 1])
    paths = [a for i, a in enumerate(argv) if not a.startswith("--") and (i == 0 or argv[i - 1] != "--max")]
    if len(paths) != 2:
        print(__doc__)
        return 2
    return run(paths[0], paths[1], with_length, max_listed)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
