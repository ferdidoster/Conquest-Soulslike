"""
Builds the knight (mesh, rig, animations) and exports an FBX for Unity.

    blender --background --python Tools/Blender/build_knight.py -- \
        --out Assets/_Project/Art/Characters/Knight/Knight.fbx [--blend Knight.blend] [--preview out_dir]
        [--model tech|classic|robot|brute|gunner] [--turnaround out_dir]

Everything is generated, so tweak the modules in ./knight and re-run instead of hand editing.
"""

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from knight import animations, brute_model, export, gunner_model, model, rig, robot_model, tech_model, weapons  # noqa: E402

# Interchangeable character models on the shared rig: (make_materials, build_mesh).
MODELS = {
    "tech": (tech_model.make_materials, tech_model.build_mesh),
    "classic": (model.make_materials, model.build_knight_mesh),
    "robot": (robot_model.make_materials, robot_model.build_mesh),  # the Sentinel enemy
    "brute": (brute_model.make_materials, brute_model.build_mesh),  # the Bulwark, a heavy robot enemy
    "gunner": (gunner_model.make_materials, gunner_model.build_mesh),  # the Warden, a stationary laser gunner
}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default="Knight.fbx")
    parser.add_argument("--blend", default=None, help="Also save the generated .blend file.")
    parser.add_argument("--preview", default=None, help="Render preview images into this folder.")
    parser.add_argument("--preview-only", action="store_true")
    parser.add_argument("--model", choices=sorted(MODELS), default="tech",
                        help="Which look to build on the shared rig (default: the tech-knight).")
    parser.add_argument("--turnaround", default=None,
                        help="Render front/side/back/3-4 views of the idle pose into this folder.")
    return parser.parse_args(argv)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = animations.FPS
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def main():
    args = parse_args()
    reset_scene()

    if args.model == "gunner":
        gunner_model.shorten_rig()  # the droid stands on short legs: its own rig proportions and clips
    armature = rig.build_armature("Knight")
    make_materials, build_mesh = MODELS[args.model]
    materials = make_materials()
    mesh = build_mesh(armature, materials)
    if args.model == "tech":
        weapons.build_all(armature, materials)  # separate meshes, swapped in Unity by WeaponLoadout
        weapons.build_gun(armature, materials)  # the parry pistol, in hand and holstered
        weapons.build_injector(armature, materials)  # the nano-injector cartridge and gauntlet port
    rig.add_sockets(armature)
    actions = animations.build_all(armature)

    if args.turnaround:
        from knight import turnaround
        idle = next(a for a in actions if a.name == "Idle")
        armature.animation_data.action = idle
        if hasattr(armature.animation_data, "action_slot") and idle.slots:
            armature.animation_data.action_slot = idle.slots[0]
        bpy.context.scene.frame_set(0)
        turnaround.render(args.turnaround, views=tuple(turnaround.VIEWS), prefix=args.model)
        armature.animation_data.action = None
    if args.preview:
        from knight import preview
        preview.render(armature, args.preview, actions)
    if args.preview_only:
        return

    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
    export.export_fbx(armature, mesh, os.path.abspath(args.out))
    print(f"Exported {len(actions)} animations to {args.out}")


if __name__ == "__main__":
    main()
