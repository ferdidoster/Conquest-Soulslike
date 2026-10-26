"""
Weapons for the tech-knight, each its own mesh skinned to the Weapon.R bone, so Unity can swap
them on the same rig (see WeaponLoadout). Every weapon has hit sample points from hilt to tip,
named "<prefix>_<n>". The weapon bone runs forward (-Y) out of the right fist at (-0.30, z 0.85).

    Longsword        the tech-knight's original long dark blade with a cyan edge
    UltraGreatsword  a slab of a two-handed blade after Elden Ring's ultra greatswords

The nano-injector: a glowing cartridge in the left fist (Injector_Hand, needle socket Injector_Tip),
shown by Unity only while healing, and the port on the right gauntlet's front plate it is slammed
into (Injector_Gauntlet, socket Injector_Port; see animations.build_heal).

The parry pistol lives in the left hand (Gun_Hand, with a Gun_Muzzle socket) while shooting and
on the left hip (Gun_Holster) otherwise; Unity's ParryGun swaps which one is shown.
"""

import math

import bpy
from mathutils import Matrix

from .tech_model import TechBuilder

BONE = "Weapon.R"
X, Z = -0.30, 0.85  # the line of the grip through the right fist

# Points along the blade, from hilt to tip (armature Y), for hit detection and the trail.
WEAPONS = {
    "Longsword": {"prefix": "Longsword", "points": (-0.25, -0.62, -1.00)},
    "UltraGreatsword": {"prefix": "Greatsword", "points": (-0.28, -0.72, -1.16, -1.60)},
}


