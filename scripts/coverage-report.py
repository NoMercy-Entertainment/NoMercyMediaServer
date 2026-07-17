#!/usr/bin/env python3
"""Merge every cobertura report under coverage/raw/ and rank what is uncovered.

Each test assembly instruments the whole solution but only exercises the part of
src/ it touches, so a per-assembly percentage is meaningless on its own: a file
covered by NoMercy.Tests.Api reads as 0% in NoMercy.Tests.Queue's report. The
merge here is a union -- a line counts as covered if ANY assembly hit it, and a
branch if any assembly took it.

Outputs:
  coverage/merged.json   per-file covered/total lines plus the uncovered line
                         numbers, for tooling and for handing a precise gap to
                         a test author.
  stdout                 totals, then the per-project and per-file rankings by
                         uncovered line count.

Usage:
  python scripts/coverage-report.py [--top N] [--project NoMercy.Api] [--json-only]
"""

from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
RAW = REPO / "coverage" / "raw"
MERGED = REPO / "coverage" / "merged.json"

# A line's coverage is the union across assemblies; a file that no assembly
# instruments never appears at all, which is itself a finding worth surfacing.
Hits = dict[str, dict[int, int]]
Branches = dict[str, dict[int, tuple[int, int]]]


MARKER = "/nomercy-media-server/"


def source_roots(root: ET.Element) -> list[str]:
    """The <sources> roots a report's class filenames are relative to.

    Coverlet emits `<source>C:\\...\\nomercy-media-server\\src\\</source>` and then
    writes every class filename RELATIVE to it (`NoMercy.Api\\Controllers\\X.cs`).
    Resolving against this root is what turns those back into repo-relative paths;
    treating the filename as already-absolute silently drops every file.
    """
    return [(s.text or "").replace("\\", "/") for s in root.iter("source")]


def normalize(filename: str, roots: list[str]) -> str:
    """Key on the repo-relative path (`src/NoMercy.Api/...`), whether the report
    wrote an absolute filename or one relative to a <sources> root."""
    path = filename.replace("\\", "/")

    if MARKER in path:
        return path.split(MARKER, 1)[1]

    for root in roots:
        if MARKER not in root:
            continue
        prefix = root.split(MARKER, 1)[1].lstrip("/")
        candidate = f"{prefix.rstrip('/')}/{path.lstrip('/')}" if prefix else path
        return candidate

    return path


def is_source(path: str) -> bool:
    """Only src/ counts. Tests, generated migrations and benchmarks are noise:
    migrations are EF-authored and rewritten wholesale, so a test pinning them
    asserts the generator, not our intent."""
    return path.startswith("src/") and "/Migrations/" not in path


def collect(hits: Hits, branches: Branches, report: Path) -> None:
    try:
        root = ET.parse(report).getroot()
    except ET.ParseError as exc:
        print(f"  ! unparseable, skipped: {report} ({exc})", file=sys.stderr)
        return

    roots = source_roots(root)
    for cls in root.iter("class"):
        path = normalize(cls.get("filename", ""), roots)
        if not is_source(path):
            continue
        for line in cls.iter("line"):
            number = int(line.get("number", 0))
            count = int(line.get("hits", 0))
            file_hits = hits.setdefault(path, {})
            file_hits[number] = max(file_hits.get(number, 0), count)

            if line.get("branch") == "True":
                # e.g. "50% (1/2)" -- the covered count is what merges.
                condition = line.get("condition-coverage", "")
                if "(" in condition:
                    taken, total = condition.split("(", 1)[1].rstrip(")").split("/")
                    file_branches = branches.setdefault(path, {})
                    prev_taken, prev_total = file_branches.get(number, (0, 0))
                    file_branches[number] = (
                        max(prev_taken, int(taken)),
                        max(prev_total, int(total)),
                    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--top", type=int, default=40)
    parser.add_argument("--project", default=None)
    parser.add_argument("--json-only", action="store_true")
    args = parser.parse_args()

    reports = sorted(RAW.rglob("*.cobertura.xml"))
    if not reports:
        print(f"No cobertura reports under {RAW}. Run scripts/run-coverage.sh first.")
        return 1

    hits: Hits = {}
    branches: Branches = {}
    for report in reports:
        collect(hits, branches, report)

    per_file = {}
    for path, lines in hits.items():
        uncovered = sorted(n for n, count in lines.items() if count == 0)
        file_branches = branches.get(path, {})
        branch_total = sum(total for _, total in file_branches.values())
        branch_taken = sum(taken for taken, _ in file_branches.values())
        per_file[path] = {
            "total": len(lines),
            "covered": len(lines) - len(uncovered),
            "uncovered": len(uncovered),
            "uncovered_lines": uncovered,
            "branch_total": branch_total,
            "branch_taken": branch_taken,
            "partial_branch_lines": sorted(
                n for n, (taken, total) in file_branches.items() if 0 < taken < total
            ),
        }

    MERGED.parent.mkdir(parents=True, exist_ok=True)
    MERGED.write_text(json.dumps(per_file, indent=1), encoding="utf-8")

    if args.json_only:
        print(f"wrote {MERGED}")
        return 0

    total = sum(f["total"] for f in per_file.values())
    covered = sum(f["covered"] for f in per_file.values())
    b_total = sum(f["branch_total"] for f in per_file.values())
    b_taken = sum(f["branch_taken"] for f in per_file.values())

    print(f"reports merged : {len(reports)}")
    print(f"src files      : {len(per_file)}")
    pct = 100 * covered / total if total else 0
    print(f"line coverage  : {covered}/{total}  ({pct:.2f}%)   uncovered={total - covered}")
    bpct = 100 * b_taken / b_total if b_total else 0
    print(f"branch coverage: {b_taken}/{b_total}  ({bpct:.2f}%)   untaken={b_total - b_taken}")

    by_project: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for path, f in per_file.items():
        project = path.split("/")[1] if len(path.split("/")) > 1 else "?"
        by_project[project][0] += f["covered"]
        by_project[project][1] += f["total"]

    print("\n=== per project (uncovered  covered/total  pct  project) ===")
    for project, (cov, tot) in sorted(by_project.items(), key=lambda kv: kv[1][0] - kv[1][1]):
        print(f"{tot - cov:7d}  {cov:6d}/{tot:<6d}  {100 * cov / tot if tot else 0:6.2f}%  {project}")

    ranked = sorted(per_file.items(), key=lambda kv: -kv[1]["uncovered"])
    if args.project:
        ranked = [kv for kv in ranked if f"/{args.project}/" in kv[0]]

    print(f"\n=== top {args.top} files by uncovered lines ===")
    for path, f in ranked[: args.top]:
        pct = 100 * f["covered"] / f["total"] if f["total"] else 0
        print(f"{f['uncovered']:6d}  {pct:6.2f}%  {path}")

    zero = [p for p, f in per_file.items() if f["covered"] == 0 and f["total"] > 0]
    print(f"\n=== files with ZERO covered lines: {len(zero)} ===")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
