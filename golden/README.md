# Эталонные логи трафика (эталонный клиент ClassicUO)

Здесь записи 8 сценариев Стадии 0 (задача 0.6): `01-login`, `02-walk-20`, `03-speech`, `04-drag`, `05-container`, `06-gump`, `07-target`, `08-death` — файлы `<сценарий>.tsv` в формате нашего лога пакетов (`docs/CODE-MAP.md`, «Лог пакетов»). Пароль в них заменён звёздочками. Файлы только для чтения: по ним сверяются тесты и бот.

Записать — `tools/golden/record.sh` (на Mac, ClassicUO из `remaster/classicuo`); сравнить с ботом — `tools/golden/compare.sh`; сравнить два лога — `tools/golden/diff.sh`. Как и зачем — [`docs/REFERENCE-CLIENT.md`](../docs/REFERENCE-CLIENT.md). Допустимые расхождения — `allowed-diffs.md` (появится с первым сравнением).

ClassicUO при записи представляется версией клиента 7.0.95.0 (`TARGET.md`) и читает те же файлы данных, что и сервер (ADR 0003).
