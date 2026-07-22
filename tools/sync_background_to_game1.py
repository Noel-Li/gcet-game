#!/usr/bin/env python3
"""Merge background.unity into game1.unity without losing gameplay wiring."""

from __future__ import annotations

import argparse
import re
from dataclasses import dataclass, replace
from pathlib import Path


HEADER = re.compile(r"(?m)^--- !u!(\d+) &(\d+)( stripped)?\n")
ROOTS_TYPE = 1660057539
TRANSFORM_TYPES = {4, 224}

EXCLUDED_SOURCE_ROOTS = {
    "Area_Left",
    "Area_Right",
    "Area_Top",
    "PauseController",
}

GAME1_ONLY_ROOTS = {
    "R1",
    "R2",
    "R3",
    "R4",
    "InvisibleWall",
    "InvisibleWall (1)",
    "InvisibleWall (2)",
    "ReviewBookController",
    "Controls Hint Canvas",
    "Opening Location Overlay",
    "Review Book Hint Canvas",
    "TX Tileset Wall_8 (3)",
    "TX Tileset Wall_8 (4)",
}

PROTECTED_ROOTS = {
    "Player",
    "Soldier",
    "Silk Seller",
    "Trader",
    "guopinyuMain_0",
    "motherMain_0",
    "Main Camera",
    "EventSystem",
}

AUTHORED_CHARACTER_ROOTS = {
    "Player",
    "Soldier",
    "Silk Seller",
    "Trader",
    "guopinyuMain_0",
    "motherMain_0",
}


@dataclass(frozen=True)
class ObjectBlock:
    type_id: int
    file_id: int
    stripped: bool
    body: str

    def serialize(self) -> str:
        suffix = " stripped" if self.stripped else ""
        return f"--- !u!{self.type_id} &{self.file_id}{suffix}\n{self.body}"


class Scene:
    def __init__(self, path: Path) -> None:
        self.path = path
        text = path.read_text(encoding="utf-8")
        matches = list(HEADER.finditer(text))
        if not matches:
            raise ValueError(f"No Unity YAML objects in {path}")
        self.preamble = text[: matches[0].start()]
        self.objects: dict[int, ObjectBlock] = {}
        self.order: list[int] = []
        for index, match in enumerate(matches):
            end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
            file_id = int(match.group(2))
            if file_id in self.objects:
                raise ValueError(f"Duplicate fileID {file_id} in {path}")
            self.objects[file_id] = ObjectBlock(
                int(match.group(1)),
                file_id,
                bool(match.group(3)),
                text[match.end() : end],
            )
            self.order.append(file_id)

        self.names: dict[int, str] = {}
        self.components: dict[int, list[int]] = {}
        self.transform_go: dict[int, int] = {}
        self.children: dict[int, list[int]] = {}
        self.parents: dict[int, int] = {}
        self._index()

        root_blocks = [block for block in self.objects.values() if block.type_id == ROOTS_TYPE]
        if len(root_blocks) != 1:
            raise ValueError(f"Expected exactly one SceneRoots object in {path}")
        self.scene_roots = root_blocks[0]
        self.root_ids = [
            int(value)
            for value in re.findall(
                r"(?m)^  - \{fileID: (\d+)\}", self.scene_roots.body
            )
        ]
        self.root_names: dict[str, int] = {}
        for transform_id in self.root_ids:
            name = self.names[self.transform_go[transform_id]]
            if name in self.root_names:
                raise ValueError(f"Duplicate root name {name!r} in {path}")
            self.root_names[name] = transform_id

    def _index(self) -> None:
        for file_id, block in self.objects.items():
            if block.type_id == 1:
                name = re.search(r"(?m)^  m_Name: (.*)$", block.body)
                if not name:
                    raise ValueError(f"Unnamed GameObject {file_id}")
                self.names[file_id] = name.group(1)
                self.components[file_id] = [
                    int(value)
                    for value in re.findall(
                        r"(?m)^  - component: \{fileID: (\d+)\}", block.body
                    )
                ]
            elif block.type_id in TRANSFORM_TYPES:
                go = re.search(
                    r"(?m)^  m_GameObject: \{fileID: (\d+)\}", block.body
                )
                parent = re.search(
                    r"(?m)^  m_Father: \{fileID: (\d+)\}", block.body
                )
                child_section = re.search(
                    r"(?ms)^  m_Children:\n(.*?)(?=^  m_Father:)", block.body
                )
                if not go or not parent:
                    raise ValueError(f"Incomplete Transform {file_id}")
                self.transform_go[file_id] = int(go.group(1))
                self.parents[file_id] = int(parent.group(1))
                self.children[file_id] = (
                    [
                        int(value)
                        for value in re.findall(
                            r"(?m)^  - \{fileID: (\d+)\}", child_section.group(1)
                        )
                    ]
                    if child_section
                    else []
                )

    def subtree(self, root_name: str) -> set[int]:
        result: set[int] = set()
        pending = [self.root_names[root_name]]
        while pending:
            transform_id = pending.pop()
            go_id = self.transform_go[transform_id]
            result.add(go_id)
            result.update(self.components[go_id])
            pending.extend(self.children.get(transform_id, []))
        return result

    def owned_ids(self) -> set[int]:
        result: set[int] = set()
        for name in self.root_names:
            result.update(self.subtree(name))
        return result

    def position(self, root_name: str) -> tuple[str, str]:
        body = self.objects[self.root_names[root_name]].body
        match = re.search(
            r"(?m)^  m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: [^}]+\}", body
        )
        if not match:
            raise ValueError(f"No position for {root_name}")
        return match.group(1), match.group(2)


