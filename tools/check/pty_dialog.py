#!/usr/bin/env python3
"""Запуск команды в настоящем терминале (pty) с ответами на вопросы — для проверки скрытого ввода пароля и Ctrl+C.

    pty_dialog.py [--secret ТЕКСТ] [--ctrl-c-after СЕКУНД] [--answer "ВОПРОС=ОТВЕТ" ...] -- команда аргументы

Ждёт в выводе каждый ВОПРОС по очереди и отвечает ОТВЕТ + Enter. --ctrl-c-after — нажать Ctrl+C через столько секунд.
--secret — проверить, что этот текст не появился на экране (пароль не отображается). Печатает вывод команды и выходит
с её кодом; 98 — вопрос не дождались, 99 — секрет попал на экран.
"""
import argparse
import os
import pty
import select
import sys
import time

TIMEOUT = 60


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--secret", default="")
    parser.add_argument("--ctrl-c-after", type=float, default=0)
    parser.add_argument("--answer", action="append", default=[])
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    answers = [a.split("=", 1) for a in args.answer]

    pid, fd = pty.fork()
    if pid == 0:
        os.execvp(command[0], command)

    output = b""
    started = time.time()
    interrupted = False
    while True:
        if args.ctrl_c_after and not interrupted and time.time() - started >= args.ctrl_c_after:
            os.write(fd, b"\x03")
            interrupted = True
        if answers and answers[0][0].encode() in output:
            os.write(fd, answers.pop(0)[1].encode() + b"\r")
            output += b"\n"  # следующий вопрос ищем после этого места
        if time.time() - started > TIMEOUT:
            print(output.decode(errors="replace"))
            print("pty_dialog: команда не завершилась за %d с" % TIMEOUT)
            os.kill(pid, 9)
            return 98
        ready, _, _ = select.select([fd], [], [], 0.1)
        if ready:
            try:
                chunk = os.read(fd, 4096)
            except OSError:
                break
            if not chunk:
                break
            output += chunk

    _, status = os.waitpid(pid, 0)
    text = output.decode(errors="replace")
    print(text)
    if answers:
        print("pty_dialog: не дождался вопроса «%s»" % answers[0][0])
        return 98
    if args.secret and args.secret in text:
        print("pty_dialog: секрет появился на экране")
        return 99
    return os.waitstatus_to_exitcode(status) if hasattr(os, "waitstatus_to_exitcode") else (status >> 8)


if __name__ == "__main__":
    sys.exit(main())