def _blade_matrix(y):
    """Outline space -> armature space: outline x = blade width (up), outline z = length (forward)."""
    basis = Matrix(((0, 1, 0, 0), (0, 0, -1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))
    return Matrix.Translation((X, y, Z)) @ basis


def _grip(mb, back, pommel_size):
    """Leather grip from the guard back to `back`, long enough for both fists, with a pommel."""
    mb.cylinder((X, back, Z), (X, -0.10, Z), 0.018, 0.018, BONE, "Leather", segments=6)
    for y in (0.0, 0.08):  # wraps
        mb.cylinder((X, y + 0.012, Z), (X, y - 0.012, Z), 0.021, 0.021, BONE, "Plate", segments=6)
    mb.box((X, back + pommel_size[1] / 2, Z), pommel_size, BONE, "Plate", bevel=0.006)


def _longsword(mb):
    """Long dark blade with a cyan cutting edge, pointing forward (-Y) out of the right fist."""
    mb.cylinder((X, 0.13, Z), (X, -0.10, Z), 0.017, 0.017, BONE, "Leather", segments=6)
    mb.box((X, 0.145, Z), (0.04, 0.03, 0.04), BONE, "Plate", bevel=0.006)  # pommel
    mb.box((X, -0.11, Z), (0.15, 0.035, 0.035), BONE, "Plate", bevel=0.006)  # crossguard
    mb.box((X, -0.11, Z), (0.02, 0.037, 0.012), BONE, "Glow")
    # Blade: outline length runs forward, width runs vertically, the cutting edge faces down.
    blade = [(-0.026, 0.0), (0.024, 0.0), (0.024, 0.80), (0.0, 0.92), (-0.026, 0.84)]
    blade_m = (Matrix.Translation((X, -0.125, Z)) @ Matrix.Rotation(math.radians(90), 4, "Y")
               @ Matrix.Rotation(math.radians(90), 4, "X"))
    mb.prism(blade, 0.01, BONE, "Blade", blade_m)
    edge = [(-0.034, 0.03), (-0.024, 0.03), (-0.024, 0.84), (0.0, 0.92), (-0.006, 0.93), (-0.034, 0.84)]
    mb.prism(edge, 0.006, BONE, "Glow", blade_m)
    mb.prism([(-0.004, 0.06), (0.004, 0.06), (0.004, 0.6), (-0.004, 0.6)], 0.012, BONE, "Suit", blade_m)


def _ultra_greatsword(mb):
    """
    A huge flat slab: 1.55 m of dark steel a hand and a half wide, a blunt chisel tip, a heavy
    square guard and a long two-hand grip. Cyan edges and a glowing core slot match the knight.
    """
    _grip(mb, 0.20, (0.075, 0.05, 0.075))

    # Guard: a wide block with swept-back horns and a glowing seam.
    mb.box((X, -0.125, Z), (0.075, 0.07, 0.34), BONE, "Plate", bevel=0.01)
    for s in (1, -1):
        mb.box((X, -0.14, Z + 0.18 * s), (0.06, 0.10, 0.05), BONE, "Plate", taper=(0.7, 0.7), bevel=0.008)
    mb.box((X, -0.162, Z), (0.08, 0.008, 0.26), BONE, "Glow")

    start, length = -0.16, 1.55
    m = _blade_matrix(start)
    # Width runs up/down (both edges cut), length runs forward. Slight taper, chisel tip.
    blade = [(-0.125, 0.0), (0.125, 0.0), (0.112, 1.36), (0.04, length), (-0.112, 1.45)]
    mb.prism(blade, 0.04, BONE, "Blade", m)
    # Thick spine ridge down the middle, so the slab reads as heavy from the side.
    mb.prism([(-0.035, 0.02), (0.035, 0.02), (0.03, 1.30), (-0.03, 1.30)], 0.058, BONE, "Plate", m)
    # Glowing core slot in the ridge.
    mb.prism([(-0.008, 0.10), (0.008, 0.10), (0.008, 1.10), (-0.008, 1.10)], 0.062, BONE, "Glow", m)
    # Glowing bevels along both cutting edges.
    for s in (1, -1):
        edge = [(0.118 * s, 0.05), (0.13 * s, 0.05), (0.118 * s, 1.36), (0.106 * s, 1.36)]
        mb.prism(edge, 0.016, BONE, "Glow", m)
    # Chisel tip edge.
    mb.prism([(-0.112, 1.44), (-0.118, 1.46), (0.035, length + 0.012), (0.04, length)], 0.016, BONE, "Glow", m)
    # Notches near the guard, a classic greatsword ricasso.
    for s in (1, -1):
        mb.box((X, start - 0.17, Z + 0.125 * s), (0.05, 0.05, 0.03), BONE, "Plate")


BUILDERS = {"Longsword": _longsword, "UltraGreatsword": _ultra_greatsword}

# The pistol in the left fist at rest (arm hanging): the barrel runs down along the forearm on the
# thumb side (-Y), the grip goes back through the fist.
GUN_X, GUN_Y = 0.30, -0.075
GUN_TOP, GUN_MUZZLE = 0.94, 0.60
HOLSTER_OFFSET = (-0.03, 0.17, -0.07)  # on the outside of the left hip, behind the hanging hand


def _pistol(mb, bone, offset=(0.0, 0.0, 0.0)):
    """A stubby tech flintlock: heavy barrel with a flared muzzle, a glowing charge strip, a leather grip."""
    def at(x, y, z):
        return (x + offset[0], y + offset[1], z + offset[2])

    mb.cylinder(at(GUN_X, GUN_Y, GUN_TOP - 0.06), at(GUN_X, GUN_Y, GUN_MUZZLE + 0.02), 0.024, 0.021, bone, "Plate",
                segments=8)
    mb.cylinder(at(GUN_X, GUN_Y, GUN_MUZZLE + 0.03), at(GUN_X, GUN_Y, GUN_MUZZLE), 0.026, 0.032, bone, "Plate",
                segments=8)
    mb.box(at(GUN_X, GUN_Y + 0.004, GUN_TOP - 0.05), (0.055, 0.07, 0.11), bone, "Plate", bevel=0.008)  # lock
    mb.box(at(GUN_X, GUN_Y - 0.024, (GUN_TOP + GUN_MUZZLE) / 2 - 0.03), (0.012, 0.008, 0.22), bone, "Glow")
    mb.box(at(GUN_X, GUN_Y - 0.03, GUN_TOP - 0.035), (0.016, 0.02, 0.03), bone, "Grey")  # hammer
    mb.box(at(GUN_X, -0.005, GUN_TOP - 0.075), (0.042, 0.13, 0.05), bone, "Leather",
           rotation=Matrix.Rotation(math.radians(-12), 4, "X"), bevel=0.006)  # grip through the fist
    mb.box(at(GUN_X, 0.055, GUN_TOP - 0.065), (0.05, 0.03, 0.06), bone, "Plate", bevel=0.006)  # pommel


def build_gun(rig, mats):
    """The parry pistol: in the left hand, on the hip, and a muzzle socket. Returns (hand, holster)."""
    bone_names = [b.name for b in rig.data.bones if b.use_deform]
    objects = []
    for name, bone, offset in (("Gun_Hand", "Hand.L", (0.0, 0.0, 0.0)), ("Gun_Holster", "Hips", HOLSTER_OFFSET)):
        mb = TechBuilder(bone_names, list(mats.keys()))
        _pistol(mb, bone, offset)
        obj = mb.to_object(name, list(mats.values()))
        obj.parent = rig
        obj.modifiers.new("Armature", "ARMATURE").object = rig
        objects.append(obj)

    muzzle = bpy.data.objects.new("Gun_Muzzle", None)
    muzzle.empty_display_size = 0.04
    bpy.context.scene.collection.objects.link(muzzle)
    muzzle.parent = rig
    muzzle.parent_type = "BONE"
    muzzle.parent_bone = "Hand.L"
    bpy.context.view_layer.update()
    muzzle.matrix_world = Matrix.Translation((GUN_X, GUN_Y, GUN_MUZZLE))
    bpy.context.view_layer.update()
    return tuple(objects)


def build_all(rig, mats):
    """Builds every weapon mesh and its blade points. Returns {name: object}."""
    bone_names = [b.name for b in rig.data.bones if b.use_deform]
    objects = {}
    for name, build in BUILDERS.items():
        mb = TechBuilder(bone_names, list(mats.keys()))
        build(mb)
        obj = mb.to_object("Weapon_" + name, list(mats.values()))
        obj.parent = rig
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
        objects[name] = obj
        _add_points(rig, WEAPONS[name])
    return objects


def _add_points(rig, spec):
    for i, y in enumerate(spec["points"]):
        empty = bpy.data.objects.new(f"{spec['prefix']}_{i}", None)
        empty.empty_display_size = 0.04
        bpy.context.scene.collection.objects.link(empty)
        empty.parent = rig
        empty.parent_type = "BONE"
        empty.parent_bone = BONE
        bpy.context.view_layer.update()
        empty.matrix_world = Matrix.Translation((X, y, Z))
    bpy.context.view_layer.update()


# The cartridge runs down out of the left fist (rest pose: the hand hangs, so down is along the hand).
CARTRIDGE_X, CARTRIDGE_Y = 0.30, -0.035
CARTRIDGE_TOP, CARTRIDGE_NEEDLE = 0.86, 0.71


def _cartridge(mb):
    x, y = CARTRIDGE_X, CARTRIDGE_Y
    mb.cylinder((x, y, CARTRIDGE_TOP), (x, y, 0.835), 0.026, 0.026, "Hand.L", "Plate", segments=8)  # cap in the fist
    mb.cylinder((x, y, 0.838), (x, y, 0.762), 0.022, 0.022, "Hand.L", "NaniteGlow", segments=8)  # the nanites
    for z in (0.815, 0.787):  # cage bands over the glass
        mb.cylinder((x, y, z + 0.004), (x, y, z - 0.004), 0.0245, 0.0245, "Hand.L", "Grey", segments=8)
    mb.cylinder((x, y, 0.764), (x, y, 0.745), 0.025, 0.016, "Hand.L", "Plate", segments=8)  # nozzle
    mb.cylinder((x, y, 0.745), (x, y, CARTRIDGE_NEEDLE), 0.006, 0.002, "Hand.L", "Grey", segments=6)


def _port(mb):
    """A round socket on the right gauntlet's front plate with a glowing ring; it faces -Y in the rest pose."""
    x, y, z = -0.29, -0.062, 1.035
    mb.cylinder((x, y, z), (x, -0.08, z), 0.03, 0.028, "LowerArm.R", "Plate", segments=10)
    mb.cylinder((x, -0.079, z), (x, -0.084, z), 0.027, 0.027, "LowerArm.R", "NaniteGlow", segments=10)
    mb.cylinder((x, -0.08, z), (x, -0.086, z), 0.013, 0.013, "LowerArm.R", "Suit", segments=8)
    mb.box((x, -0.07, z + 0.045), (0.02, 0.012, 0.03), "LowerArm.R", "Grey", bevel=0.003)  # latch


def build_injector(rig, mats):
    """The nano-injector cartridge (left hand) and gauntlet port (right forearm), with their sockets."""
    bone_names = [b.name for b in rig.data.bones if b.use_deform]
    objects = []
    for name, build in (("Injector_Hand", _cartridge), ("Injector_Gauntlet", _port)):
        mb = TechBuilder(bone_names, list(mats.keys()))
        build(mb)
        obj = mb.to_object(name, list(mats.values()))
        obj.parent = rig
        obj.modifiers.new("Armature", "ARMATURE").object = rig
        objects.append(obj)

    for socket, bone, position in (("Injector_Tip", "Hand.L", (CARTRIDGE_X, CARTRIDGE_Y, CARTRIDGE_NEEDLE)),
                                   ("Injector_Port", "LowerArm.R", (-0.29, -0.083, 1.035))):
        empty = bpy.data.objects.new(socket, None)
        empty.empty_display_size = 0.03
        bpy.context.scene.collection.objects.link(empty)
        empty.parent = rig
        empty.parent_type = "BONE"
        empty.parent_bone = bone
        bpy.context.view_layer.update()
        empty.matrix_world = Matrix.Translation(position)
        bpy.context.view_layer.update()
    return tuple(objects)
