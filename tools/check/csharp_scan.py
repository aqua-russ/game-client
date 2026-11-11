"""Грубый разбор C#-файла для tools/check/architecture.py: без компилятора, только скобки и регулярки.

Умеет: вычистить комментарии и строки (включая $"..{x}.." и @"..."), найти типы верхнего уровня,
методы с их длиной, вложенностью и числом параметров, члены интерфейсов.
Неточности допустимы (правило без скобок, экзотический синтаксис) — цель поймать явные нарушения.
Совместим с Python 3.9 (системный python3 на macOS).
"""
import bisect
import re

NEST_KEYWORDS = re.compile(r"^(?:else\s+)?(?:if|else|for|foreach|while|do|switch)\b")
BLOCK_KEYWORDS = re.compile(r"^(?:try|catch|finally|using|lock|fixed|checked|unchecked|unsafe)\b")
TYPE_DECL = re.compile(r"\b(class|struct|interface|enum|record)\s+([A-Za-z_]\w*)")
CASE_LABELS = re.compile(r"^(?:(?:case\s+[^:]+|default)\s*:\s*)+")
IDENT_BEFORE_PAREN = re.compile(r"([A-Za-z_]\w*)\s*(?:<[^()]*>)?\s*$")


def strip_code(text):
    """Заменяет комментарии, строки и символы пробелами, сохраняя переводы строк (и номера строк)."""
    out = list(text)
    n = len(text)
    i = 0
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if c == "/" and nxt == "/":
            j = text.find("\n", i)
            j = n if j < 0 else j
        elif c == "/" and nxt == "*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
        elif c == "'":
            j = _skip_char(text, i)
        elif c in "$@\"":
            j = _skip_string(text, i)
            if j is None:
                i += 1
                continue
        else:
            i += 1
            continue
        for k in range(i, j):
            if out[k] != "\n":
                out[k] = " "
        i = j
    return "".join(out)


def _skip_char(text, i):
    n = len(text)
    if i + 1 < n and text[i + 1] == "\\":
        j = text.find("'", i + 2)
    else:
        j = text.find("'", i + 2) if i + 2 < n and text[i + 2] != "'" else i + 2
    return n if j < 0 else j + 1


def _skip_string(text, i):
    """Возвращает индекс после строкового литерала, начинающегося с i, или None, если это не строка."""
    n = len(text)
    j = i
    prefix = ""
    while j < n and text[j] in "$@" and len(prefix) < 2:
        prefix += text[j]
        j += 1
    if j >= n or text[j] != '"':
        return None
    verbatim = "@" in prefix
    interp = "$" in prefix
    j += 1
    depth = 0
    while j < n:
        ch = text[j]
        nxt = text[j + 1] if j + 1 < n else ""
        if depth > 0:
            if ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
            elif ch == "'":
                j = _skip_char(text, j)
                continue
            elif ch in "$@\"":
                end = _skip_string(text, j)
                if end is not None:
                    j = end
                    continue
            j += 1
            continue
        if interp and ch == "{":
            if nxt == "{":
                j += 2
                continue
            depth = 1
            j += 1
            continue
        if interp and ch == "}" and nxt == "}":
            j += 2
            continue
        if verbatim:
            if ch == '"':
                if nxt == '"':
                    j += 2
                    continue
                return j + 1
        else:
            if ch == "\\":
                j += 2
                continue
            if ch == '"':
                return j + 1
            if ch == "\n":
                return j
        j += 1
    return n


def _strip_attributes(header):
    h = header.strip()
    while h.startswith("["):
        depth = 0
        for k, ch in enumerate(h):
            if ch == "[":
                depth += 1
            elif ch == "]":
                depth -= 1
                if depth == 0:
                    h = h[k + 1:].strip()
                    break
        else:
            return h
    return h


def _has_assignment(header):
    """Есть ли '=' (не '==', '=>', '<=' ...) вне скобок — признак поля или инициализатора."""
    depth = 0
    for k, ch in enumerate(header):
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        elif ch == "=" and depth == 0:
            prev = header[k - 1] if k > 0 else ""
            nxt = header[k + 1] if k + 1 < len(header) else ""
            if prev not in "=!<>" and nxt not in "=>":
                return True
    return False


def _signature(header):
    """(имя, текст параметров) для заголовка метода или None."""
    if re.search(r"\boperator\b", header):
        start = header.find("(")
        name = "operator"
    else:
        if "=>" in header:
            header = header[:header.find("=>")]
        if _has_assignment(header):
            return None
        start = header.find("(")
        if start < 0:
            return None
        m = IDENT_BEFORE_PAREN.search(header[:start])
        if not m:
            return None
        name = m.group(1)
        if name in ("if", "for", "foreach", "while", "switch", "catch", "using", "lock", "fixed", "return", "new", "nameof", "typeof", "sizeof", "default"):
            return None
    if start < 0:
        return None
    depth = 0
    for k in range(start, len(header)):
        if header[k] == "(":
            depth += 1
        elif header[k] == ")":
            depth -= 1
            if depth == 0:
                return name, header[start + 1:k]
    return name, header[start + 1:]


def count_params(params):
    text = params.strip()
    if not text:
        return 0
    depth = 0
    count = 1
    for ch in text:
        if ch in "(<[{":
            depth += 1
        elif ch in ")>]}":
            depth -= 1
        elif ch == "," and depth == 0:
            count += 1
    return count


