"""
Builds the modular underground laboratory kit (one FBX per piece) and the lab demo level layout
for Unity.

    blender --background --python Tools/Blender/build_lab_kit.py -- \
        --out Assets/_Project/Art/Environment/LabKit [--preview <folder>] [--only Gate_Blast,...]

Writes:
    <out>/Meshes/<Piece>.fbx         one model per kit piece (moving door leaves as child objects)
    <out>/LabKit.json                palette + piece list (collider, lights, gate, camera fade, notes)
    <out>/Layouts/LabDemo.json       the level from the sketch, in Unity coordinates, with gate ids
    <out>/Source~/LabKit.blend       every piece laid out in a grid plus the assembled level
                                     (the ~ keeps Unity from importing it)

Everything is generated: edit the modules in ./lab and re-run. Unity picks the changes up
through Conquest > Build Lab Demo.
"""

import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from lab import export, kit, layout, palette, sections  # noqa: E402
from lab import pieces_props, pieces_structure, pieces_tech  # noqa: E402,F401

UNITY_ROOT_MARKER = "Assets/"


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default="Assets/_Project/Art/Environment/LabKit")
    parser.add_argument("--preview", default=None, help="Render preview images into this folder.")
    parser.add_argument("--only", default=None, help="Comma-separated piece names to rebuild.")
    parser.add_argument("--no-blend", action="store_true")
    parser.add_argument("--no-fbx", action="store_true")
    return parser.parse_args(argv)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def asset_path(out_dir, *parts):
    """Project-relative path for the manifest (Unity loads assets by 'Assets/...' paths)."""
    full = os.path.join(out_dir, *parts).replace("\\", "/")
    i = full.find(UNITY_ROOT_MARKER)
    return full[i:] if i >= 0 else full


def main():
    args = parse_args()
    out_dir = os.path.abspath(args.out)
    reset_scene()

    only = set(args.only.split(",")) if args.only else None
    kit_col = bpy.data.collections.new("Kit")
    bpy.context.scene.collection.children.link(kit_col)

    objects, entries = {}, []
    for name, p in kit.PIECES.items():
        objs = kit.build(name, kit_col)
        objects[name] = objs
        fbx = os.path.join(out_dir, "Meshes", f"{name}.fbx")
        if not args.no_fbx and (only is None or name in only):
            export.export_piece(objs, fbx)
        entries.append(kit.manifest_entry(p, asset_path(out_dir, "Meshes", f"{name}.fbx")))
        faces = sum(len(o.data.polygons) for o in objs)
        print(f"  {name}: {faces} faces")

    export.write_json(os.path.join(out_dir, "LabKit.json"), {
        "palette": palette.manifest(),
        "pieces": entries,
    })

    placements, spawns, blockers, gates = layout.build()
    unknown = {p["piece"] for p in placements} - set(objects)
    if unknown:
        raise SystemExit(f"layout uses unknown pieces: {sorted(unknown)}")
    sections.tag(placements)
    export.write_json(os.path.join(out_dir, "Layouts", "LabDemo.json"),
                      export.layout_json("LabDemo", placements, spawns, blockers, gates, sections.section_map()))

    level_col = assemble(objects, placements)
    lay_out_kit_grid(objects)

    if args.preview:
        from lab import preview
        preview.render(args.preview, objects, level_col, kit_col, spawns)

    if not args.no_blend:
        blend_dir = os.path.join(out_dir, "Source~")
        os.makedirs(blend_dir, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, "LabKit.blend"))
    print(f"Exported {len(entries)} kit pieces and a {len(placements)}-piece level to {out_dir}")


def assemble(objects, placements):
    """Linked duplicates of the kit placed like the Unity level (for previews and hand edits)."""
    col = bpy.data.collections.new("LabDemo")
    bpy.context.scene.collection.children.link(col)
    for p in placements:
        for src in objects[p["piece"]]:
            if "_COL_" in src.name:
                continue
            inst = src.copy()
            inst.location = p["loc"]
            inst.rotation_euler = (0, 0, math.radians(p["rot"]))
            inst.scale = (p.get("sx", 1.0), 1.0, 1.0)
            col.objects.link(inst)
    return col


def lay_out_kit_grid(objects):
    """Moves the kit originals onto a browsing grid far from the level."""
    x, y, row_h = 0.0, -200.0, 0.0
    for objs in objects.values():
        main = objs[0]
        xs = [c[0] for o in objs for c in o.bound_box]
        ys = [c[1] for o in objs for c in o.bound_box]
        width, depth = max(xs) - min(xs), max(ys) - min(ys)
        if width > 40:  # base slab stays hidden
            for o in objs:
                o.hide_set(True)
                o.hide_render = True
            continue
        for o in objs:
            o.location = (x - min(xs), y - max(ys), 0)
            if "_COL_" in o.name:
                o.hide_render = True
                o.display_type = "WIRE"
        x += width + 2
        row_h = max(row_h, depth)
        if x > 90:
            x, y, row_h = 0.0, y - row_h - 4, 0.0


if __name__ == "__main__":
    main()
