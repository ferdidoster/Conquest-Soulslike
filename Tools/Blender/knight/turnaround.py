"""Turnaround renders (front, side, back, 3/4) on a paper background, with ink lines, for concept comparison."""

import math
import os

import bpy
from mathutils import Vector

VIEWS = {
    "front": Vector((0, -1, 0.08)),
    "side": Vector((-1, 0, 0.08)),
    "back": Vector((0, 1, 0.08)),
    "34": Vector((-0.75, -0.65, 0.12)),
    "34back": Vector((0.7, 0.7, 0.2)),
}


def setup(resolution=(520, 760)):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 16
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.view_settings.view_transform = "Standard"

    world = bpy.data.worlds.new("Paper")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.8, 0.78, 0.74, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    scene.world = world

    sun_data = bpy.data.lights.new("Key", "SUN")
    sun_data.energy = 3.0
    sun_data.angle = math.radians(2)
    sun = bpy.data.objects.new("Key", sun_data)
    sun.rotation_euler = (math.radians(45), 0, math.radians(-35))
    scene.collection.objects.link(sun)

    # Ink lines like the in-game outline pass.
    scene.render.use_freestyle = True
    scene.render.line_thickness_mode = "ABSOLUTE"
    scene.render.line_thickness = 1.3
    lineset = scene.view_layers[0].freestyle_settings.linesets[0]
    lineset.select_by_visibility = True
    lineset.select_silhouette = True
    lineset.select_border = True
    lineset.select_crease = True
    if lineset.linestyle is None:
        lineset.linestyle = bpy.data.linestyles.new("Ink")
    lineset.linestyle.color = (0.1, 0.06, 0.07)

    cam_data = bpy.data.cameras.new("TurnCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.35
    cam = bpy.data.objects.new("TurnCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return scene


def render(out_dir, views=("front", "side", "back", "34"), prefix="turn", target=(0, 0, 1.02)):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    if scene.camera is None or scene.camera.name != "TurnCam":
        setup()
    cam = scene.camera
    target = Vector(target)
    paths = []
    for view in views:
        cam.location = target + VIEWS[view].normalized() * 6
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(out_dir, f"{prefix}_{view}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    return paths
