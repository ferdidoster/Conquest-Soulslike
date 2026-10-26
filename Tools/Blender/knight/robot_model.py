"""
The Sentinel: the robot enemy. Bone-white plating over a dark frame, a purple cloth scarf and tabard,
a single magenta eye and a glowing core, and a medium sword with a glowing edge.

Uses the same rig, sockets and animations as the knight (rig.py / animations.py), so it is a drop-in
model. Every part is rigidly bound to one bone. The sword is part of the mesh, on Weapon.R, and runs
along the Blade_* sockets.
"""

import math

from mathutils import Matrix

from .model import MeshBuilder

import bpy

# name: (base colour (linear), metallic, roughness, emission strength)
PALETTE = {
    "Bone": ((0.79, 0.74, 0.65, 1), 0.1, 0.6, 0.0),
    "Frame": ((0.044, 0.036, 0.08, 1), 0.3, 0.6, 0.0),
    "Ink": ((0.01, 0.007, 0.018, 1), 0.2, 0.7, 0.0),
    "Cloth": ((0.16, 0.05, 0.24, 1), 0.0, 0.9, 0.0),
    "Magenta": ((0.64, 0.06, 0.16, 1), 0.1, 0.5, 0.0),
    "Steel": ((0.10, 0.085, 0.18, 1), 0.6, 0.35, 0.0),
    "Glow": ((1.0, 0.05, 0.21, 1), 0.0, 0.4, 5.0),
}


def make_materials():
    mats = {}
    for name, (color, metallic, roughness, emission) in PALETTE.items():
        m = bpy.data.materials.new("Robot_" + name)
        m.diffuse_color = color
        m.metallic = metallic
        m.roughness = roughness
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = color
            bsdf.inputs["Metallic"].default_value = metallic
            bsdf.inputs["Roughness"].default_value = roughness
            if emission > 0:
                bsdf.inputs["Emission Color"].default_value = color
                bsdf.inputs["Emission Strength"].default_value = emission
        mats[name] = m
    return mats


def _rx(deg):
    return Matrix.Rotation(math.radians(deg), 4, "X")


