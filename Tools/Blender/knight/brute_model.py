"""
The Bulwark: a heavy robot enemy. Same family as the Sentinel (robot_model.py) but built like a tank:
a huge armoured chest that hides a sunken head, slab pauldrons, oversized fists and a heavy cleaver.
Same rig, sockets and animations as the knight; every part is rigidly bound to one bone.
"""

import math

from mathutils import Matrix

from .model import MeshBuilder
from .robot_model import make_materials  # noqa: F401  (shared Sentinel palette)


def _ry(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Y")


def _rx(deg):
    return Matrix.Rotation(math.radians(deg), 4, "X")


def _head(mb):
    # Sunken between the shoulders, a heavy brow and a wide eye slit with two eyes.
    mb.box((0, -0.03, 1.60), (0.2, 0.2, 0.16), "Head", "Bone", taper=(0.9, 0.9), bevel=0.012)
    mb.box((0, -0.125, 1.685), (0.24, 0.05, 0.04), "Head", "Bone", bevel=0.006)   # brow
    mb.box((0, -0.125, 1.635), (0.17, 0.03, 0.04), "Head", "Ink")
    for x in (-0.04, 0.04):
        mb.box((x, -0.142, 1.635), (0.03, 0.01, 0.022), "Head", "Glow")
    mb.box((0, -0.13, 1.56), (0.12, 0.03, 0.05), "Head", "Frame")              # jaw grille
    for x in (-0.03, 0.0, 0.03):
        mb.box((x, -0.147, 1.56), (0.01, 0.01, 0.04), "Head", "Ink")


def _torso(mb):
    mb.cylinder((0, 0.01, 1.08), (0, 0.01, 1.26), 0.07, 0.07, "Spine", "Ink", segments=8)
    for i in range(3):
        mb.box((0, 0, 1.12 + i * 0.05), (0.24, 0.17, 0.035), "Spine", "Steel", bevel=0.005)
    # Barrel chest, much wider than the shoulders' bones, with a raised collar that hides the neck.
    mb.box((0, -0.01, 1.36), (0.46, 0.34, 0.36), "Chest", "Bone", taper=(1.3, 1.1), bevel=0.03)
    mb.box((0, 0.03, 1.55), (0.40, 0.30, 0.05), "Chest", "Frame", bevel=0.01)        # collar ring
    mb.box((0, -0.19, 1.32), (0.16, 0.03, 0.16), "Chest", "Ink", bevel=0.01)        # core housing
    mb.box((0, -0.207, 1.32), (0.1, 0.01, 0.1), "Chest", "Glow")
    # Hazard stripes across the chest.
    for i in range(4):
        mb.box((-0.2 + i * 0.045, -0.2, 1.47), (0.022, 0.012, 0.05), "Chest", "Magenta",
               rotation=Matrix.Rotation(math.radians(30), 4, "Y"))
    # Reactor on the back with twin exhausts.
    mb.box((0, 0.24, 1.36), (0.36, 0.14, 0.32), "Chest", "Frame", bevel=0.015)
    for i in range(2):
        mb.box((0, 0.313, 1.29 + i * 0.12), (0.26, 0.01, 0.035), "Chest", "Glow")
    for s in (-1, 1):
        mb.cylinder((0.13 * s, 0.26, 1.50), (0.13 * s, 0.26, 1.70), 0.04, 0.035, "Chest", "Steel", segments=8)
        mb.cylinder((0.13 * s, 0.26, 1.70), (0.13 * s, 0.26, 1.74), 0.05, 0.05, "Chest", "Ink", segments=8)
    # Tattered cloth hung off the collar, down the back.
    mb.box((-0.08, 0.32, 1.05), (0.2, 0.02, 0.5), "Chest", "Cloth", taper=(1.4, 1.0))


def _hips(mb):
    mb.box((0, 0, 0.96), (0.40, 0.26, 0.14), "Hips", "Frame", bevel=0.012)
    mb.box((0, 0, 1.04), (0.42, 0.28, 0.05), "Hips", "Magenta", bevel=0.005)
    # Armoured skirt plates front and sides.
    mb.box((0, -0.15, 0.82), (0.2, 0.03, 0.22), "Hips", "Bone", taper=(1.3, 1.0), bevel=0.008)
    for s in (-1, 1):
        mb.box((0.22 * s, 0, 0.84), (0.04, 0.2, 0.2), "Hips", "Bone", bevel=0.008)
    mb.box((0, -0.12, 0.66), (0.16, 0.015, 0.3), "Hips", "Cloth", taper=(1.3, 1.0))


def _arms(mb):
    for side, s in (("L", 1), ("R", -1)):
        rot = _ry(-12 * s)
        # Slab pauldron, wider than the chest.
        mb.box((0.30 * s, 0, 1.46), (0.26, 0.32, 0.17), "UpperArm." + side, "Bone", taper=(0.85, 0.9),
               rotation=rot, bevel=0.015)
        mb.box((0.41 * s, 0, 1.44), (0.04, 0.33, 0.1), "UpperArm." + side, "Magenta", rotation=rot)
        mb.cylinder((0.24 * s, 0, 1.40), (0.27 * s, 0.01, 1.15), 0.075, 0.07, "UpperArm." + side, "Frame",
                    segments=8, smooth=False)
        mb.sphere((0.27 * s, 0.01, 1.15), 0.075, "LowerArm." + side, "Ink", segments=8)
        # Huge forearm gauntlet, flared toward the fist.
        mb.box((0.285 * s, -0.01, 0.94), (0.15, 0.15, 0.24), "LowerArm." + side, "Bone", taper=(0.8, 0.8),
               bevel=0.012)
        mb.box((0.287 * s, -0.012, 1.0), (0.165, 0.165, 0.035), "LowerArm." + side, "Magenta")
        mb.box((0.30 * s, -0.03, 0.83), (0.14, 0.15, 0.14), "Hand." + side, "Ink", bevel=0.015)


def _legs(mb):
    for side, s in (("L", 1), ("R", -1)):
        mb.box((0.12 * s, 0, 0.76), (0.15, 0.16, 0.36), "UpperLeg." + side, "Frame", taper=(1.2, 1.15),
               bevel=0.012)
        mb.box((0.125 * s, -0.06, 0.54), (0.15, 0.1, 0.12), "LowerLeg." + side, "Bone", bevel=0.012)  # knee guard
        mb.box((0.12 * s, 0.0, 0.30), (0.15, 0.17, 0.42), "LowerLeg." + side, "Bone", taper=(1.15, 1.1),
               bevel=0.012)
        mb.box((0.12 * s, 0.0, 0.38), (0.165, 0.185, 0.035), "LowerLeg." + side, "Magenta")
        mb.box((0.12 * s, -0.06, 0.05), (0.18, 0.32, 0.11), "Foot." + side, "Ink", bevel=0.015)


def _cleaver(mb):
    # A heavy, wide, square-ended blade along the knight's sword line (forward out of the right fist).
    x, z, bone = -0.30, 0.85, "Weapon.R"
    mb.box((x, 0.02, z - 0.02), (0.045, 0.2, 0.045), bone, "Steel")
    mb.box((x, 0.13, z - 0.02), (0.07, 0.05, 0.07), bone, "Magenta")
    mb.box((x, -0.11, z - 0.08), (0.06, 0.05, 0.22), bone, "Frame", bevel=0.006)
    mb.box((x, -0.56, z - 0.09), (0.03, 0.86, 0.2), bone, "Steel", bevel=0.004)
    mb.box((x, -0.56, z - 0.195), (0.034, 0.86, 0.02), bone, "Glow")
    mb.box((x, -0.98, z - 0.07), (0.03, 0.04, 0.24), bone, "Steel", rotation=_rx(10))
    for i in range(3):  # vents in the spine of the blade
        mb.box((x, -0.35 - i * 0.18, z + 0.0), (0.034, 0.08, 0.025), bone, "Ink")


def build_mesh(rig, mats):
    bone_names = [b.name for b in rig.data.bones if b.use_deform]
    mb = MeshBuilder(bone_names, list(mats.keys()))
    _head(mb)
    _torso(mb)
    _hips(mb)
    _arms(mb)
    _legs(mb)
    _cleaver(mb)
    obj = mb.to_object("Brute_Mesh", list(mats.values()))
    obj.parent = rig
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    return obj
