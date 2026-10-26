"""
The Warden: a stationary gunner droid, after the boxy Star Wars "BD" droids. A worn white box body on
short bird-like legs, a wide flat head on a neck stalk with two lamp eyes (the laser fires from between them),
thin tool arms, and a blade on the right arm that only comes out in melee. Same rig, sockets and
animations as the knight, so the shapes suggest reverse-jointed legs on the humanoid leg bones.

Extra objects for Unity: "Gunner_Blade" (a separate mesh on Weapon.R, toggled by TurretAIBrain) and
"Laser_Muzzle" (an empty on Head between the lamps, where the beam starts).
"""

import math

import bpy
from mathutils import Matrix

from .model import MeshBuilder

# name: (base colour (linear), metallic, roughness, emission strength)
PALETTE = {
    "White": ((0.70, 0.67, 0.60, 1), 0.1, 0.7, 0.0),
    "Grime": ((0.36, 0.33, 0.28, 1), 0.1, 0.9, 0.0),
    "Blue": ((0.04, 0.16, 0.48, 1), 0.2, 0.6, 0.0),
    "Orange": ((0.75, 0.22, 0.04, 1), 0.1, 0.6, 0.0),
    "Mech": ((0.03, 0.03, 0.035, 1), 0.6, 0.5, 0.0),
    "Steel": ((0.25, 0.25, 0.27, 1), 0.8, 0.35, 0.0),
    "Glow": ((1.0, 0.62, 0.25, 1), 0.0, 0.4, 6.0),
}

MUZZLE = (0.0, -0.27, 1.86)

# Short legs: the leg bones are scaled to LEG_SCALE of the knight's and everything above them drops by
# DROP. Geometry below is written in knight proportions and reshaped the same way (see _reshape).
LEG_SCALE = 0.6
LEG_BONES = {"UpperLeg.L", "LowerLeg.L", "Foot.L", "UpperLeg.R", "LowerLeg.R", "Foot.R"}
DROP = 0.98 * (1 - LEG_SCALE)


def _reshape_point(p, leg):
    x, y, z = p
    return (x, y, z * LEG_SCALE) if leg else (x, y, z - DROP)


def shorten_rig():
    """Rewrites the shared rig tables for the short-legged droid. Call before rig.build_armature()."""
    from . import posing, rig
    for name, (head, tail, parent) in list(rig.BONES.items()):
        leg = name in LEG_BONES
        rig.BONES[name] = (_reshape_point(head, leg), _reshape_point(tail, leg), parent)
    for name, (bone, pos) in list(rig.SOCKETS.items()):
        rig.SOCKETS[name] = (bone, _reshape_point(pos, False))
    posing.HIPS_Z_SCALE = LEG_SCALE


def _reshape(mb):
    """Applies the short-leg proportions to every vertex, by the bone it is bound to."""
    leg_groups = {mb.groups[n] for n in LEG_BONES if n in mb.groups}
    for v in mb.bm.verts:
        leg = any(g in leg_groups for g in v[mb.deform].keys())
        v.co.z = v.co.z * LEG_SCALE if leg else v.co.z - DROP


def make_materials():
    mats = {}
    for name, (color, metallic, roughness, emission) in PALETTE.items():
        m = bpy.data.materials.new("Droid_" + name)
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


