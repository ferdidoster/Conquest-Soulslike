"""
Builds the modular desert kit (one FBX per piece) and the boss arena layout for Unity.

    blender --background --python Tools/Blender/build_arena_kit.py -- \
        --out Assets/_Project/Art/Environment/DesertKit [--preview <folder>] [--only Pillar_Banner,...]

Writes:
    <out>/Meshes/<Piece>.fbx         one model per kit piece
    <out>/DesertKit.json             palette + piece list (collider, light, category, notes)
    <out>/Layouts/BossArena.json     the arena from the concept art, in Unity coordinates
    <out>/Source~/DesertKit.blend    every piece laid out in a grid plus the assembled arena
                                     (the ~ keeps Unity from importing it)

Everything is generated: edit the modules in ./arena and re-run. Unity picks the changes up
through Conquest > Build Desert Arena (or automatically, see ConquestAutoSetup).
"""

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from arena import export, kit, layout, palette  # noqa: E402
from arena import pieces_architecture, pieces_nature, pieces_props, pieces_tech  # noqa: E402,F401

UNITY_ROOT_MARKER = "Assets/"


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default="Assets/_Project/Art/Environment/DesertKit")
    parser.add_argument("--preview", default=None, help="Render preview images into this folder.")
    parser.add_argument("--only", default=None, help="Comma-separated piece names to rebuild.")
    parser.add_argument("--no-blend", action="store_true")
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
    for i, (name, p) in enumerate(kit.PIECES.items()):
        obj = kit.build(name, kit_col)
        objects[name] = obj
        fbx = os.path.join(out_dir, "Meshes", f"{name}.fbx")
        if only is None or name in only:
            export.export_piece(obj, fbx)
        entries.append(kit.manifest_entry(p, asset_path(out_dir, "Meshes", f"{name}.fbx")))
        print(f"  {name}: {len(obj.data.polygons)} faces")

    export.write_json(os.path.join(out_dir, "DesertKit.json"), {
        "palette": palette.manifest(),
        "pieces": entries,
    })

    placements, spawns, blockers = layout.build()
    export.write_json(os.path.join(out_dir, "Layouts", "BossArena.json"),
                      export.layout_json("BossArena", placements, spawns, blockers))

    arena_col = assemble_arena(objects, placements)
    lay_out_kit_grid(objects)

    if args.preview:
        from arena import preview
        preview.render(args.preview, objects, arena_col, kit_col)

    if not args.no_blend:
        blend_dir = os.path.join(out_dir, "Source~")
        os.makedirs(blend_dir, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, "DesertKit.blend"))
    print(f"Exported {len(entries)} kit pieces and a {len(placements)}-piece arena to {out_dir}")


def assemble_arena(objects, placements):
    """Linked duplicates of the kit placed like the Unity arena (for previews and hand edits)."""
    import math
    col = bpy.data.collections.new("BossArena")
    bpy.context.scene.collection.children.link(col)
    for name, group, loc, rot, scale in placements:
        inst = objects[name].copy()
        inst.location = loc
        inst.rotation_euler = (0, 0, math.radians(rot))
        inst.scale = (scale, scale, scale)
        col.objects.link(inst)
    return col


def lay_out_kit_grid(objects):
    """Moves the kit originals onto a browsing grid far from the arena."""
    x, y, row_h = 0.0, -400.0, 0.0
    for obj in objects.values():
        xs = [c[0] for c in obj.bound_box]
        ys = [c[1] for c in obj.bound_box]
        width, depth = max(xs) - min(xs), max(ys) - min(ys)
        if width > 60:  # terrain stays at the origin, hidden
            obj.hide_set(True)
            obj.hide_render = True
            continue
        obj.location = (x - min(xs), y - max(ys), 0)
        x += width + 4
        row_h = max(row_h, depth)
        if x > 150:
            x, y, row_h = 0.0, y - row_h - 6, 0.0


if __name__ == "__main__":
    main()