def merge_transform(target: ObjectBlock, source: ObjectBlock) -> ObjectBlock:
    body = target.body
    for field in (
        "m_LocalRotation",
        "m_LocalPosition",
        "m_LocalScale",
        "m_ConstrainProportionsScale",
        "m_LocalEulerAnglesHint",
    ):
        source_line = re.search(rf"(?m)^  {field}: .*$", source.body)
        target_line = re.search(rf"(?m)^  {field}: .*$", body)
        if source_line and target_line:
            body = body[: target_line.start()] + source_line.group(0) + body[target_line.end() :]
    return replace(target, body=body)


def build(source: Scene, target: Scene) -> tuple[str, list[str]]:
    missing_source = (EXCLUDED_SOURCE_ROOTS | PROTECTED_ROOTS) - source.root_names.keys()
    missing_target = (GAME1_ONLY_ROOTS | PROTECTED_ROOTS) - target.root_names.keys()
    if missing_source or missing_target:
        raise ValueError(
            f"Missing roots; source={sorted(missing_source)}, target={sorted(missing_target)}"
        )

    final: dict[int, ObjectBlock] = {}
    target_owned = target.owned_ids()
    for file_id in target.order:
        if file_id not in target_owned and file_id != target.scene_roots.file_id:
            final[file_id] = target.objects[file_id]

    for name in source.root_names:
        if name in EXCLUDED_SOURCE_ROOTS or name in PROTECTED_ROOTS:
            continue
        for file_id in source.subtree(name):
            if file_id in final and final[file_id] != source.objects[file_id]:
                raise ValueError(f"Source fileID collision: {file_id}")
            final[file_id] = source.objects[file_id]

    for name in PROTECTED_ROOTS:
        for file_id in target.subtree(name):
            if file_id in final:
                raise ValueError(f"Protected fileID collision: {file_id}")
            final[file_id] = target.objects[file_id]

        if name not in AUTHORED_CHARACTER_ROOTS:
            continue
        source_transform = source.root_names[name]
        target_transform = target.root_names[name]
        final[target_transform] = merge_transform(
            final[target_transform], source.objects[source_transform]
        )
        source_go = source.transform_go[source_transform]
        target_go = target.transform_go[target_transform]
        target_components = set(target.components[target_go])
        for component_id in source.components[source_go]:
            component = source.objects[component_id]
            if component_id in target_components and component.type_id in {61, 212}:
                final[component_id] = component

    for name in GAME1_ONLY_ROOTS:
        for file_id in target.subtree(name):
            if file_id in final:
                raise ValueError(f"game1-only fileID collision: {file_id}")
            final[file_id] = target.objects[file_id]

    source_order = [source.names[source.transform_go[file_id]] for file_id in source.root_ids]
    target_order = [target.names[target.transform_go[file_id]] for file_id in target.root_ids]
    final_names = [name for name in source_order if name not in EXCLUDED_SOURCE_ROOTS]
    final_names.extend(name for name in target_order if name in GAME1_ONLY_ROOTS)
    final_root_ids = [
        source.root_names[name] if name in source.root_names else target.root_names[name]
        for name in final_names
    ]

    roots_body = re.sub(
        r"(?ms)(^  m_Roots:\n).*$",
        lambda match: match.group(1)
        + "".join(f"  - {{fileID: {file_id}}}\n" for file_id in final_root_ids),
        target.scene_roots.body,
    )
    final[target.scene_roots.file_id] = replace(target.scene_roots, body=roots_body)

    output_order: list[int] = []
    for file_id in target.order + source.order:
        if (
            file_id in final
            and file_id not in output_order
            and file_id != target.scene_roots.file_id
        ):
            output_order.append(file_id)
    output_order.append(target.scene_roots.file_id)
    if len(output_order) != len(final) or len(set(output_order)) != len(output_order):
        raise ValueError("Generated object ordering is invalid")

    text = target.preamble + "".join(final[file_id].serialize() for file_id in output_order)
    validate(text, final_root_ids)
    return text, final_names


