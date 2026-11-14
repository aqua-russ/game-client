#!/usr/bin/env python3
"""Сверка журнала боя бота с логом пакетов (PLAN Стадия 4, п. 8).

  combat_order.py <лог бота> <лог пакетов .tsv>

Из лога пакетов берутся пакеты сервера 0x2F, 0x6E, 0x0B, 0x54, 0x70, 0xC0, 0xC7 и сами, по байтам (раскладка — как пишет
сервер: OutgoingCombatPackets.cs, OutgoingMobilePackets.cs, OutgoingDamagePackets.cs, OutgoingEffectPackets.cs),
превращаются в те же строки, что пишет бот («замах 0x… → 0x…», «анимация 0x… действие N», …). Строки журнала бота
(«Журнал боя: +… с …») должны совпасть с началом этого списка — те же события в том же порядке. Печатает итог;
код выхода 0 — совпадает, 1 — нет.
"""
import re
import sys

EFFECT_KINDS = {0: "Moving", 1: "Lightning", 2: "FixedXYZ", 3: "FixedFrom", 4: "Screen"}
JOURNAL = re.compile(r"Журнал боя: \+[0-9.]+ с (.*)\.$")


def u16(b, i):
    return (b[i] << 8) | b[i + 1]


def s16(b, i):
    v = u16(b, i)
    return v - 0x10000 if v >= 0x8000 else v


def u32(b, i):
    return (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3]


def serial(b, i):
    return "0x%08X" % u32(b, i)


def sbyte(v):
    v &= 0xFF
    return v - 0x100 if v >= 0x80 else v


def effect(b):
    kind = EFFECT_KINDS.get(b[1], str(b[1]))
    return "эффект %s 0x%04X %s → %s" % (kind, u16(b, 10), serial(b, 2), serial(b, 6))


def describe(b):
    pid = b[0]
    if pid == 0x2F:
        return "замах %s → %s" % (serial(b, 2), serial(b, 6))
    if pid == 0x6E:
        return "анимация %s действие %d" % (serial(b, 1), s16(b, 5))
    if pid == 0x0B:
        return "урон %s: %d" % (serial(b, 1), u16(b, 5))
    if pid == 0x54:
        return "звук 0x%04X в (%d, %d, %d)" % (u16(b, 2), s16(b, 6), s16(b, 8), sbyte(s16(b, 10)))
    if pid == 0x70 and b[1] == 4:
        return "эффект Screen 0x%04X 0x00000000 → 0x00000000" % u16(b, 10)
    if pid in (0x70, 0xC0, 0xC7):
        return effect(b)
    return None


def from_packets(path):
    events = []
    for line in open(path, encoding="utf-8"):
        cols = line.rstrip("\n").split("\t")
        if line.startswith("#") or len(cols) < 8 or cols[1] != "S>C":
            continue
        data = bytes(int(x, 16) for x in cols[7].split())
        text = describe(data) if data else None
        if text:
            events.append(text)
    return events


def from_journal(path):
    return [m.group(1) for m in (JOURNAL.search(line.rstrip("\n")) for line in open(path, encoding="utf-8")) if m]


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    journal = from_journal(sys.argv[1])
    packets = from_packets(sys.argv[2])
    if not journal:
        print("В логе бота нет строк журнала боя.")
        return 1
    for i, text in enumerate(journal):
        if i >= len(packets) or packets[i] != text:
            got = packets[i] if i < len(packets) else "(пакетов больше нет)"
            print("Расхождение на событии %d: журнал — «%s», лог пакетов — «%s»." % (i + 1, text, got))
            return 1
    kinds = {}
    for text in journal:
        kinds[text.split(" ")[0]] = kinds.get(text.split(" ")[0], 0) + 1
    summary = ", ".join("%s %d" % (k, v) for k, v in sorted(kinds.items()))
    print("Порядок совпадает: событий %d (%s), в логе пакетов таких %d." % (len(journal), summary, len(packets)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
