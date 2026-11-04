#!/usr/bin/env python3
"""Consistency checks for the planning documents.

Usage: python3 scripts/check_docs.py
Exit code 0 when all checks pass, 1 when any check fails.

Checks:
  links      local Markdown links point to existing files
  sections   "x.md section N" / "x §N" / bare "section N" point to existing numbered headings
  features   every F-<AREA>-<n> used is defined once in features.md
  p0         P0 count matches the stated count and the milestone table; milestone features are P0
  milestones milestone names agree across features.md, tasks.md and README.md
  ids        assumption (A#), rule (R#) and decision (DEC-#) references are defined
  tasks      task IDs in "Depends on" columns and "task X" mentions exist; statuses are valid
  decisions  decisions.md index statuses are valid; Accepted/Proposed entries have a section
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "documentation"
FILES = sorted(DOCS.glob("*.md")) + [ROOT / "README.md", ROOT / "CLAUDE.md"]
DOC_NAMES = ["business-problem", "features", "domain-model", "architecture", "decisions", "tasks"]

errors: list[str] = []


def err(check: str, path: Path, line: int, msg: str) -> None:
    where = f"{path.relative_to(ROOT)}:{line}" if line else str(path.relative_to(ROOT))
    errors.append(f"[{check}] {where}: {msg}")


def read(path: Path) -> list[str]:
    return path.read_text(encoding="utf-8").splitlines()


def strip_struck(text: str) -> str:
    """Remove ~~struck~~ text, which records history rather than current claims."""
    return re.sub(r"~~.*?~~", "", text)


TEXT = {p: read(p) for p in FILES if p.exists()}


def lines(path: Path):
    for i, line in enumerate(TEXT.get(path, []), 1):
        yield i, line


def table_rows(path: Path, first_cell: str):
    """Yield (line_no, cells) for table rows whose first cell matches the regex."""
    for i, line in lines(path):
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if cells and re.fullmatch(first_cell, cells[0]):
            yield i, cells


# ---------------------------------------------------------------- links
def check_links() -> None:
    for path in TEXT:
        for i, line in lines(path):
            for target in re.findall(r"\]\(([^)\s]+)\)", line):
                if re.match(r"[a-z]+:", target) or target.startswith("#"):
                    continue
                file_part = target.split("#", 1)[0]
                if not (path.parent / file_part).exists():
                    err("links", path, i, f"broken link: {target}")


# ---------------------------------------------------------------- sections
def headings(path: Path) -> set[str]:
    nums = set()
    for _, line in lines(path):
        m = re.match(r"#{2,6} (\d+(?:\.\d+)*)\.? ", line)
        if m:
            nums.add(m.group(1))
    return nums


HEADINGS = {name: headings(DOCS / f"{name}.md") for name in DOC_NAMES}
QUALIFIED = re.compile(
    r"`?(" + "|".join(DOC_NAMES) + r")(?:\.md)?`?\s+(?:section|§)\s*(\d+(?:\.\d+)*)(?:-(\d+(?:\.\d+)*))?"
)
BARE = re.compile(r"\bsections?\s+(\d+(?:\.\d+)*)")


def check_sections() -> None:
    for path in TEXT:
        own = path.stem if path.parent == DOCS else None
        for i, line in lines(path):
            spans = []
            for m in QUALIFIED.finditer(line):
                spans.append(m.span())
                for num in filter(None, (m.group(2), m.group(3))):
                    if num not in HEADINGS[m.group(1)]:
                        err("sections", path, i, f"{m.group(1)}.md has no section {num}")
            for m in BARE.finditer(line):
                if any(a <= m.start() < b for a, b in spans) or own is None:
                    continue
                if m.group(1) not in HEADINGS[own]:
                    err("sections", path, i, f"{own}.md has no section {m.group(1)}")


# ---------------------------------------------------------------- features
FEATURES_MD = DOCS / "features.md"
FEATURE_DEFS: dict[str, str] = {}


def check_features() -> None:
    for i, cells in table_rows(FEATURES_MD, r"F-[A-Z]+-\d+"):
        fid, pri = cells[0], cells[-1]
        if fid in FEATURE_DEFS:
            err("features", FEATURES_MD, i, f"{fid} defined twice")
        if not re.fullmatch(r"P[0-3]", pri):
            err("features", FEATURES_MD, i, f"{fid} has invalid priority '{pri}'")
        FEATURE_DEFS[fid] = pri
    for path in TEXT:
        for i, line in lines(path):
            for fid in re.findall(r"\bF-[A-Z]+-\d+\b", line):
                if fid not in FEATURE_DEFS:
                    err("features", path, i, f"{fid} is not defined in features.md")


# ---------------------------------------------------------------- p0 and milestones
def milestone_table(path: Path) -> dict[str, tuple[int, list[str]]]:
    """Milestone rows: 'M1 Foundation' -> (line, cells)."""
    return {cells[0]: (i, cells) for i, cells in table_rows(path, r"M\d+ .+")}


def check_p0_and_milestones() -> None:
    p0 = {f for f, p in FEATURE_DEFS.items() if p == "P0"}
    ms = milestone_table(FEATURES_MD)
    seen: dict[str, str] = {}
    for name, (i, cells) in ms.items():
        for fid in re.findall(r"F-[A-Z]+-\d+", cells[-1]):
            if fid in seen:
                err("p0", FEATURES_MD, i, f"{fid} is in both {seen[fid]} and {name}")
            seen[fid] = name
            if FEATURE_DEFS.get(fid) != "P0":
                err("p0", FEATURES_MD, i, f"{fid} is in {name} but is not P0")
    for fid in sorted(p0 - seen.keys()):
        err("p0", FEATURES_MD, 0, f"P0 feature {fid} is in no milestone")

    for path in TEXT:
        for i, line in lines(path):
            for m in re.finditer(r"P0 count:\s*(\d+)|(\d+) P0 features", strip_struck(line)):
                n = int(m.group(1) or m.group(2))
                if n != len(p0):
                    err("p0", path, i, f"says {n} P0 features, features.md defines {len(p0)}")

    names = set(ms)
    tasks_md = DOCS / "tasks.md"
    task_ms = {line[4:].strip() for _, line in lines(tasks_md) if re.match(r"### M\d+ ", line)}
    if task_ms != names:
        err("milestones", tasks_md, 0,
            f"milestone headings {sorted(task_ms)} differ from features.md {sorted(names)}")
    readme = ROOT / "README.md"
    readme_ms = set(milestone_table(readme))
    if readme_ms and readme_ms != names:
        err("milestones", readme, 0,
            f"milestones {sorted(readme_ms)} differ from features.md {sorted(names)}")


# ---------------------------------------------------------------- A#, R#, DEC-#
def check_ids() -> None:
    dm = DOCS / "domain-model.md"
    dec = DOCS / "decisions.md"
    defined = {
        "A": {c[0] for _, c in table_rows(dm, r"A\d+")},
        "R": {c[0] for _, c in table_rows(dm, r"R\d+")},
        "DEC-": {c[0] for _, c in table_rows(dec, r"DEC-\d+")},
    }
    patterns = {"A": r"\bA\d+\b", "R": r"\bR\d+\b", "DEC-": r"\bDEC-\d+\b"}
    for path in TEXT:
        for i, line in lines(path):
            for kind, pat in patterns.items():
                for ref in re.findall(pat, line):
                    if ref not in defined[kind]:
                        err("ids", path, i, f"{ref} is not defined")


# ---------------------------------------------------------------- tasks
TASK_STATUSES = {"Todo", "Doing", "Done"}


def check_tasks() -> None:
    tasks_md = DOCS / "tasks.md"
    rows = list(table_rows(tasks_md, r"(?:P|D)\d+|M\d+-\d+"))
    ids = {cells[0] for _, cells in rows}
    for i, cells in rows:
        if cells[-1] not in TASK_STATUSES:
            err("tasks", tasks_md, i, f"{cells[0]} has invalid status '{cells[-1]}'")
        for dep in re.findall(r"\b(?:P|D|M)\d+(?:-\d+)?\b", cells[-2]):
            if dep not in ids:
                err("tasks", tasks_md, i, f"{cells[0]} depends on unknown task {dep}")
    for path in TEXT:
        for i, line in lines(path):
            refs = re.findall(r"\b[Tt]asks?\s+((?:P|D|M)\d+(?:-\d+)?)", line)
            refs += re.findall(r"\bM\d+-\d+\b", line)
            for ref in refs:
                if ref not in ids:
                    err("tasks", path, i, f"unknown task {ref}")


# ---------------------------------------------------------------- decisions
DEC_STATUSES = {"Accepted", "Proposed", "Open", "Superseded"}


def check_decisions() -> None:
    dec = DOCS / "decisions.md"
    sections = {m.group(1) for _, line in lines(dec) if (m := re.match(r"### (DEC-\d+) ", line))}
    seen = set()
    for i, cells in table_rows(dec, r"DEC-\d+"):
        if len(cells) < 3:  # rows of the Open table have no status column
            continue
        did, status = cells[0], cells[2]
        if did in seen:
            continue
        seen.add(did)
        if status not in DEC_STATUSES:
            err("decisions", dec, i, f"{did} has invalid status '{status}'")
        if status in ("Accepted", "Proposed") and did not in sections:
            err("decisions", dec, i, f"{did} is {status} but has no ### section")


def main() -> int:
    check_links()
    check_sections()
    check_features()
    check_p0_and_milestones()
    check_ids()
    check_tasks()
    check_decisions()
    for e in errors:
        print(e)
    if errors:
        print(f"\n{len(errors)} problem(s) found.")
        return 1
    print(f"All checks passed ({len(FEATURE_DEFS)} features, {len(TEXT)} files).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
