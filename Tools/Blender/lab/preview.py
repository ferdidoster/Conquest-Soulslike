"""Quick Workbench renders of the lab level and the kit, to compare against the sketch and concept."""

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
    scene.render.resolution_y = 1200
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "MATERIAL"
    shading.show_object_outline = True
    shading.object_outline_color = (0.02, 0.03, 0.05)
    shading.show_cavity = True
    shading.cavity_type = "WORLD"
    shading.show_shadows = True
    shading.shadow_intensity = 0.4
    scene.display.light_direction = (0.35, -0.45, 0.82)
    shading.background_type = "VIEWPORT"
    shading.background_color = (0.02, 0.025, 0.035)


def _spawn_markers(spawns, col):
    for kind, loc, _ in spawns:
        mat = bpy.data.materials.new("Spawn_" + kind)
        mat.diffuse_color = (0.2, 1.0, 0.4, 1) if kind == "Player" else (1.0, 0.15, 0.1, 1)
        mesh = bpy.data.meshes.new("spawn")
        obj = bpy.data.objects.new("spawn", mesh)
        import bmesh
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.8, radius2=0.8, depth=3.0)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(mat)
        obj.location = (loc[0], loc[1], 1.5)
        col.objects.link(obj)


def render(folder, objects, level_col, kit_col, spawns):
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    _setup(scene)
    _spawn_markers(spawns, level_col)

    shots = [
        ("lab_top", _camera("Top", (8, 0, 150), (0, 0, 0), ortho=112)),
        ("lab_iso_overview", _camera("Iso", (8 - 60, -60, 70), (55, 0, -45), ortho=110)),
        ("lab_iso_hub", _camera("IsoHub", (-25, -25, 30), (55, 0, -45), ortho=34)),
        ("lab_iso_arena", _camera("IsoArena", (52 - 30, -30, 34), (55, 0, -45), ortho=50)),
        ("lab_iso_rooms", _camera("IsoRooms", (-25 - 30, -25, 36), (55, 0, -45), ortho=48)),
        ("lab_hero_hub", _camera("HeroHub", (-7.5, -1.5, 2.0), (85, 0, -80), lens=18)),
    ]
    kit_col.hide_render = True
    for name, cam in shots:
        scene.camera = cam
        scene.render.filepath = os.path.join(folder, name + ".png")
        bpy.ops.render.render(write_still=True)

    kit_col.hide_render = False
    level_col.hide_render = True
    vis = [o for objs in objects.values() for o in objs if not o.hide_render]
    xs = [o.location.x for o in vis]
    ys = [o.location.y for o in vis]
    center = Vector(((min(xs) + max(xs)) / 2 + 3, (min(ys) + max(ys)) / 2 - 4, 0))
    span = max(max(xs) - min(xs), max(ys) - min(ys)) + 16
    cam = _camera("KitCam", (center.x, center.y - 60, 70), (42, 0, 0), ortho=span)
    scene.camera = cam
    scene.render.filepath = os.path.join(folder, "lab_kit_sheet.png")
    bpy.ops.render.render(write_still=True)
    level_col.hide_render = False