class Block(object):
    def __init__(self, kind, name, line, type_name=None):
        self.kind = kind            # namespace | type | method | accessor | property | nest | block | other
        self.name = name
        self.line = line
        self.type_name = type_name  # для type: class/struct/interface/enum/record
        self.max_nest = 0
        self.members = 0
        self.params = 0


class FileScan(object):
    def __init__(self):
        self.namespaces = []        # объявленные namespace
        self.top_types = []         # имена типов верхнего уровня
        self.methods = []           # (qualified name, lines, max nesting, params, is_ctor)
        self.interfaces = []        # (имя, число членов)


def scan(text):
    clean = strip_code(text)
    line_starts = [0] + [m.end() for m in re.finditer("\n", clean)]

    def line_of(index):
        return bisect.bisect_right(line_starts, index)

    result = FileScan()
    stack = []
    paren = 0
    stmt_start = 0

    for idx, ch in enumerate(clean):
        if ch == "(":
            paren += 1
        elif ch == ")":
            paren -= 1
        elif ch == "{":
            header_raw = clean[stmt_start:idx]
            header = _strip_attributes(header_raw)
            block = _classify(header, stack, line_of(stmt_start + _offset(header_raw, header)), result)
            stack.append(block)
            stmt_start = idx + 1
        elif ch == "}":
            if stack:
                _close(stack.pop(), line_of(idx), stack, result)
            stmt_start = idx + 1
        elif ch == ";" and paren <= 0:
            header_raw = clean[stmt_start:idx]
            header = _strip_attributes(header_raw)
            _statement(header, stack, line_of(stmt_start + _offset(header_raw, header)), line_of(idx), result)
            stmt_start = idx + 1
    return result


def _offset(raw, header):
    """Позиция заголовка (без атрибутов) внутри сырого текста — чтобы строка метода считалась с сигнатуры."""
    if not header:
        return len(raw) - len(raw.lstrip())
    pos = raw.rfind(header)
    return pos if pos >= 0 else len(raw) - len(raw.lstrip())


def _enclosing_type(stack):
    for block in reversed(stack):
        if block.kind == "type":
            return block
    return None


def _code_owner(stack):
    """Ближайший метод/аксессор, внутри которого стоим, или None."""
    for block in reversed(stack):
        if block.kind in ("method", "accessor"):
            return block
        if block.kind == "type":
            return None
    return None


def _classify(header, stack, line, result):
    parent = stack[-1] if stack else None
    pkind = parent.kind if parent else None
    h = header.strip()

    if h.startswith("namespace"):
        result.namespaces.append(h[len("namespace"):].strip())
        return Block("namespace", h, line)

    if pkind in (None, "namespace", "type"):
        m = TYPE_DECL.search(h)
        if m and not _has_assignment(h) and "=>" not in h:
            if pkind in (None, "namespace"):
                result.top_types.append(m.group(2))
            if pkind == "type" and parent.type_name == "interface":
                parent.members += 1
            return Block("type", m.group(2), line, m.group(1))

    if pkind == "type":
        if parent.type_name == "interface":
            parent.members += 1
        sig = _signature(h)
        if sig:
            block = Block("method", parent.name + "." + sig[0], line)
            block.params = count_params(sig[1])
            return block
        if "=>" in h or _has_assignment(h):
            return Block("other", h, line)
        return Block("property", parent.name + "." + _last_word(h), line)

    if pkind == "property":
        return Block("accessor", parent.name + "." + _last_word(h), line)

    if _code_owner(stack) is not None:
        h = CASE_LABELS.sub("", h)
        if NEST_KEYWORDS.match(h):
            return Block("nest", h, line)
        if BLOCK_KEYWORDS.match(h):
            return Block("block", h, line)
    return Block("other", h, line)


def _last_word(h):
    words = re.findall(r"[A-Za-z_]\w*", h)
    return words[-1] if words else "?"


def _close(block, end_line, stack, result):
    if block.kind == "type" and block.type_name == "interface":
        result.interfaces.append((block.name, block.members))
    if block.kind in ("method", "accessor"):
        _record_method(block, end_line, stack, result)
        return
    if block.kind == "nest":
        owner = _code_owner(stack)
        if owner is not None:
            depth = 1 + sum(1 for b in stack[stack.index(owner) + 1:] if b.kind == "nest")
            owner.max_nest = max(owner.max_nest, depth)


def _record_method(block, end_line, stack, result):
    owner_type = _enclosing_type(stack)
    method_name = block.name.split(".")[-1]
    is_ctor = owner_type is not None and method_name == owner_type.name
    params = block.params
    result.methods.append((block.name, end_line - block.line + 1, block.max_nest, params, is_ctor))


def _statement(header, stack, start_line, end_line, result):
    parent = stack[-1] if stack else None
    pkind = parent.kind if parent else None
    h = header.strip()
    if not h:
        return
    if pkind in (None, "namespace"):
        if re.search(r"\bdelegate\b", h) or TYPE_DECL.search(h):
            m = TYPE_DECL.search(h)
            name = m.group(2) if m else (_signature(h) or ("?", ""))[0]
            result.top_types.append(name)
        return
    if pkind != "type":
        return
    if parent.type_name == "interface":
        parent.members += 1
    sig = _signature(h)
    if sig and not re.search(r"\bdelegate\b", h):
        name, params = sig
        is_ctor = name == parent.name
        result.methods.append((parent.name + "." + name, end_line - start_line + 1, 0, count_params(params), is_ctor))
