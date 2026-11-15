#!/usr/bin/env python3
"""Итог проверки чужих мобов (tools/check/others.sh): строки «Чужой …» из журнала бота-GM против шагов игрока.

    python3 others_check.py <журнал GM> <журнал игрока>

Игрок (тело 0x0190): GM увидел столько шагов, сколько игрок сделал (сумма «Ходьба …: шагов N»), и показал все; в каждом
режиме (бег пешком, шаг пешком, бег верхом) средний интервал прихода шагов совпадает со средней длительностью показа ±5%;
перескок ровно один — телепорт от GM. Крыса (тело 0x00EE): перескоков нет, показаны все пришедшие шаги; её темп только
печатается — NPC ходит в своём темпе (PLAN Стадия 3, п. 8; ADR 0020). Печатает «  ОК: …» / «  ОШИБКА: …», код 1 при ошибке.
"""
import re
import sys

TOLERANCE = 0.05
WALKER_MODES = ("бег пешком", "шаг пешком", "бег верхом")
LINE = re.compile(r"Чужой (0x[0-9A-F]{8}) тело (0x[0-9A-F]{4}): пришло (\d+), показано (\d+), выброшено (\d+)(?:; (.*?))?; "
                  r"догонялок (\d+), перескоков (\d+) \(телепорт (\d+), рывок (\d+), отставание (\d+)\), появлений (\d+)\.")
MODE = re.compile(r"(шаг пешком|бег пешком|шаг верхом|бег верхом) (—|\d+)/(\d+) мс ×(\d+)")
WALK = re.compile(r"Ходьба [^:]*: шагов (\d+)")

failed = False


def ok(text):
    print("  ОК: " + text)


def fail(text):
    global failed
    failed = True
    print("  ОШИБКА: " + text)


def others(path):
    found = []
    with open(path, encoding="utf-8", errors="replace") as log:
        for line in log:
            m = LINE.search(line)
            if m:
                g = m.groups()
                modes = {name: (None if arr == "—" else int(arr), int(shown), int(n)) for name, arr, shown, n in MODE.findall(g[5] or "")}
                found.append({"serial": g[0], "body": g[1], "received": int(g[2]), "shown": int(g[3]), "dropped": int(g[4]),
                              "modes": modes, "catchups": int(g[6]), "snaps": int(g[7]), "teleports": int(g[8]),
                              "jumps": int(g[9]), "backlogs": int(g[10]), "text": m.group(0)})
    return found


def check_walker(walker, walked):
    if walker["received"] == walked and walker["shown"] == walked and walker["dropped"] == 0:
        ok("GM увидел и показал все шаги игрока: %d из %d" % (walker["shown"], walked))
    else:
        fail("шаги игрока: сделал %d, GM увидел %d, показал %d, выбросил %d" % (walked, walker["received"], walker["shown"], walker["dropped"]))
    for name in WALKER_MODES:
        arrival, shown, count = walker["modes"].get(name, (None, 0, 0))
        if arrival is None or shown == 0:
            fail("%s: нет темпа прихода (шагов %d)" % (name, count))
        elif abs(arrival - shown) <= shown * TOLERANCE:
            ok("%s: шаги приходят раз в %d мс, показ %d мс (шагов %d)" % (name, arrival, shown, count))
        else:
            fail("%s: шаги приходят раз в %d мс, а показ %d мс — расхождение больше 5%%" % (name, arrival, shown))
    if walker["teleports"] == 1 and walker["snaps"] == 1:
        ok("перескок у игрока один — телепорт от GM; догонялок %d" % walker["catchups"])
    else:
        fail("перескоки у игрока: нужен один телепорт, а %s" % walker["text"].split("; догонялок ")[1])


def check_rat(rat):
    tempo = ", ".join("%s: приход %s / показ %d мс ×%d" % (name, "—" if a is None else a, s, n) for name, (a, s, n) in rat["modes"].items())
    if rat["snaps"] == 0 and rat["shown"] == rat["received"]:
        ok("крыса: шагов %d, все показаны, перескоков нет (%s)" % (rat["shown"], tempo or "не ходила"))
    else:
        fail("крыса: %s" % rat["text"])


def main():
    seen = others(sys.argv[1])
    walked = 0
    with open(sys.argv[2], encoding="utf-8", errors="replace") as log:
        walked = sum(int(n) for n in WALK.findall(log.read()))
    walkers = sorted((o for o in seen if o["body"] == "0x0190"), key=lambda o: o["received"])
    rats = [o for o in seen if o["body"] == "0x00EE"]
    if not walkers or walked == 0:
        fail("нет строки GM об игроке или игрок не ходил (журналы: Logs/check/others-gm.log, others-player.log)")
    else:
        check_walker(walkers[-1], walked)
    if rats:
        check_rat(rats[0])
    else:
        fail("GM не видел крысу (журнал: Logs/check/others-gm.log)")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