def _head(mb):
    # Neck stalk: a dark mechanical column from the body up into the head.
    mb.box((0, 0.02, 1.52), (0.09, 0.1, 0.14), "Neck", "Mech", bevel=0.01)
    mb.box((0, 0.02, 1.64), (0.07, 0.08, 0.16), "Head", "Mech", bevel=0.01)
    mb.cylinder((0.05, 0.05, 1.47), (0.05, 0.03, 1.70), 0.012, 0.012, "Head", "Mech", segments=6)
    # Wide flat head, tilted forward a little, with a blue stripe and grime on the underside.
    tilt = _rx(8)
    mb.box((0, -0.06, 1.85), (0.62, 0.40, 0.13), "Head", "White", taper=(0.92, 0.9), rotation=tilt, bevel=0.012)
    mb.box((0, -0.06, 1.787), (0.6, 0.38, 0.02), "Head", "Grime", rotation=tilt)
    mb.box((0.0, -0.04, 1.918), (0.5, 0.06, 0.012), "Head", "Blue", rotation=tilt)
    mb.box((0.15, -0.04, 1.918), (0.1, 0.32, 0.012), "Head", "Orange", rotation=tilt)
    # Two lamp eyes on the front face, and a small sensor on the right corner.
    for x in (-0.13, 0.13):
        mb.cylinder((x, -0.25, 1.84), (x, -0.275, 1.84), 0.05, 0.05, "Head", "Steel", segments=12)
        mb.cylinder((x, -0.27, 1.84), (x, -0.28, 1.84), 0.036, 0.036, "Head", "Glow", segments=12)
    mb.cylinder((-0.3, -0.22, 1.86), (-0.3, -0.29, 1.86), 0.03, 0.025, "Head", "Mech", segments=8)
    for x in (-0.24, 0.24):  # speaker grilles
        mb.box((x, -0.255, 1.84), (0.06, 0.012, 0.05), "Head", "Mech")
    # Antennas.
    mb.cylinder((-0.22, 0.06, 1.9), (-0.25, 0.08, 2.24), 0.006, 0.005, "Head", "Mech", segments=5)
    mb.cylinder((0.22, 0.06, 1.9), (0.24, 0.1, 2.18), 0.006, 0.005, "Head", "Mech", segments=5)


def _body(mb):
    # A boxy chest on the Chest bone, with the blue trim, front display and vents of the reference.
    mb.box((0, 0, 1.24), (0.46, 0.40, 0.42), "Chest", "White", taper=(1.05, 1.0), bevel=0.02)
    mb.box((0, -0.005, 1.41), (0.475, 0.405, 0.03), "Chest", "Blue")
    mb.box((0, -0.005, 1.385), (0.475, 0.405, 0.012), "Chest", "Orange")
    mb.box((0, 0, 1.04), (0.44, 0.38, 0.03), "Chest", "Blue")
    mb.box((0, -0.205, 1.31), (0.2, 0.02, 0.1), "Chest", "Grime", bevel=0.005)     # display bezel
    mb.box((0, -0.214, 1.31), (0.15, 0.01, 0.05), "Chest", "Glow")                 # display
    for i in range(5):                                                             # front vents
        mb.box((0, -0.205, 1.12 + i * 0.025), (0.16, 0.02, 0.01), "Chest", "Mech")
    for s in (-1, 1):
        mb.box((0.235 * s, 0.03, 1.22), (0.02, 0.25, 0.25), "Chest", "Mech")
        mb.cylinder((0.2 * s, 0.17, 1.38), (0.25 * s, 0.24, 1.18), 0.018, 0.018, "Chest", "Mech", segments=6)
    mb.box((0, 0, 1.06), (0.24, 0.22, 0.12), "Spine", "Mech", bevel=0.01)


def _hips(mb):
    mb.box((0, 0.02, 0.98), (0.3, 0.26, 0.1), "Hips", "Mech", bevel=0.01)
    for s in (-1, 1):
        # Big hip actuator housings, the white pods on the reference's flanks.
        mb.box((0.2 * s, 0.03, 0.97), (0.12, 0.2, 0.2), "Hips", "White", bevel=0.015)
        mb.box((0.262 * s, 0.03, 0.97), (0.012, 0.16, 0.08), "Hips", "Blue")


