#!/usr/bin/env python3
"""Генератор Assets/TheVerge/Network/Framing/PacketTable.g.cs из docs/protocol/packets.csv (PLAN 0.5).

  python3 tools/gen/packet_table.py           — перезаписать таблицу по матрице;
  python3 tools/gen/packet_table.py --check   — только сверить файл со свежей генерацией (tools/check/packets.sh).

Из матрицы с ошибками таблица не строится: сначала проверка по правилам tools/gen/packets_csv.py.
Только стандартная библиотека, совместимо с Python 3.9.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import packets_csv  # noqa: E402

OUTPUT = os.path.join(packets_csv.ROOT, "Assets", "TheVerge", "Network", "Framing", "PacketTable.g.cs")
OUTPUT_REL = "Assets/TheVerge/Network/Framing/PacketTable.g.cs"

_HEADER = """// Сгенерировано tools/gen/packet_table.py из docs/protocol/packets.csv. Руками не править: поправьте CSV и
// запустите генератор (docs/protocol/packets.md); tools/check/all.sh сверяет этот файл со свежей генерацией.
namespace TheVerge.Network.Framing
{
    /// <summary>
    /// Длины и имена пакетов протокола для клиента 7.0.95.0 из матрицы docs/protocol/packets.csv. Номера или
    /// подкоманды нет в матрице — возвращается значение по умолчанию: «неизвестен».
    /// </summary>
    public static class PacketTable
    {
        /// <summary>Пакет по номеру — для нарезки потока, обработчиков и логов.</summary>
        public static PacketInfo Get(PacketDirection direction, byte id) =>
            direction == PacketDirection.ServerToClient ? FromServer(id) : FromClient(id);

        /// <summary>
        /// Подкоманда пакета 0xBF, 0xD7, 0x12, 0xF0 или 0xF1. Для номера без подкоманд — «неизвестен».
        /// Подкоманда 0xBF и 0xD7 занимает 2 байта, поэтому параметр не byte: 0x0119 не должна стать 0x19.
        /// </summary>
        public static PacketInfo Get(PacketDirection direction, byte id, ushort subCommand) =>
            direction == PacketDirection.ServerToClient ? FromServer(id, subCommand) : FromClient(id, subCommand);

        /// <summary>Где у пакета подкоманда; у пакета без подкоманд — значение по умолчанию (подкоманды нет).</summary>
        public static SubCommandLayout GetSubCommandLayout(PacketDirection direction, byte id) =>
            direction == PacketDirection.ServerToClient ? ServerSubCommandLayout(id) : ClientSubCommandLayout(id);
"""

_FOOTER = """
        private static PacketInfo Fixed(int length, string name) => new PacketInfo(length, name);

        private static PacketInfo Variable(string name) => new PacketInfo(PacketInfo.VariableLength, name);
    }
}
"""


# Где подкоманда у пакетов с подкомандами: (смещение от начала пакета, размер в байтах). В CSV этого столбца нет —
# правило описано в docs/protocol/packets.md («Что в матрице») и сверено с сервером:
#   0xBF — 2 байта сразу после длины: IncomingExtendedCommandPackets.cs, ExtendedCommand (ReadUInt16);
#   0xD7 — 2 байта после serial: IncomingPlayerPackets.cs, EncodedCommand (ReadUInt32, ReadUInt16);
#   0x12 — 1 байт после длины: IncomingPlayerPackets.cs, TextCommand (ReadByte);
#   0xF0, 0xF1 — 1 байт после длины: UOContent/Network/ProtocolExtensions.cs (ReadByte), MapUO.cs.
SUBCOMMAND_LAYOUTS = {
    "0xBF": (3, 2),
    "0xD7": (7, 2),
    "0x12": (3, 1),
    "0xF0": (3, 1),
    "0xF1": (3, 1),
}


def _layout_method(name, rows):
    """Раскладка подкоманды для номеров, у которых в матрице есть подкоманды (в этом направлении)."""
    ids = sorted({r["id"] for r in rows if r["sub"]}, key=lambda i: int(i, 16))
    lines = ["", "        private static SubCommandLayout %s(byte id) => id switch" % name, "        {"]
    for packet_id in ids:
        if packet_id not in SUBCOMMAND_LAYOUTS:
            raise ValueError("у пакета %s есть подкоманды, но нет раскладки в SUBCOMMAND_LAYOUTS "
                             "(tools/gen/packet_table.py)" % packet_id)
        offset, size = SUBCOMMAND_LAYOUTS[packet_id]
        lines.append("            %s => new SubCommandLayout(%d, %d)," % (packet_id, offset, size))
    lines.append("            _ => default,")
    lines.append("        };")
    return lines


def _value(row):
    if row["length"] == packets_csv.VARIABLE:
        call = 'Variable("%s")' % row["name"]
    else:
        call = 'Fixed(%d, "%s")' % (int(row["length"]), row["name"])
    comment = "" if row["status"] == "нужно" else " // " + row["status"]
    return call + "," + comment


def _method(name, rows, with_sub):
    if with_sub:
        lines = ["", "        private static PacketInfo %s(byte id, ushort subCommand) => (id, subCommand) switch" % name]
    else:
        lines = ["", "        private static PacketInfo %s(byte id) => id switch" % name]
    lines.append("        {")
    for row in rows:
        key = "(%s, %s)" % (row["id"], row["sub"]) if with_sub else row["id"]
        lines.append("            %s => %s" % (key, _value(row)))
    lines.append("            _ => default,")
    lines.append("        };")
    return lines


def render(rows):
    """Текст PacketTable.g.cs по проверенным строкам матрицы (порядок строк — как в CSV)."""
    lines = [_HEADER.rstrip("\n")]
    for direction, name in (("s2c", "FromServer"), ("c2s", "FromClient")):
        own = [r for r in rows if r["dir"] == direction]
        lines += _method(name, [r for r in own if not r["sub"]], with_sub=False)
        lines += _method(name, [r for r in own if r["sub"]], with_sub=True)
    for direction, name in (("s2c", "ServerSubCommandLayout"), ("c2s", "ClientSubCommandLayout")):
        lines += _layout_method(name, [r for r in rows if r["dir"] == direction])
    return "\n".join(lines) + "\n" + _FOOTER


def main(argv):
    with open(packets_csv.CSV_PATH, encoding="utf-8") as fh:
        text = fh.read()
    server = packets_csv.SERVER_ROOT if os.path.isdir(packets_csv.SERVER_ROOT) else None
    errors = packets_csv.validate(text, server)
    if errors:
        for e in errors:
            print("ОШИБКА: " + e)
        print("ОШИБКА: таблица не построена — сначала исправьте docs/protocol/packets.csv.")
        return 1
    try:
        fresh = render(packets_csv.read_rows(text))
    except ValueError as e:
        print("ОШИБКА: " + str(e))
        return 1
    if "--check" in argv:
        current = open(OUTPUT, encoding="utf-8").read() if os.path.isfile(OUTPUT) else ""
        if current != fresh:
            print("ОШИБКА: %s не совпадает с docs/protocol/packets.csv: файл правили руками или забыли "
                  "перегенерировать. Запустите: python3 tools/gen/packet_table.py" % OUTPUT_REL)
            return 1
        print("Таблица пакетов %s совпадает с матрицей." % OUTPUT_REL)
        return 0
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(fresh)
    print("Записан %s." % OUTPUT_REL)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
