"""
Close-up renders of single lab kit pieces, to compare them with the concept sheet's asset callouts.

    python3 Tools/Blender/render_lab_pieces.py -- --out <folder> [--only Locker_Single,Crate_Storage]

Each piece gets a 3/4 view (Cycles, cold key light from the front-left, warm rim light, dark
backdrop, like the sheet's "Details & Props" panel), written as <out>/<Piece>.png.
"""

import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

from lab import kit  # noqa: E402
from lab import pieces_props, pieces_structure, pieces_tech  # noqa: E402,F401


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--only", default=None)
    parser.add_argument("--size", type=int, default=480)
    parser.add_argument("--samples", type=int, default=24)
    return parser.parse_args(argv)


def setup(size, samples):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.018, 0.022, 0.03, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    scene.world = world

    def area(name, loc, energy, color, size_m):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.size = size_m
        obj = bpy.data.objects.new(name, data)
        obj.location = loc
        obj.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(obj)
        return obj

    lights = [area("Key", (-6, -8, 7), 900, (0.75, 0.85, 1.0), 4),
              area("Rim", (7, 5, 5), 500, (1.0, 0.6, 0.3), 3),
              area("Fill", (6, -7, 2), 150, (0.6, 0.75, 1.0), 5)]
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-50, -50, 0), (50, -50, 0), (50, 50, 0), (-50, 50, 0)], [], [(0, 1, 2, 3)])
    mat = bpy.data.materials.new("FloorMat")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.03, 0.035, 0.045, 1)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.35
    floor.materials.append(mat)
    fobj = bpy.data.objects.new("Floor", floor)
    fobj.location.z = -0.002
    scene.collection.objects.link(fobj)
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return scene, cam, lights


def frame(cam, lights, objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    centre = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.5)
    direction = Vector((-0.55, -1.0, 0.45)).normalized()
    dist = radius / math.tan(math.radians(17)) * 1.05
    cam.location = centre + direction * dist
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = dist * 4
    scale = radius / 2.5
    for light, base in zip(lights, ((-6, -8, 7), (7, 5, 5), (6, -7, 2))):
        light.location = centre + Vector(base) * max(scale, 0.6)
        light.rotation_euler = (centre - light.location).to_track_quat("-Z", "Y").to_euler()
        light.data.energy = {"Key": 900, "Rim": 500, "Fill": 150}[light.name] * max(scale, 0.6) ** 2


def main():
    args = parse_args()
    os.makedirs(args.out, exist_ok=True)
    scene, cam, lights = setup(args.size, args.samples)
    only = set(args.only.split(",")) if args.only else None
    col = bpy.data.collections.new("Piece")
    scene.collection.children.link(col)
    for name in kit.PIECES:
        if only and name not in only:
            continue
        objs = kit.build(name, col)
        visible = [o for o in objs if "_COL_" not in o.name]
        for o in objs:
            o.hide_render = o not in visible
        frame(cam, lights, visible)
        scene.render.filepath = os.path.join(args.out, name + ".png")
        bpy.ops.render.render(write_still=True)
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
        print("rendered", name)


if __name__ == "__main__":
    main()