def validate(text: str, expected_roots: list[int]) -> None:
    matches = list(HEADER.finditer(text))
    ids = [int(match.group(2)) for match in matches]
    if len(ids) != len(set(ids)):
        raise ValueError("Generated scene contains duplicate fileIDs")
    known = set(ids)
    dangling: set[int] = set()
    for match in re.finditer(r"\{fileID: (-?\d+)([^}]*)\}", text):
        file_id = int(match.group(1))
        if file_id > 0 and "guid:" not in match.group(2) and file_id not in known:
            dangling.add(file_id)
    if dangling:
        raise ValueError(f"Dangling internal fileIDs: {sorted(dangling)}")
    roots_match = next(match for match in matches if int(match.group(1)) == ROOTS_TYPE)
    roots = [
        int(value)
        for value in re.findall(r"(?m)^  - \{fileID: (\d+)\}", text[roots_match.end() :])
    ]
    if roots != expected_roots:
        raise ValueError("Generated SceneRoots list is incorrect")


def update_notes(repo: Path, source: Scene, final_names: list[str]) -> None:
    path = repo / "AGENTS.md"
    text = path.read_text(encoding="utf-8")
    imported = len(source.root_names) - len(EXCLUDED_SOURCE_ROOTS)

    def count(prefix: str) -> int:
        return sum(name.startswith(prefix) for name in source.root_names)

    game_line = re.search(
        r"(?m)^- \*\*`game1\.unity`\*\* — the current playable scene\..*$", text
    )
    if not game_line:
        raise ValueError("Missing game1 scene note")
    summary = (
        f"Its visual map is synchronized from {imported} of the {len(source.root_names)} "
        "roots in `background.unity`; the omitted roots are `Area_Left`, `Area_Right`, "
        f"`Area_Top`, and `PauseController`. {len(GAME1_ONLY_ROOTS)} game-only roots "
        f"remain, for **{len(final_names)} scene roots total**. The revised map includes "
        f"{count('stoneWall_0')} `stoneWall_0*` boundaries, {count('StoneUP_0')} "
        f"`StoneUP_0*` boundaries, {count('straightRiver_0')} straight-river pieces, "
        f"{count('riverCurveNew_0')} river curves, {count('grass_0')} large grass "
        f"patches, {count('bigHouse_0')} large houses, {count('lilHouse_0')} small "
        f"houses, the rice farm and border, {count('newFence_0')} new fence pieces, "
        f"{count('whitefleur') + count('rougefleur')} red/white flower props, bridges, "
        "trees, the market/arch set, and shared character roots. Every non-gameplay "
        "source subtree—including authored building and environment colliders—is "
        "copied exactly. Gameplay roots retain the newer `game1` wiring and game-only "
        "UI, regions, gates, and retained wall roots. The persistent music source"
    )
    revised, replacements = re.subn(
        r"Its visual map is synchronized from .*?The persistent music source",
        summary,
        game_line.group(0),
    )
    if replacements != 1:
        raise ValueError("Could not revise the game1 summary")

    character_labels = [
        ("Player", "Player"),
        ("Soldier", "Soldier"),
        ("Silk Seller", "Silk Seller"),
        ("Trader", "Trader"),
        ("guopinyuMain_0", "`guopinyuMain_0`"),
        ("motherMain_0", "`motherMain_0`"),
        ("lilguyMain_0", "`lilguyMain_0`"),
    ]
    position_parts = []
    for name, label in character_labels:
        x, y = source.position(name)
        position_parts.append(f"{label} `({x}, {y})`")
    positions = (
        "Character root positions match `background.unity`: "
        + ", ".join(position_parts[:-1])
        + f", and {position_parts[-1]}. The `ReviewBookController`"
    )
    revised, replacements = re.subn(
        r"Character root positions match `background\.unity`: .*?"
        r"The `ReviewBookController`",
        positions,
        revised,
    )
    if replacements != 1:
        raise ValueError("Could not revise character positions")
    text = text[: game_line.start()] + revised + text[game_line.end() :]

    soldier = source.position("Soldier")
    farmer = source.position("riceFarmer_0")
    latest = (
        f"  - **Latest background resync:** `game1` contains {imported} authoritative "
        f"roots from the current {len(source.root_names)}-root `background.unity`, "
        "omitting `Area_Left`, `Area_Right`, `Area_Top`, and `PauseController`. The "
        "revised stone-boundary, river, house, rice-farm, grass, fence, flower, bridge, "
        "tree, market, and character roots match the source transforms, sprites, and "
        f"colliders. The Soldier is at `({soldier[0]}, {soldier[1]})` and the rice "
        f"farmer at `({farmer[0]}, {farmer[1]})`. The Player visual child, NPC "
        "controllers/conversations, review-book/controls/opening UI, R1–R4 region "
        "gates, tracing state, and two game-only wall roots remain intact. "
        "`tools/sync_background_to_game1.py` reproduces and validates the merge."
    )
    text, replacements = re.subn(
        r"(?m)^  - \*\*Latest background resync:\*\*.*$", latest, text
    )
    if replacements != 1:
        raise ValueError("Could not revise latest resync note")

    background = (
        f"- **`background.unity`** — the authoritative {len(source.root_names)}-root "
        "map source synchronized into `game1`. It contains the revised stone-boundary "
        "frame, river network, houses, rice farm, grass grid, fences, flower garden, "
        "bridges, trees, market/arch props, shared player/NPC roots, and four support "
        "roots omitted from gameplay synchronization (`Area_Left`, `Area_Right`, "
        "`Area_Top`, and `PauseController`)."
    )
    text, replacements = re.subn(
        r"(?m)^- \*\*`background\.unity`\*\* —.*$", background, text
    )
    if replacements != 1:
        raise ValueError("Could not revise background scene note")
    path.write_text(text, encoding="utf-8", newline="\n")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    parser.add_argument(
        "--repo", type=Path, default=Path(__file__).resolve().parents[1]
    )
    args = parser.parse_args()
    scenes = args.repo / "gcet_game_unity" / "Assets" / "Scenes"
    source = Scene(scenes / "background.unity")
    target = Scene(scenes / "game1.unity")
    result, final_names = build(source, target)
    object_count = len(HEADER.findall(result))
    print(
        f"game1: {len(target.root_ids)} -> {len(final_names)} roots; "
        f"{len(target.objects)} -> {object_count} objects"
    )
    print(
        f"background: {len(source.root_ids)} roots, "
        f"{len(source.root_ids) - len(EXCLUDED_SOURCE_ROOTS)} imported"
    )
    if args.write:
        target.path.write_text(result, encoding="utf-8", newline="\n")
        update_notes(args.repo, source, final_names)
        print(f"Wrote {target.path}")
        print(f"Updated {args.repo / 'AGENTS.md'}")
    else:
        print("Dry run passed; use --write to apply")


if __name__ == "__main__":
    main()
