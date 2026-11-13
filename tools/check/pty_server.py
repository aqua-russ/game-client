#!/usr/bin/env python3
"""Сервер в настоящем терминале (pty): вывод — в лог, ответы на вопросы — по очереди; работает, пока его не остановят.

    pty_server.py --log ФАЙЛ [--answer "ВОПРОС=ОТВЕТ" ...] -- команда аргументы

Нужен tools/check/world.sh: в пустом мире ModernUO спрашивает «создать владельца?» и читает ответ только из терминала.
SIGINT и SIGTERM передаются серверу как Ctrl+C. Совместим с Python 3.9.
"""
import argparse
import os
import pty
import select
import signal


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--log", required=True)
    parser.add_argument("--answer", action="append", default=[])
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    answers = [(q.encode(), (a + "\n").encode()) for q, a in (x.split("=", 1) for x in args.answer)]

    pid, fd = pty.fork()
    if pid == 0:
        os.execvp(command[0], command)

    def stop(*_):
        try:
            os.kill(pid, signal.SIGINT)
        except ProcessLookupError:
            pass

    signal.signal(signal.SIGINT, stop)
    signal.signal(signal.SIGTERM, stop)
    seen = b""
    with open(args.log, "ab", buffering=0) as log:
        while True:
            try:
                ready, _, _ = select.select([fd], [], [], 0.2)
            except InterruptedError:
                continue
            if fd in ready:
                try:
                    data = os.read(fd, 4096)
                except OSError:
                    break
                if not data:
                    break
                log.write(data)
                seen += data
                while answers and answers[0][0] in seen:
                    os.write(fd, answers[0][1])
                    seen = seen[seen.index(answers[0][0]) + len(answers[0][0]):]
                    answers.pop(0)
            if os.waitpid(pid, os.WNOHANG)[0]:
                break


if __name__ == "__main__":
    main()