def _legs(mb):
    for side, s in (("L", 1), ("R", -1)):
        up, low, foot = "UpperLeg." + side, "LowerLeg." + side, "Foot." + side
        # Thigh: a white plate swept back from the hip, then a dark knee joint behind the leg line.
        mb.box((0.11 * s, 0.07, 0.77), (0.12, 0.13, 0.36), up, "White", rotation=_rx(-18), bevel=0.012)
        mb.box((0.11 * s, 0.07, 0.77), (0.125, 0.135, 0.04), up, "Blue", rotation=_rx(-18))
        mb.cylinder((0.04 * s, 0.13, 0.56), (0.18 * s, 0.13, 0.56), 0.07, 0.07, low, "Mech", segments=10)
        # Shin: thin dark struts angled forward down to the ankle disc.
        mb.box((0.11 * s, 0.07, 0.33), (0.07, 0.08, 0.44), low, "Mech", rotation=_rx(14), bevel=0.008)
        mb.cylinder((0.05 * s, 0.03, 0.12), (0.17 * s, 0.03, 0.12), 0.08, 0.08, low, "Steel", segments=12)
        mb.cylinder((0.165 * s, 0.03, 0.12), (0.175 * s, 0.03, 0.12), 0.05, 0.05, low, "Grime", segments=12)
        # Wedge foot in the accent colour, with a dark sole.
        mb.box((0.11 * s, -0.04, 0.06), (0.14, 0.32, 0.12), foot, "Blue", taper=(0.9, 0.6), bevel=0.01)
        mb.box((0.11 * s, -0.04, 0.012), (0.145, 0.33, 0.025), foot, "Mech")
        mb.box((0.11 * s, -0.2, 0.05), (0.14, 0.015, 0.06), foot, "Orange")


def _arms(mb):
    # Thin tool arms tucked against the body.
    for side, s in (("L", 1), ("R", -1)):
        mb.box((0.25 * s, 0, 1.38), (0.09, 0.14, 0.1), "UpperArm." + side, "White", bevel=0.01)
        mb.cylinder((0.25 * s, 0, 1.36), (0.27 * s, 0.01, 1.15), 0.025, 0.025, "UpperArm." + side, "Mech",
                    segments=6, smooth=False)
        mb.sphere((0.27 * s, 0.01, 1.15), 0.035, "LowerArm." + side, "Steel", segments=8)
        mb.cylinder((0.27 * s, 0.01, 1.15), (0.29 * s, -0.02, 0.92), 0.022, 0.022, "LowerArm." + side, "Mech",
                    segments=6, smooth=False)
        mb.box((0.295 * s, -0.025, 0.87), (0.06, 0.07, 0.07), "Hand." + side, "Steel", bevel=0.008)


def _blade(rig, mats):
    """The melee blade, a separate mesh so Unity can show it only up close."""
    names = [b.name for b in rig.data.bones if b.use_deform]
    mb = MeshBuilder(names, list(mats.keys()))
    x, z, bone = -0.30, 0.85, "Weapon.R"
    mb.box((x, -0.10, z - 0.02), (0.06, 0.08, 0.1), bone, "Orange", bevel=0.008)
    mb.box((x, -0.52, z - 0.03), (0.02, 0.76, 0.1), bone, "Steel", bevel=0.004)
    mb.box((x, -0.52, z - 0.085), (0.024, 0.76, 0.014), bone, "Glow")
    mb.box((x, -0.93, z - 0.035), (0.02, 0.06, 0.08), bone, "Steel", rotation=_rx(-30))
    _reshape(mb)
    obj = mb.to_object("Gunner_Blade", list(mats.values()))
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    return obj


def _muzzle(rig):
    empty = bpy.data.objects.new("Laser_Muzzle", None)
    empty.empty_display_size = 0.05
    bpy.context.scene.collection.objects.link(empty)
    empty.parent = rig
    empty.parent_type = "BONE"
    empty.parent_bone = "Head"
    bpy.context.view_layer.update()
    empty.matrix_world = Matrix.Translation(_reshape_point(MUZZLE, False))


def build_mesh(rig, mats):
    bone_names = [b.name for b in rig.data.bones if b.use_deform]
    mb = MeshBuilder(bone_names, list(mats.keys()))
    _head(mb)
    _body(mb)
    _hips(mb)
    _legs(mb)
    _arms(mb)
    _reshape(mb)
    obj = mb.to_object("Gunner_Mesh", list(mats.values()))
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    _blade(rig, mats)
    _muzzle(rig)
    return obj
