#!/usr/bin/env python3
"""Checks docs/requirements/traceability-register.csv against the requirement baseline.

- Every requirement defined in docs/requirements/powerhub-v2/ has exactly one row, and
  every row names a defined requirement.
- Result is one of the allowed values; a Passed row names its evidence, environment and
  revision.
- Every verification reference resolves to a test, CI step, smoke check or file that
  exists, so a renamed or deleted test cannot leave a row claiming evidence it lost.

Reference forms (separated by "; "):
  identity:Class.Method   test in src/services/identity/PowerHub.Identity.Tests/Class.cs
  device:Class.Method     test in src/services/device/PowerHub.Device.Tests/Class.cs
  contract:Class.Method   test in tests/contract/PowerHub.Contracts.Tests/Class.cs
  web:file#title          test title in a frontend test file under src/frontend/src
  smoke:text              check in tests/integration/staging-smoke.sh
  ci:text                 text in a workflow under .github/workflows
  file:path               file or directory, for inspection evidence

Usage: python3 scripts/check-traceability.py  (from the repository root)
"""
import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REGISTER = ROOT / "docs/requirements/traceability-register.csv"
REQUIREMENT = re.compile(r"^\s*-\s+\*\*([A-Z]{1,5}(?:-[A-Z]{2,6})?-\d{3}):\*\*")
RESULTS = {"Pending", "Passed", "Failed", "Excepted", "Not applicable"}
TEST_DIRS = {
    "identity": ROOT / "src/services/identity/PowerHub.Identity.Tests",
    "device": ROOT / "src/services/device/PowerHub.Device.Tests",
    "contract": ROOT / "tests/contract/PowerHub.Contracts.Tests",
}


def text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def resolves(ref: str) -> bool:
    kind, _, target = ref.partition(":")
    if kind in TEST_DIRS:
        cls, _, method = target.partition(".")
        source = TEST_DIRS[kind] / f"{cls}.cs"
        return bool(method) and source.is_file() and re.search(rf"\b{re.escape(method)}\s*\(", text(source)) is not None
    if kind == "web":
        name, _, title = target.partition("#")
        return any(f"'{title}'" in text(p) for p in (ROOT / "src/frontend/src").rglob(name))
    if kind == "smoke":
        return target in text(ROOT / "tests/integration/staging-smoke.sh")
    if kind == "ci":
        return any(target in text(p) for p in (ROOT / ".github/workflows").glob("*.yml"))
    if kind == "file":
        return (ROOT / target).exists()
    return False


def main() -> int:
    defined = []
    for doc in sorted((ROOT / "docs/requirements/powerhub-v2").glob("*.md")):
        defined += [m.group(1) for line in text(doc).splitlines() if (m := REQUIREMENT.match(line))]

    errors = [f"{rid} is defined more than once" for rid in sorted({r for r in defined if defined.count(r) > 1})]
    with REGISTER.open(encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))

    listed = [row["Requirement ID"] for row in rows]
    errors += [f"{rid} has no register row" for rid in sorted(set(defined) - set(listed))]
    errors += [f"{rid} is not a defined requirement" for rid in sorted(set(listed) - set(defined))]
    errors += [f"{rid} has more than one register row" for rid in sorted({r for r in listed if listed.count(r) > 1})]

    for row in rows:
        rid, result = row["Requirement ID"], row["Result"]
        if result not in RESULTS:
            errors.append(f"{rid}: unknown result {result!r}")
        if result == "Passed" and not (row["Verification"] and row["Environment"] and row["Last verified"]):
            errors.append(f"{rid}: Passed needs verification, environment and last verified")
        if result == "Excepted" and not row["Exception"]:
            errors.append(f"{rid}: Excepted needs an exception reference")
        for ref in filter(None, (r.strip() for r in row["Verification"].split(";"))):
            if not resolves(ref):
                errors.append(f"{rid}: evidence not found: {ref}")

    for error in errors:
        print(error, file=sys.stderr)
    passed = sum(row["Result"] == "Passed" for row in rows)
    print(f"{len(rows)} requirements, {passed} passed, {len(errors)} problem(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