def _ry(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Y")


def _head(mb):
    mb.cylinder((0, 0, 1.45), (0, 0, 1.57), 0.045, 0.045, "Neck", "Ink")
    mb.box((0, 0, 1.66), (0.21, 0.22, 0.2), "Head", "Bone", taper=(0.75, 0.85), bevel=0.012)
    mb.box((0, -0.098, 1.66), (0.215, 0.04, 0.06), "Head", "Ink", bevel=0.005)  # visor band
    mb.box((0.045, -0.12, 1.66), (0.035, 0.012, 0.03), "Head", "Glow")          # the one eye
    mb.box((0, 0.0, 1.765), (0.12, 0.15, 0.02), "Head", "Bone")                 # crown plate
    mb.cylinder((-0.07, 0.01, 1.72), (-0.11, 0.02, 1.98), 0.01, 0.007, "Head", "Frame", segments=6)
    mb.box((-0.111, 0.02, 1.985), (0.03, 0.03, 0.025), "Head", "Magenta")


def _torso(mb):
    for i in range(4):
        mb.box((0, 0, 1.11 + i * 0.04), (0.15 - i * 0.004, 0.11, 0.028), "Spine", "Steel", bevel=0.004)
    mb.cylinder((0, 0.01, 1.10), (0, 0.01, 1.26), 0.04, 0.04, "Spine", "Ink", segments=6)
    # Chest: narrow at the waist, wide at the shoulders.
    mb.box((0, 0, 1.31), (0.28, 0.22, 0.28), "Chest", "Bone", taper=(1.75, 1.25), bevel=0.015)
    mb.box((0, -0.135, 1.34), (0.1, 0.03, 0.12), "Chest", "Ink", bevel=0.008)  # core housing
    for i in range(3):
        mb.box((0, -0.152, 1.30 + i * 0.035), (0.07, 0.01, 0.015), "Chest", "Glow")
    mb.box((0, -0.12, 1.44), (0.42, 0.03, 0.025), "Chest", "Magenta")  # collar trim
    # Power pack on the back.
    mb.box((0, 0.17, 1.34), (0.24, 0.1, 0.26), "Chest", "Frame", bevel=0.01)
    for i in range(3):
        mb.box((0, 0.222, 1.26 + i * 0.075), (0.16, 0.01, 0.022), "Chest", "Glow")
    for s in (-1, 1):
        mb.cylinder((0.09 * s, 0.19, 1.44), (0.09 * s, 0.19, 1.60), 0.025, 0.025, "Chest", "Steel", segments=6)
        mb.cylinder((0.09 * s, 0.19, 1.60), (0.09 * s, 0.19, 1.63), 0.03, 0.03, "Chest", "Ink", segments=6)
    # Scarf: a wrap round the neck, a tail over the left breast and a longer one down the back.
    mb.cylinder((0, 0, 1.43), (0, 0, 1.51), 0.15, 0.12, "Chest", "Cloth", segments=10, smooth=False)
    mb.box((0.12, -0.13, 1.10), (0.11, 0.02, 0.32), "Chest", "Cloth", taper=(1.4, 1.0))
    mb.box((0.06, 0.23, 1.02), (0.12, 0.02, 0.42), "Chest", "Cloth", taper=(1.3, 1.0))


def _hips(mb):
    mb.box((0, 0, 0.96), (0.30, 0.19, 0.11), "Hips", "Frame", bevel=0.01)
    mb.box((0, 0, 1.03), (0.32, 0.21, 0.04), "Hips", "Magenta", bevel=0.004)
    mb.box((0, -0.105, 0.70), (0.14, 0.015, 0.32), "Hips", "Cloth", taper=(1.45, 1.0))
    mb.box((0, -0.115, 0.95), (0.03, 0.012, 0.05), "Hips", "Magenta")


def _arms(mb):
    for side, s in (("L", 1), ("R", -1)):
        # Pauldron, tilted down and out.
        rot = _ry(-18 * s)
        mb.box((0.25 * s, 0, 1.40), (0.19, 0.22, 0.11), "UpperArm." + side, "Bone", taper=(0.85, 0.9),
               rotation=rot, bevel=0.01)
        mb.box((0.33 * s, 0, 1.40), (0.04, 0.23, 0.06), "UpperArm." + side, "Magenta", rotation=rot)
        mb.cylinder((0.225 * s, 0, 1.40), (0.27 * s, 0.01, 1.15), 0.05, 0.045, "UpperArm." + side, "Frame",
                    segments=6, smooth=False)
        mb.sphere((0.27 * s, 0.01, 1.15), 0.05, "LowerArm." + side, "Ink", segments=8)
        mb.box((0.28 * s, -0.005, 1.02), (0.10, 0.10, 0.2), "LowerArm." + side, "Bone", taper=(1.3, 1.3),
               bevel=0.008)
        mb.box((0.282 * s, -0.008, 0.98), (0.12, 0.12, 0.03), "LowerArm." + side, "Magenta")
        mb.box((0.295 * s, -0.025, 0.85), (0.085, 0.1, 0.1), "Hand." + side, "Ink", bevel=0.01)


def _legs(mb):
    for side, s in (("L", 1), ("R", -1)):
        mb.box((0.105 * s, 0, 0.77), (0.11, 0.12, 0.38), "UpperLeg." + side, "Bone", taper=(1.3, 1.25),
               bevel=0.01)
        mb.box((0.11 * s, -0.035, 0.54), (0.11, 0.09, 0.08), "LowerLeg." + side, "Frame", bevel=0.01)
        mb.box((0.11 * s, 0.01, 0.32), (0.09, 0.10, 0.40), "LowerLeg." + side, "Frame", taper=(1.35, 1.3),
               bevel=0.01)
        mb.box((0.11 * s, 0.01, 0.38), (0.125, 0.135, 0.035), "LowerLeg." + side, "Magenta")
        mb.box((0.11 * s, -0.05, 0.05), (0.12, 0.25, 0.09), "Foot." + side, "Ink", bevel=0.012)


def _sword(mb):
    # Held in the right fist like the knight's blade: it points forward (-Y), the edge faces down.
    x, z, bone = -0.30, 0.85, "Weapon.R"
    mb.box((x, 0.0, z - 0.02), (0.035, 0.17, 0.035), bone, "Steel")          # grip
    mb.box((x, 0.10, z - 0.025), (0.05, 0.04, 0.05), bone, "Magenta")       # pommel
    mb.box((x, -0.11, z - 0.1), (0.045, 0.035, 0.2), bone, "Magenta", bevel=0.005)  # guard
    mb.box((x, -0.55, z - 0.045), (0.018, 0.84, 0.09), bone, "Steel")        # blade
    mb.box((x, -0.55, z - 0.098), (0.022, 0.84, 0.016), bone, "Glow")        # glowing edge
    mb.box((x, -0.99, z - 0.065), (0.018, 0.1, 0.06), bone, "Steel", rotation=_rx(-35))  # clipped tip


def build_mesh(rig, mats):
    bone_names = [b.name for b in rig.data.bones if b.use_deform]
    mb = MeshBuilder(bone_names, list(mats.keys()))
    _head(mb)
    _torso(mb)
    _hips(mb)
    _arms(mb)
    _legs(mb)
    _sword(mb)
    obj = mb.to_object("Robot_Mesh", list(mats.values()))
    obj.parent = rig
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    return obj
