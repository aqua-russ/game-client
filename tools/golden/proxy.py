#!/usr/bin/env python3
"""Посредник между клиентом UO и сервером: пишет все байты сеанса в обе стороны и умеет имитировать задержку,
разброс и всплески лага (PLAN Стадия 0, п. 6; Стадия 3, п. 12).

    proxy.py --listen ПОРТ --server ХОСТ:ПОРТ --capture ФАЙЛ [--lag ЗАДЕРЖКА[,РАЗБРОС]] [--until-game-closed] [--status ФАЙЛ]
    proxy.py --self-test

Клиент подключается к посреднику, посредник — к серверу. Каждый кусок данных записывается строкой
«мс, соединение, c2s|s2c, байты hex»; ещё строки open и close (c — закрыл клиент, s — сервер). Перевести запись в лог
пакетов: TheVerge.Bot --convert-capture ЗАПИСЬ ЛОГ.

Задержка и разброс работают по тому же правилу, что DelayLine в клиенте: постоянная часть ± разброс (не меньше нуля),
пакеты никогда не обгоняют друг друга. Запись c2s ставит время отправки клиентом, s2c — время получения клиентом.
Совместим с Python 3.9.
"""
import argparse
import asyncio
import random
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


class DelayLine:
    """Очередь задержки одного направления без обгона пакетов (как Network/Diagnostics/DelayLine.cs)."""

    def __init__(self, delay_ms=0, jitter_ms=0, rng=None):
        self.delay_ms = max(0, delay_ms)
        self.jitter_ms = max(0, jitter_ms)
        self.rng = rng or random.Random()
        self.last_release = 0.0
        self.hold_until = 0.0

    def is_active(self):
        return self.delay_ms > 0 or self.jitter_ms > 0 or self.hold_until > 0.0

    def sample_s(self):
        if self.delay_ms == 0 and self.jitter_ms == 0:
            return 0.0
        offset = self.rng.randint(-self.jitter_ms, self.jitter_ms) if self.jitter_ms > 0 else 0
        return max(0, self.delay_ms + offset) / 1000.0

    def next_release(self, now):
        release = max(now + self.sample_s(), self.last_release, self.hold_until)
        self.last_release = release
        return release


class LagProfile:
    """Параметры задержки, разброса и периодического входящего всплеска для посредника."""

    def __init__(self, in_delay=0, in_jitter=0, out_delay=0, out_jitter=0, spike_ms=0, spike_every_ms=0, seed=None):
        self.in_delay = max(0, in_delay)
        self.in_jitter = max(0, in_jitter)
        self.out_delay = max(0, out_delay)
        self.out_jitter = max(0, out_jitter)
        self.spike_ms = max(0, spike_ms)
        self.spike_every_ms = max(0, spike_every_ms)
        self.rng = random.Random(seed) if seed is not None else random.Random()

    def new_line(self, incoming):
        return DelayLine(self.in_delay if incoming else self.out_delay,
                         self.in_jitter if incoming else self.out_jitter, self.rng)


