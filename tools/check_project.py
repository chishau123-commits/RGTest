"""License-free source checks. This is deliberately not a Unity build/device test."""
from __future__ import annotations

import json
import math
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
errors: list[str] = []


def check(condition: bool, message: str) -> None:
    if not condition:
        errors.append(message)


def load_json(path: Path):
    try:
        # Unity source JSON must remain portable across operating systems.
        return json.loads(path.read_text(encoding="utf-8-sig"), parse_constant=reject_constant)
    except (ValueError, OSError) as exc:
        errors.append(f"{path.relative_to(ROOT)}: invalid JSON: {exc}")
        return None


def reject_constant(value: str):
    raise ValueError(f"non-standard JSON numeric constant {value}")


def finite_values(value, source: str) -> None:
    if isinstance(value, float):
        check(math.isfinite(value), f"{source}: non-finite number")
    elif isinstance(value, dict):
        for child in value.values():
            finite_values(child, source)
    elif isinstance(value, list):
        for child in value:
            finite_values(child, source)


def chart_shape(chart, source: str) -> None:
    """Small, independent transport contract; gameplay validation remains in the C# compiler."""
    check(isinstance(chart, dict), f"{source}: chart must be an object")
    if not isinstance(chart, dict):
        return
    check(chart.get("schemaVersion") == "prototype-ring-0", f"{source}: unknown schemaVersion")
    timebase = chart.get("timebase")
    check(isinstance(timebase, dict), f"{source}: timebase must be an object")
    if isinstance(timebase, dict):
        ppq = timebase.get("ppq")
        check(type(ppq) is int and ppq > 0, f"{source}: ppq must be a positive integer")
        tempos = timebase.get("tempos")
        check(isinstance(tempos, list) and bool(tempos), f"{source}: tempos must be a nonempty array")
        if isinstance(tempos, list):
            for tempo in tempos:
                check(isinstance(tempo, dict), f"{source}: tempo must be an object")
                if isinstance(tempo, dict):
                    check(type(tempo.get("tick")) is int, f"{source}: tempo tick must be an integer")
                    bpm = tempo.get("bpm")
                    check(type(bpm) in (int, float) and bpm > 0, f"{source}: BPM must be positive")
    for field in ("notes", "paths", "actions", "decorations"):
        values = chart.get(field)
        check(isinstance(values, list), f"{source}: {field} must be an array")
        if not isinstance(values, list):
            continue
        ids: set[str] = set()
        for item in values:
            check(isinstance(item, dict), f"{source}: {field} item must be an object")
            if not isinstance(item, dict):
                continue
            item_id = item.get("id")
            valid_id = isinstance(item_id, str) and bool(item_id)
            check(valid_id, f"{source}: {field} item id is missing")
            if valid_id:
                check(item_id not in ids, f"{source}: duplicate {field} id {item_id}")
                ids.add(item_id)
            if field == "notes":
                check(type(item.get("tick")) is int and type(item.get("spawnTick")) is int,
                      f"{source}: note timing must use integer ticks")
                check(item.get("motion") in ("shrink", "arrival"), f"{source}: unsupported note motion")
                radius = item.get("radius")
                check(type(radius) in (int, float) and radius > 0, f"{source}: radius must be positive")
                target = item.get("target")
                check(isinstance(target, dict) and all(type(target.get(axis)) in (int, float) for axis in ("x", "y")),
                      f"{source}: target must contain numeric x/y")


version_path = ROOT / "ProjectSettings/ProjectVersion.txt"
check(version_path.is_file(), "ProjectSettings/ProjectVersion.txt is missing")
if version_path.is_file():
    check("m_EditorVersion: 2022.3.62f3c1" in version_path.read_text(encoding="utf-8"),
          "Project must use Unity 2022.3.62f3c1")

manifest = load_json(ROOT / "Packages/manifest.json")
check(isinstance(manifest, dict), "Packages/manifest.json must be an object")
if isinstance(manifest, dict):
    dependencies = manifest.get("dependencies", {})
    check(dependencies.get("com.unity.inputsystem") == "1.7.0", "Input System must be pinned to 1.7.0")
    check(dependencies.get("com.unity.test-framework") == "1.1.33", "Unity Test Framework must be pinned to 1.1.33")
    check(dependencies.get("com.unity.nuget.newtonsoft-json") == "3.2.1", "Newtonsoft JSON must be pinned to 3.2.1")
    check("com.unity.ugui" in dependencies, "UGUI package is missing")

assets = ROOT / "Assets"
check(assets.is_dir(), "Assets/ is missing")
check((assets / "RingGame/Scenes/Game.unity").is_file(), "Bootstrap Game.unity scene is missing; run Prepare")
check((assets / "RingGame/Editor/BuildCommands.cs").is_file(), "Reproducible build entry point is missing")
names: dict[str, Path] = {}
guid_owners: dict[str, Path] = {}
source_count = 0
for path in sorted(assets.rglob("*")):
    if not path.is_file():
        continue
    if path.suffix == ".meta":
        match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8"), re.M)
        check(match is not None, f"{path.relative_to(ROOT)}: missing/invalid GUID")
        if match:
            guid = match.group(1)
            check(guid not in guid_owners, f"Duplicate asset GUID: {path.relative_to(ROOT)} and {guid_owners.get(guid)}")
            guid_owners[guid] = path
        check(Path(str(path)[:-5]).exists(), f"Orphan .meta: {path.relative_to(ROOT)}")
        continue
    source_count += 1
    check(Path(str(path) + ".meta").is_file(), f"Missing .meta: {path.relative_to(ROOT)}")
    if path.suffix in (".json", ".asmdef"):
        content = load_json(path)
        finite_values(content, str(path.relative_to(ROOT)))
        if path.parent.name == "Charts" and path.suffix == ".json":
            chart_shape(content, str(path.relative_to(ROOT)))
        if path.suffix == ".asmdef" and isinstance(content, dict):
            name = content.get("name")
            check(bool(name), f"{path.relative_to(ROOT)}: assembly name is missing")
            check(name not in names, f"Duplicate assembly name {name}")
            names[name] = path
    if path.suffix == ".cs":
        # In Unity a runtime reference to UnityEditor breaks player compilation.
        if "Editor" not in path.relative_to(assets).parts and "Tests" not in path.parts:
            check(not re.search(r"^\s*using\s+UnityEditor[.;]", path.read_text(encoding="utf-8"), re.M),
                  f"{path.relative_to(ROOT)}: UnityEditor import outside Editor/Tests")

lock_path = ROOT / "Packages/packages-lock.json"
if lock_path.exists():
    load_json(lock_path)
if errors:
    print("Source contract failed:", file=sys.stderr)
    for error in errors:
        print(f"- {error}", file=sys.stderr)
    sys.exit(1)
print(f"Source contract passed: {source_count} asset files, {len(names)} assemblies, {len(guid_owners)} unique GUIDs.")
print("This does not compile C#, run Unity tests, build an APK, or verify a physical Android device.")
