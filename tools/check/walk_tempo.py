#!/usr/bin/env python3
"""Паузы между шагами пешком (без бега) в логе пакетов: медиана и число пауз (tools/check/walk.sh).

    python3 walk_tempo.py <лог пакетов> [<эталон>]

Берутся запросы шага 0x02 от клиента. Пауза считается между двумя шагами подряд в одну сторону без бита бега (0x80);
поворот (другое направление) не шаг — пауза до и после него не считается. С эталоном печатает обе медианы и выходит
с кодом 1, если они расходятся больше чем на 5% (PLAN Стадия 3, критерии: темп).
"""
import statistics
import sys

RUN_BIT = 0x80
FACING = 0x07
TOLERANCE = 0.05


def walk_gaps(path):
    gaps = []
    prev = None  # (время, направление, бег, шаг ли это)
    with open(path, encoding="utf-8") as log:
        for line in log:
            fields = line.rstrip("\n").split("\t")
            if len(fields) < 8 or fields[1] != "C>S" or fields[2] != "0x02":
                continue
            data = bytes.fromhex(fields[7].replace(" ", ""))
            time, facing, run = int(fields[0]), data[1] & FACING, bool(data[1] & RUN_BIT)
            is_move = prev is not None and prev[1] == facing
            if is_move and prev[3] and not run and not prev[2]:
                gaps.append(time - prev[0])
            prev = (time, facing, run, is_move)
    return gaps


def describe(gaps):
    return "медиана %d мс, пауз %d" % (statistics.median(gaps), len(gaps)) if gaps else "шагов пешком нет"


def main():
    ours = walk_gaps(sys.argv[1])
    if len(sys.argv) < 3:
        print("Шаг пешком: " + describe(ours))
        return 0 if ours else 1
    golden = walk_gaps(sys.argv[2])
    print("Шаг пешком: бот — %s; ClassicUO (эталон) — %s" % (describe(ours), describe(golden)))
    if not ours or not golden:
        return 1
    ratio = statistics.median(ours) / statistics.median(golden)
    return 0 if abs(ratio - 1) <= TOLERANCE else 1


if __name__ == "__main__":
    sys.exit(main())
