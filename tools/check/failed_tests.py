#!/usr/bin/env python3
"""Печатает упавшие тесты из отчёта dotnet test в формате TRX: название и первые строки причины.

Использование: failed_tests.py <отчёт.trx>
Нужен, потому что краткий вывод dotnet test сообщает только число упавших тестов, а на Mac его ещё и переводит.
TRX от языка системы не зависит. Совместим с Python 3.9.
"""
import sys
import xml.etree.ElementTree as ET

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
MAX_LINES = 6
MAX_TESTS = 20


def failed(path):
    root = ET.parse(path).getroot()
    classes = {}
    for test in root.iter(NS + "UnitTest"):
        method = test.find(NS + "TestMethod")
        if method is not None:
            classes[test.get("id")] = method.get("className", "").split(".")[-1]
    for result in root.iter(NS + "UnitTestResult"):
        if result.get("outcome") in ("Passed", "NotExecuted"):
            continue
        message = result.find("%sOutput/%sErrorInfo/%sMessage" % (NS, NS, NS))
        stack = result.find("%sOutput/%sErrorInfo/%sStackTrace" % (NS, NS, NS))
        text = (message.text if message is not None and message.text else "").strip()
        where = (stack.text if stack is not None and stack.text else "").strip().splitlines()[:1]
        owner = classes.get(result.get("testId"), "")
        name = result.get("testName", "?")
        yield ("%s.%s" % (owner, name) if owner else name), text, where


def main():
    if len(sys.argv) != 2:
        print("использование: failed_tests.py <отчёт.trx>")
        return 2
    try:
        tests = list(failed(sys.argv[1]))
    except (OSError, ET.ParseError) as error:
        print("не удалось прочитать отчёт тестов %s: %s" % (sys.argv[1], error))
        return 2
    for name, text, where in tests[:MAX_TESTS]:
        print("- %s" % name)
        for line in (text.splitlines()[:MAX_LINES] + [l.strip() for l in where]):
            print("    %s" % line)
    if len(tests) > MAX_TESTS:
        print("… и ещё %d" % (len(tests) - MAX_TESTS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
