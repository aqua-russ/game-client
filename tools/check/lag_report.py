#!/usr/bin/env python3
"""Разбор и проверка итогов замера под лагом (PLAN Стадия 3, п. 12; tools/check/lag-walk.sh).

    python3 lag_report.py player <лог_бота> <ожидаемая_задержка_мс> <допуск_темпа_%> [<мин_шагов>]
    python3 lag_report.py others <лог_GM> <лог_игрока>

Печатает понятную сводку замеров (откаты, ресинхронизации, задержка 0x02->0x22, темп, догонялки и перескоки чужих).
Код выхода 0 — всё по критериям, 1 — есть отказ/рассинхрон/лишний перескок/выход за допуск.
"""
import re
import sys

MOVEMENT_RE = re.compile(
    r"Движение: шагов (\d+), поворотов (\d+), отказов сервера (\d+), ресинхронизаций (\d+)"
    r"(?:; подтверждение шага: мин (\d+) мс, среднее (\d+) мс, макс (\d+) мс \(измерений (\d+)\))?"
)
TEMPO_RE = re.compile(
    r"Темп: фактическое время (\d+) мс, сервер (\d+) мс \(([+-]?\d+,\d+)%; (\d+,\d+) против (\d+,\d+) шага/с\)"
)
WALK_RE = re.compile(
    r"Ходьба ([^:]+): шагов (\d+), отказов сервера (\d+), ресинхронизаций (\d+); "
    r"пауза между шагами в среднем (\d+) мс \((\d+,\d+) шага в секунду\), от (\d+) до (\d+) мс"
)
SYNCS_RE = re.compile(r"Сверка с сервером: .*как предсказано\.")
OTHER_RE = re.compile(
    r"Чужой (0x[0-9A-F]{8}) тело (0x[0-9A-F]{4}): пришло (\d+), показано (\d+), выброшено (\d+)(?:; (.*?))?; "
    r"догонялок (\d+), перескоков (\d+) \(телепорт (\d+), рывок (\d+), отставание (\d+)\), появлений (\d+)\."
)
PLAYER_WALK_COUNT_RE = re.compile(r"Ходьба [^:]*: шагов (\d+)")


def check_player(log_path, expected_rtt_ms, max_tempo_pct, min_steps):
    text = open(log_path, encoding="utf-8", errors="replace").read()
    m = MOVEMENT_RE.search(text)
    if not m:
        print("  ОШИБКА: бот не напечатал строку «Движение: …» (%s)" % log_path)
        return 1

    steps, turns, rejects, resyncs = int(m.group(1)), int(m.group(2)), int(m.group(3)), int(m.group(4))
    min_ack = int(m.group(5)) if m.group(5) else 0
    avg_ack = int(m.group(6)) if m.group(6) else 0
    max_ack = int(m.group(7)) if m.group(7) else 0
    ack_n = int(m.group(8)) if m.group(8) else 0
    syncs = len(SYNCS_RE.findall(text))

    tempos = TEMPO_RE.findall(text)
    actual_sum = sum(int(t[0]) for t in tempos)
    expected_sum = sum(int(t[1]) for t in tempos)
    diff_pct = ((actual_sum - expected_sum) * 100.0 / expected_sum) if expected_sum > 0 else 0.0

    walks = WALK_RE.findall(text)
    walk_parts = [
        "%s=%s шага/с (%s мс)" % (w[0], w[5], w[4])
        for w in walks
    ]

    ok = True
    if steps < min_steps:
        print("  ОШИБКА: пройдено %d шагов, ожидалось не меньше %d" % (steps, min_steps))
        ok = False
    if rejects != 0 or resyncs != 0:
        print("  ОШИБКА: отказов сервера %d, ресинхронизаций %d (ожидалось 0)" % (rejects, resyncs))
        ok = False
    if expected_rtt_ms > 0 and avg_ack < expected_rtt_ms:
        print("  ОШИБКА: средняя задержка шага %d мс меньше заданной %d мс" % (avg_ack, expected_rtt_ms))
        ok = False
    if expected_sum > 0 and abs(diff_pct) > max_tempo_pct:
        print("  ОШИБКА: отклонение темпа %.2f%% превышает допуск ±%.1f%%" % (diff_pct, max_tempo_pct))
        ok = False

    summary = (
        "шагов %d (поворотов %d), отказов %d, рассинхронов %d, сверок %d; подтверждение %d/%d/%d мс (n=%d)"
        % (steps, turns, rejects, resyncs, syncs, min_ack, avg_ack, max_ack, ack_n)
    )
    if expected_sum > 0:
        summary += "; темп %+.2f%% (%d/%d мс)" % (diff_pct, actual_sum, expected_sum)
    if walk_parts:
        summary += "; отрезки: " + ", ".join(walk_parts)
    print(("  ОК: " if ok else "  ПОДРОБНО: ") + summary)
    return 0 if ok else 1


def check_others(gm_path, player_path):
    gm_text = open(gm_path, encoding="utf-8", errors="replace").read()
    player_text = open(player_path, encoding="utf-8", errors="replace").read()
    walked = sum(int(n) for n in PLAYER_WALK_COUNT_RE.findall(player_text))
    found = [m.groups() for m in OTHER_RE.finditer(gm_text)]
    walkers = sorted((g for g in found if g[1] == "0x0190"), key=lambda g: int(g[2]))
    rats = [g for g in found if g[1] == "0x00EE"]

    ok = True
    if not walkers or walked == 0:
        print("  ОШИБКА: нет наблюдения за игроком (GM=%s, player=%s)" % (gm_path, player_path))
        return 1

    w = walkers[-1]
    received, shown, dropped = int(w[2]), int(w[3]), int(w[4])
    catchups, snaps = int(w[6]), int(w[7])
    tele, jump, backlog = int(w[8]), int(w[9]), int(w[10])
    if received != walked or shown != walked or dropped != 0:
        print("  ОШИБКА: шаги игрока %d, GM увидел %d, показал %d, выбросил %d" % (walked, received, shown, dropped))
        ok = False
    if catchups == 0:
        print("  ОШИБКА: догонялка не сработала при всплеске (догонялок 0)")
        ok = False
    if snaps != 0:
        print("  ОШИБКА: лишние перескоки при всплеске: %d (телепорт %d, рывок %d, отставание %d)" % (snaps, tele, jump, backlog))
        ok = False
    if ok:
        print("  ОК: игрок — показано %d/%d, выброшено 0, догонялок %d, перескоков 0 (%s)" % (shown, walked, catchups, w[5] or "—"))

    if not rats:
        print("  ОШИБКА: GM не видел крысу")
        ok = False
    else:
        r = rats[0]
        r_recv, r_shown, r_drop, r_snaps = int(r[2]), int(r[3]), int(r[4]), int(r[7])
        if r_snaps != 0 or r_shown != r_recv or r_drop != 0:
            print("  ОШИБКА: крыса — пришло %d, показано %d, выброшено %d, перескоков %d" % (r_recv, r_shown, r_drop, r_snaps))
            ok = False
        else:
            print("  ОК: крыса — шагов %d, все показаны, перескоков 0" % r_shown)

    return 0 if ok else 1


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    if mode == "player" and len(sys.argv) >= 5:
        min_steps = int(sys.argv[5]) if len(sys.argv) >= 6 else 1
        return check_player(sys.argv[2], int(sys.argv[3]), float(sys.argv[4]), min_steps)
    if mode == "others" and len(sys.argv) >= 4:
        return check_others(sys.argv[2], sys.argv[3])
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
