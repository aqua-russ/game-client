#!/usr/bin/env python3
"""Портит копию файла карты для самопроверки tools/check/map.sh: меняет номера земли в каждом третьем блоке.

    corrupt_map.py <папка-копия>

map0.mul — блоки по 196 байт подряд; map0LegacyMUL.uop — те же блоки внутри кусков (таблицы кусков не трогаются,
файл остаётся читаемым). Портится младший байт номера земли каждой клетки (XOR 0x5A) — сервер и клиент разойдутся.
Совместим с Python 3.9.
"""
import os
import struct
import sys


def find(folder, name):
    for entry in os.listdir(folder):
        if entry.lower() == name.lower():
            return os.path.join(folder, entry)
    return None


def corrupt_blocks(data, start, length):
    """XOR младшего байта номера земли во всех клетках каждого третьего блока куска [start, start + length)."""
    block = 0
    for at in range(start, start + length - 195, 196):
        if block % 3 == 0:
            for cell in range(64):
                data[at + 4 + cell * 3] ^= 0x5A
        block += 1


def uop_chunks(data):
    chunks = []
    table = struct.unpack_from("<q", data, 12)[0]
    seen = set()
    while 0 < table < len(data) and table not in seen:
        seen.add(table)
        count, following = struct.unpack_from("<iq", data, table)
        for i in range(count):
            offset, header, stored, size, _, _, _ = struct.unpack_from("<qiiiQIh", data, table + 12 + i * 34)
            if offset and stored > 0 and size > 0:
                chunks.append((offset + header, size))
        table = following
    return chunks


def main():
    folder = sys.argv[1]
    path = find(folder, "map0.mul")
    if path:
        data = bytearray(open(path, "rb").read())
        corrupt_blocks(data, 0, len(data))
    else:
        path = find(folder, "map0LegacyMUL.uop")
        data = bytearray(open(path, "rb").read())
        for start, size in uop_chunks(data):
            corrupt_blocks(data, start, size)
    open(path, "wb").write(data)
    print("испорчен " + os.path.basename(path))


if __name__ == "__main__":
    main()
