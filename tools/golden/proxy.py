#!/usr/bin/env python3
"""Посредник между клиентом UO и сервером: пишет все байты сеанса в обе стороны (PLAN Стадия 0, п. 6).

    proxy.py --listen ПОРТ --server ХОСТ:ПОРТ --capture ФАЙЛ [--until-game-closed] [--status ФАЙЛ]
    proxy.py --self-test

Клиент подключается к посреднику, посредник — к серверу. Каждый кусок данных записывается строкой
«мс, соединение, c2s|s2c, байты hex»; ещё строки open и close (c — закрыл клиент, s — сервер). Перевести запись в лог
пакетов: TheVerge.Bot --convert-capture ЗАПИСЬ ЛОГ.

Переадресация 0x8C (OutgoingAccountPackets.cs, SendPlayServerAck: 4 байта адреса, 2 байта порта, 4 байта ключа) идёт
на соединении входа без сжатия; посредник подменяет в ней адрес и порт на свои, иначе клиент ушёл бы на игровой сервер
мимо него. В записи остаются байты сервера как есть.

--until-game-closed: выйти, когда закрылось игровое соединение (клиент прислал 0x91) и других не осталось.
--status: файл, куда пишется «game» после 0x91 — по нему скрипт знает, что клиент входит в мир.
Совместим с Python 3.9.
"""
import argparse
import asyncio
import sys
import time

FORMAT_LINE = "#TheVerge capture v1"
GAME_LOGIN = 0x91
REDIRECT = 0x8C
# Пакеты сервера на соединении входа (до 0x91, без сжатия): длина — постоянная или None (2 байта после номера).
LOGIN_SERVER_PACKETS = {0x82: 2, 0x53: 2, 0x8C: 11, 0xA8: None, 0xB9: 5, 0x86: None, 0xA9: None, 0xBD: None, 0x1B: 37}


class Capture:
    def __init__(self, path):
        self._file = open(path, "w", encoding="utf-8")
        self._start = time.monotonic()
        self._file.write(FORMAT_LINE + "\n")

    def write(self, connection, event, data=""):
        ms = int((time.monotonic() - self._start) * 1000)
        self._file.write("%d\t%d\t%s%s\n" % (ms, connection, event, "\t" + data if data else ""))
        self._file.flush()

    def close(self):
        self._file.close()


class LoginRewriter:
    """Режет поток сервера на соединении входа и подменяет в 0x8C адрес и порт. Неизвестный пакет — дальше без изменений."""

    def __init__(self, port):
        self._port = port
        self._buffer = b""
        self._off = False

    def feed(self, data):
        if self._off:
            return data
        self._buffer += data
        out = b""
        while self._buffer:
            pid = self._buffer[0]
            if pid not in LOGIN_SERVER_PACKETS:
                self._off = True
                out, self._buffer = out + self._buffer, b""
                break
            length = LOGIN_SERVER_PACKETS[pid]
            if length is None:
                if len(self._buffer) < 3:
                    break
                length = (self._buffer[1] << 8) | self._buffer[2]
            if len(self._buffer) < length:
                break
            packet, self._buffer = self._buffer[:length], self._buffer[length:]
            if pid == REDIRECT:
                packet = bytes([REDIRECT, 127, 0, 0, 1, self._port >> 8, self._port & 0xFF]) + packet[7:]
            out += packet
        return out


