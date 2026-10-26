"""Quick Workbench renders of the arena and the kit, to compare against the concept art."""

import math
import os

import bpy
from mathutils import Vector


def _camera(name, loc, rot_deg, ortho=None, lens=28):
    cam = bpy.data.cameras.new(name)
    if ortho:
        cam.type = "ORTHO"
        cam.ortho_scale = ortho
    else:
        cam.lens = lens
    cam.clip_end = 1000
    obj = bpy.data.objects.new(name, cam)
    obj.location = loc
    obj.rotation_euler = [math.radians(a) for a in rot_deg]
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _setup(scene):
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "MATERIAL"
    shading.show_object_outline = True
    shading.object_outline_color = (0.08, 0.05, 0.06)
    shading.show_cavity = True
    shading.cavity_type = "WORLD"
    shading.show_shadows = True
    shading.shadow_intensity = 0.35
    scene.display.light_direction = (0.45, -0.35, 0.82)
    shading.background_type = "VIEWPORT"
    shading.background_color = (0.55, 0.72, 0.9)
    scene.world = bpy.data.worlds.new("Sky")
    scene.world.color = (0.42, 0.62, 0.85)


def render(folder, objects, arena_col, kit_col):
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    _setup(scene)

    shots = [
        ("arena_hero", _camera("Hero", (-3.0, -26.0, 3.2), (86, 0, -4), lens=20)),
        ("arena_iso_overview", _camera("IsoOverview", (0, -62, 52), (55, 0, 0), ortho=62)),
        ("arena_iso_gameplay", _camera("IsoGameplay", (0, -26, 15.0), (55, 0, 0), ortho=23.1)),
        ("arena_top", _camera("Top", (0, 2, 150), (0, 0, 0), ortho=95)),
    ]
    kit_col.hide_render = True
    for name, cam in shots:
        scene.camera = cam
        scene.render.filepath = os.path.join(folder, name + ".png")
        bpy.ops.render.render(write_still=True)

    # kit sheet
    kit_col.hide_render = False
    arena_col.hide_render = True
    xs = [o.location.x for o in objects.values() if not o.hide_render]
    ys = [o.location.y for o in objects.values() if not o.hide_render]
    center = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, 0))
    cam = _camera("KitCam", (center.x, center.y - 120, 90), (55, 0, 0), ortho=max(max(xs) - min(xs), 60) + 30)
    scene.camera = cam
    scene.render.filepath = os.path.join(folder, "kit_sheet.png")
    bpy.ops.render.render(write_still=True)
    arena_col.hide_render = False
