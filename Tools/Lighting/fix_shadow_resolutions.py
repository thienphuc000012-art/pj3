"""Repair invalid URP Custom shadow requests without changing lights or atlas size.

URP 17 reads (int)Light.shadowResolution for Custom, NOT shadowCustomResolution.
Only Point/Spot lights with Custom tier and invalid native resolution are repaired.
Valid artist-authored Custom resolutions and main Directional shadows are kept.
"""
import argparse
import collections
import datetime
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
LIGHT_DATA_GUID = "474bcb49853aa07438625e644c072ee6"
DOCUMENT = re.compile(r"(?m)^--- !u!(\d+) &[^\r\n]+\r?\n")
OWNER = re.compile(r"(?m)^  m_GameObject: \{fileID: (\d+)\}")
TIER = re.compile(r"(?m)^(  m_AdditionalLightsShadowResolutionTier: )(-?\d+)")


def repairs(text):
    matches = list(DOCUMENT.finditer(text))
    documents = [(int(match[1]), match.start(), text[match.start():matches[i + 1].start() if i + 1 < len(matches) else len(text)])
                 for i, match in enumerate(matches)]
    lights = {}
    for kind, _, block in documents:
        if kind != 108:
            continue
        owner = OWNER.search(block)
        light_type = re.search(r"(?m)^  m_Type: (\d+)", block)
        resolution = re.search(r"(?m)^    m_Resolution: (-?\d+)", block)
        if owner and light_type and resolution:
            lights[owner[1]] = (int(light_type[1]), int(resolution[1]))
    changes = []
    for kind, offset, block in documents:
        if kind != 114 or LIGHT_DATA_GUID not in block:
            continue
        owner, tier = OWNER.search(block), TIER.search(block)
        if not owner or not tier or int(tier[2]) != -1 or owner[1] not in lights:
            continue
        light_type, resolution = lights[owner[1]]
        valid = resolution >= 128 and resolution & (resolution - 1) == 0
        if light_type in (0, 2) and not valid:
            changes.append((offset + tier.start(2), offset + tier.end(2), resolution))
    return changes


def corrected(text):
    for start, end, _ in reversed(repairs(text)):
        text = text[:start] + "0" + text[end:]
    return text


def self_test():
    def fixture(resolution=-1, tier=-1, light_type=2):
        return ("%YAML 1.1\r\n--- !u!108 &11\r\nLight:\r\n  m_GameObject: {fileID: 1}\r\n"
                f"  m_Type: {light_type}\r\n  m_Shadows:\r\n    m_Resolution: {resolution}\r\n    m_CustomResolution: 256\r\n"
                "--- !u!114 &12\r\nMonoBehaviour:\r\n  m_GameObject: {fileID: 1}\r\n"
                f"  m_Script: {{fileID: 11500000, guid: {LIGHT_DATA_GUID}, type: 3}}\r\n  m_AdditionalLightsShadowResolutionTier: {tier}\r\n")
    bad = fixture()
    assert corrected(bad) == bad.replace("m_AdditionalLightsShadowResolutionTier: -1", "m_AdditionalLightsShadowResolutionTier: 0")
    assert corrected(corrected(bad)) == corrected(bad)
    for sample in (fixture(256), fixture(1024), fixture(tier=2), fixture(light_type=1)):
        assert corrected(sample) == sample
    assert repairs(fixture(resolution=3)) and repairs(fixture(light_type=0))


