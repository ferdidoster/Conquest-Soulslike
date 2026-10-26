"""Renders preview images of poses or animation frames (Cycles CPU, so it works headless)."""

import math
import os

import bpy
from mathutils import Vector


def _setup_scene():
    scene = bpy.context.scene
    if scene.camera is not None:
        return scene

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 12
    scene.cycles.use_denoising = False
    scene.render.resolution_x = 360
    scene.render.resolution_y = 440
    scene.render.film_transparent = False

    world = bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.68, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scene.world = world

    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.5
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-30))
    scene.collection.objects.link(sun)

    ground_mesh = bpy.data.meshes.new("Ground")
    ground_mesh.from_pydata([(-4, -4, 0), (4, -4, 0), (4, 4, 0), (-4, 4, 0)], [], [(0, 1, 2, 3)])
    ground = bpy.data.objects.new("PreviewGround", ground_mesh)
    mat = bpy.data.materials.new("PreviewGround")
    mat.diffuse_color = (0.35, 0.33, 0.3, 1)
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.35, 0.33, 0.3, 1)
    ground_mesh.materials.append(mat)
    scene.collection.objects.link(ground)

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return scene


def _aim_camera(view):
    cam = bpy.context.scene.camera
    target = Vector((0, 0, 0.95))
    offsets = {
        "front": Vector((0, -4.2, 1.2)),
        "front34": Vector((-2.6, -3.3, 1.3)),
        "side": Vector((-4.2, 0, 1.1)),
        "back34": Vector((2.6, 3.3, 1.5)),
    }
    cam.location = target + offsets[view]
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()


def render_still(path, view="front34"):
    scene = _setup_scene()
    _aim_camera(view)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def render(rig, out_dir, actions, frames_per_action=4, views=("front34",), only=None):
    """Renders a few evenly spaced frames of each action."""
    os.makedirs(out_dir, exist_ok=True)
    scene = _setup_scene()
    rig.animation_data_create()
    for action in actions:
        if only and action.name not in only:
            continue
        rig.animation_data.action = action
        if hasattr(rig.animation_data, "action_slot") and action.slots:
            rig.animation_data.action_slot = action.slots[0]
        start, end = action.frame_range
        for i in range(frames_per_action):
            frame = int(round(start + (end - start) * i / max(1, frames_per_action - 1)))
            scene.frame_set(frame)
            for view in views:
                render_still(os.path.join(out_dir, f"{action.name}_{i}_{view}.png"), view)