class Proxy:
    def __init__(self, listen, server_host, server_port, capture, until_game_closed, status):
        self.listen = listen
        self.server = (server_host, server_port)
        self.capture = capture
        self.until_game_closed = until_game_closed
        self.status = status
        self.next_id = 1
        self.open = 0
        self.game_closed = False
        self.done = None  # asyncio.Event создаётся в run(): в Python 3.9 он привязан к циклу, в котором создан

    async def handle(self, client_reader, client_writer):
        cid = self.next_id
        self.next_id += 1
        self.open += 1
        self.capture.write(cid, "open")
        say("соединение %d открыто" % cid)
        try:
            server_reader, server_writer = await asyncio.open_connection(*self.server)
        except OSError as e:
            say("сервер %s:%d недоступен: %s" % (self.server[0], self.server[1], e))
            self.capture.write(cid, "close", "s")
            client_writer.close()
            self.finish_connection(False)
            return
        state = {"head": b"", "game": False, "closed": None}
        rewriter = LoginRewriter(self.listen)
        await asyncio.gather(
            self.pump(cid, client_reader, server_writer, "c2s", state, None),
            self.pump(cid, server_reader, client_writer, "s2c", state, rewriter))
        self.finish_connection(state["game"])

    async def pump(self, cid, reader, writer, direction, state, rewriter):
        try:
            while True:
                data = await reader.read(65536)
                if not data:
                    break
                self.capture.write(cid, direction, data.hex())
                if direction == "c2s" and not state["game"]:
                    self.watch_game(cid, data, state)
                if rewriter is not None and not state["game"]:
                    data = rewriter.feed(data)
                writer.write(data)
                await writer.drain()
        except (ConnectionError, OSError):
            pass
        if state["closed"] is None:
            state["closed"] = "c" if direction == "c2s" else "s"
            self.capture.write(cid, "close", state["closed"])
            say("соединение %d закрыл %s" % (cid, "клиент" if state["closed"] == "c" else "сервер"))
        try:
            writer.close()
        except OSError:
            pass

    def watch_game(self, cid, data, state):
        state["head"] += data
        head = state["head"]
        if len(head) >= 9 and head[4] == GAME_LOGIN and head[0:4] == head[5:9]:
            state["game"] = True
            say("соединение %d — игровое (0x91): вход в мир" % cid)
            if self.status:
                with open(self.status, "w") as f:
                    f.write("game\n")

    def finish_connection(self, was_game):
        self.open -= 1
        self.game_closed = self.game_closed or was_game
        if self.until_game_closed and self.game_closed and self.open == 0 and self.done is not None:
            self.done.set()

    async def run(self):
        self.done = asyncio.Event()
        server = await asyncio.start_server(self.handle, "127.0.0.1", self.listen)
        say("посредник 127.0.0.1:%d → %s:%d" % (self.listen, self.server[0], self.server[1]))
        async with server:
            await self.done.wait()
        await asyncio.sleep(0.5)


def say(text):
    print("  посредник: " + text, file=sys.stderr, flush=True)


async def self_test():
    """Сервер-заглушка шлёт 0xA8 и 0x8C, разрезав 0x8C между кусками; клиент через посредника должен получить свой порт."""
    import os
    import tempfile
    server_list = bytes([0xA8, 0x00, 0x06, 0x5D, 0x00, 0x00])
    redirect = bytes([REDIRECT, 10, 0, 0, 5, 0x0A, 0x21, 1, 2, 3, 4])

    async def fake_server(reader, writer):
        await reader.read(100)
        writer.write(server_list + redirect[:4])
        await writer.drain()
        await asyncio.sleep(0.05)
        writer.write(redirect[4:])
        await writer.drain()
        writer.close()

    upstream = await asyncio.start_server(fake_server, "127.0.0.1", 0)
    upstream_port = upstream.sockets[0].getsockname()[1]
    path = os.path.join(tempfile.mkdtemp(), "self-test.capture")
    capture = Capture(path)
    proxy = Proxy(0, "127.0.0.1", upstream_port, capture, False, None)
    listener = await asyncio.start_server(proxy.handle, "127.0.0.1", 0)
    proxy.listen = listener.sockets[0].getsockname()[1]
    reader, writer = await asyncio.open_connection("127.0.0.1", proxy.listen)
    writer.write(bytes([0xEF]) + bytes(20))
    await writer.drain()
    got = await reader.read(100)
    while len(got) < len(server_list) + len(redirect):
        more = await reader.read(100)
        if not more:
            break
        got += more
    writer.close()
    await asyncio.sleep(0.1)
    capture.close()
    listener.close()
    upstream.close()
    expected = server_list + bytes([REDIRECT, 127, 0, 0, 1, proxy.listen >> 8, proxy.listen & 0xFF, 1, 2, 3, 4])
    text = open(path, encoding="utf-8").read()
    problems = []
    if got != expected:
        problems.append("клиент получил %s, ждали %s" % (got.hex(), expected.hex()))
    if redirect[4:].hex() not in text or "\topen" not in text or "\tclose\t" not in text:
        problems.append("в записи нет исходных байтов сервера, open или close:\n" + text)
    if problems:
        print("ОШИБКА самопроверки посредника: " + "; ".join(problems))
        return 1
    print("Самопроверка посредника: ОК (0x8C подменён на свой порт, разрезанный пакет собран, запись с байтами сервера)")
    return 0


def main():
    parser = argparse.ArgumentParser(description="Посредник с записью сеанса UO")
    parser.add_argument("--listen", type=int, default=2594)
    parser.add_argument("--server", default="127.0.0.1:2593")
    parser.add_argument("--capture")
    parser.add_argument("--until-game-closed", action="store_true")
    parser.add_argument("--status")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return asyncio.run(self_test())
    if not args.capture:
        parser.error("нужен --capture ФАЙЛ")
    host, port = args.server.rsplit(":", 1)
    capture = Capture(args.capture)
    try:
        asyncio.run(Proxy(args.listen, host, int(port), capture, args.until_game_closed, args.status).run())
    except KeyboardInterrupt:
        pass
    finally:
        capture.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