def run(apply=False):
    self_test()
    backup = ROOT / "Tools/Lighting" / ("BeforeShadowFix-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f"))
    entries = []
    old_values = collections.Counter()
    for folder in (ROOT / "Assets/Scenes", ROOT / "Assets/Resources", ROOT / "Assets/Adventure"):
        for path in sorted(folder.rglob("*")):
            if path.suffix not in (".unity", ".prefab"):
                continue
            content = path.read_bytes()
            if b"m_AdditionalLightsShadowResolutionTier: -1" not in content:
                continue
            text = content.decode("utf-8")
            changes = repairs(text)
            if not changes:
                continue
            relative = path.relative_to(ROOT)
            entries.append({"path": relative.as_posix(), "lights": len(changes)})
            old_values.update(value for _, _, value in changes)
            if apply:
                destination = backup / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(content)
                path.write_bytes(corrected(text).encode("utf-8"))
                assert not repairs(path.read_text(encoding="utf-8"))
    report = {"mode": "apply" if apply else "audit", "scope": "direct_serialized_lights_only; use Unity resolved audit for inherited prefab lights", "files": len(entries), "lights": sum(x["lights"] for x in entries),
              "invalid_native_resolutions": dict(old_values), "entries": entries, "backup": str(backup) if apply else None}
    output = ROOT / "Library/CombatValidation" / ("ShadowResolutionRepair.json" if apply else "ShadowResolutionAudit.json")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "entries"}, indent=2))


def run_resolved(report_path, apply=False):
    """Use Unity-resolved component IDs, including stripped and inherited prefab lights."""
    source_report = json.loads(Path(report_path).read_text(encoding="utf-8"))
    assert not source_report.get("error"), source_report.get("error")
    groups = collections.defaultdict(list)
    for entry in source_report["lights"]:
        resolution = entry["nativeResolution"]
        assert entry["tier"] == -1 and not (resolution >= 128 and resolution & (resolution - 1) == 0)
        groups[entry["scene"]].append(entry)
    backup = ROOT / "Tools/Lighting" / ("BeforePrefabShadowFix-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f"))
    prepared = []
    for relative, entries in groups.items():
        path = (ROOT / relative).resolve()
        assert path.is_relative_to(ROOT / "Assets/Scenes"), path
        content = path.read_bytes()
        text = content.decode("utf-8")
        docs = list(DOCUMENT.finditer(text))
        blocks = {int(match.group(0).split("&", 1)[1].split()[0]): (match.start(), text[match.start():docs[i + 1].start() if i + 1 < len(docs) else len(text)])
                  for i, match in enumerate(docs) if int(match[1]) == 114}
        edits = []
        for entry in entries:
            identity = entry["componentId"]
            if identity in blocks:
                offset, block = blocks[identity]
                tier = TIER.search(block)
                assert LIGHT_DATA_GUID in block and tier and tier[2] == "-1", (relative, identity)
                edits.append((offset + tier.start(2), offset + tier.end(2)))
            else:
                override = list(re.finditer(r"(?m)^    - target: \{fileID: " + str(identity) +
                    r",[^\r\n]+\}\r?\n      propertyPath: m_AdditionalLightsShadowResolutionTier\r?\n      value: (-1)\r?$", text))
                assert len(override) == 1, (relative, identity, "ambiguous inherited override")
                edits.append((override[0].start(1), override[0].end(1)))
        assert len(edits) == len(set(edits)), (relative, "duplicate edits")
        for start, end in sorted(edits, reverse=True):
            text = text[:start] + "0" + text[end:]
        prepared.append((path, Path(relative), content, text.encode("utf-8"), len(edits)))
    # Validate every ID before modifying any file.
    if apply:
        for path, relative, content, fixed, _ in prepared:
            destination = backup / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(content)
            path.write_bytes(fixed)
    result = {"mode": "apply" if apply else "audit", "files": len(prepared), "lights": sum(x[4] for x in prepared),
              "backup": str(backup) if apply else None, "source_report": str(report_path)}
    output = ROOT / "Library/CombatValidation/ShadowPrefabResolutionRepair.json"
    output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--resolved-report", help="JSON from Unity's Audit Resolved Chunk Shadows menu")
    args = parser.parse_args()
    if args.resolved_report:
        run_resolved(args.resolved_report, args.apply)
    else:
        run(args.apply)