class Proxy:
    def __init__(self, listen, server_host, server_port, capture, until_game_closed, status, lag=None):
        self.listen = listen
        self.server = (server_host, server_port)
        self.capture = capture
        self.until_game_closed = until_game_closed
        self.status = status
        self.lag = lag or LagProfile()
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
        state = {"head": b"", "game": False, "closed": None, "next_spike": 0.0}
        rewriter = LoginRewriter(self.listen)
        c2s_q, s2c_q = asyncio.Queue(), asyncio.Queue()
        c2s_line, s2c_line = self.lag.new_line(False), self.lag.new_line(True)
        await asyncio.gather(
            self.pump_read(cid, client_reader, c2s_q, "c2s", state, None, c2s_line),
            self.pump_write(cid, server_writer, c2s_q, "c2s", state),
            self.pump_read(cid, server_reader, s2c_q, "s2c", state, rewriter, s2c_line),
            self.pump_write(cid, client_writer, s2c_q, "s2c", state))
        self.finish_connection(state["game"])

    async def pump_read(self, cid, reader, queue, direction, state, rewriter, line):
        try:
            while True:
                data = await reader.read(65536)
                if not data:
                    break
                now = time.monotonic()
                if direction == "c2s":
                    self.capture.write(cid, direction, data.hex())
                    if not state["game"]:
                        self.watch_game(cid, data, state)
                raw = data
                if rewriter is not None and not state["game"]:
                    data = rewriter.feed(data)
                if direction == "s2c" and state["game"] and self.lag.spike_ms > 0:
                    if state["next_spike"] == 0.0:
                        state["next_spike"] = now + self.lag.spike_every_ms / 1000.0
                    if now >= state["next_spike"]:
                        line.hold_until = max(line.hold_until, now + self.lag.spike_ms / 1000.0)
                        state["next_spike"] = (now + (self.lag.spike_ms + self.lag.spike_every_ms) / 1000.0) if self.lag.spike_every_ms > 0 else float("inf")
                release = line.next_release(now) if line.is_active() else 0.0
                await queue.put((release, raw, data))
        except (ConnectionError, OSError):
            pass
        await queue.put(None)

    async def pump_write(self, cid, writer, queue, direction, state):
        try:
            while True:
                item = await queue.get()
                if item is None:
                    break
                release, raw, data = item
                if release > 0.0:
                    wait = release - time.monotonic()
                    if wait > 0:
                        await asyncio.sleep(wait)
                if direction == "s2c":
                    self.capture.write(cid, direction, raw.hex())
                if data:
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

    # Проверка искусственной задержки и порядка при разбросе: ~120 мс туда-обратно (80±20 + 40±10).
    lag = LagProfile(in_delay=80, in_jitter=20, out_delay=40, out_jitter=10, seed=7)
    lag_path = os.path.join(tempfile.mkdtemp(), "lag-test.capture")
    lag_cap = Capture(lag_path)

    async def echo_server(reader, writer):
        while True:
            chunk = await reader.read(100)
            if not chunk:
                break
            writer.write(chunk)
            await writer.drain()
        writer.close()

    echo_up = await asyncio.start_server(echo_server, "127.0.0.1", 0)
    echo_port = echo_up.sockets[0].getsockname()[1]
    lag_proxy = Proxy(0, "127.0.0.1", echo_port, lag_cap, False, None, lag)
    lag_srv = await asyncio.start_server(lag_proxy.handle, "127.0.0.1", 0)
    lr, lw = await asyncio.open_connection("127.0.0.1", lag_srv.sockets[0].getsockname()[1])
    t0 = time.monotonic()
    payload = bytes(range(1, 21))
    for b in payload:
        lw.write(bytes([b]))
        await lw.drain()
        await asyncio.sleep(0.002)
    received = b""
    while len(received) < len(payload):
        more = await lr.read(100)
        if not more:
            break
        received += more
    elapsed_ms = int((time.monotonic() - t0) * 1000)
    lw.close()
    await asyncio.sleep(0.15)
    lag_cap.close()
    lag_srv.close()
    echo_up.close()
    if received != payload:
        problems.append("лаг: порядок или байты исказились (%s)" % received.hex())
    if elapsed_ms < 85:
        problems.append("лаг: задержка слишком мала (%d мс)" % elapsed_ms)

    if problems:
        print("ОШИБКА самопроверки посредника: " + "; ".join(problems))
        return 1
    print("Самопроверка посредника: ОК (0x8C подменён, разрезанный пакет собран, лаг %d мс без перестановки)" % elapsed_ms)
    return 0


def parse_lag_pair(text):
    parts = text.split(",")
    if len(parts) > 2:
        raise ValueError("формат: задержка[,разброс]")
    delay = int(parts[0])
    jitter = int(parts[1]) if len(parts) == 2 else 0
    if delay < 0 or delay > 1000 or jitter < 0 or jitter > 500:
        raise ValueError("допустимо: задержка 0..1000, разброс 0..500")
    return delay, jitter


def main():
    parser = argparse.ArgumentParser(description="Посредник с записью сеанса UO")
    parser.add_argument("--listen", type=int, default=2594)
    parser.add_argument("--server", default="127.0.0.1:2593")
    parser.add_argument("--capture")
    parser.add_argument("--lag", help="задержка[,разброс] мс в каждую сторону, например 50,15")
    parser.add_argument("--delay-in", type=int, default=0)
    parser.add_argument("--jitter-in", type=int, default=0)
    parser.add_argument("--delay-out", type=int, default=0)
    parser.add_argument("--jitter-out", type=int, default=0)
    parser.add_argument("--lag-seed", type=int)
    parser.add_argument("--spike-ms", type=int, default=0, help="входящий всплеск задержки в игре, мс")
    parser.add_argument("--spike-every", type=int, default=5000, help="пауза между всплесками, мс (0 — один раз)")
    parser.add_argument("--until-game-closed", action="store_true")
    parser.add_argument("--status")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return asyncio.run(self_test())
    if not args.capture:
        parser.error("нужен --capture ФАЙЛ")
    in_d, in_j, out_d, out_j = args.delay_in, args.jitter_in, args.delay_out, args.jitter_out
    if args.lag:
        d, j = parse_lag_pair(args.lag)
        in_d, in_j, out_d, out_j = d, j, d, j
    lag = LagProfile(in_d, in_j, out_d, out_j, args.spike_ms, args.spike_every, args.lag_seed)
    host, port = args.server.rsplit(":", 1)
    capture = Capture(args.capture)
    try:
        asyncio.run(Proxy(args.listen, host, int(port), capture, args.until_game_closed, args.status, lag).run())
    except KeyboardInterrupt:
        pass
    finally:
        capture.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
