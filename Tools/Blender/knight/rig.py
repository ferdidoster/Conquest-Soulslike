"""Armature for the knight. Bone names are the contract with Unity (see AnimatorStates / setup tool)."""

import math

import bpy
from mathutils import Vector

# name: (head, tail, parent)
# The character faces -Y. ".L" bones are on +X (the character's left).
BONES = {
    "Root": ((0, 0, 0), (0, 0.25, 0), None),
    "Hips": ((0, 0, 0.98), (0, 0, 1.10), "Root"),
    "Spine": ((0, 0, 1.10), (0, 0, 1.26), "Hips"),
    "Chest": ((0, 0, 1.26), (0, 0, 1.46), "Spine"),
    "Neck": ((0, 0, 1.46), (0, 0, 1.55), "Chest"),
    "Head": ((0, 0, 1.55), (0, 0, 1.80), "Neck"),

    "UpperArm.L": ((0.22, 0.0, 1.42), (0.27, 0.01, 1.15), "Chest"),
    "LowerArm.L": ((0.27, 0.01, 1.15), (0.29, -0.02, 0.90), "UpperArm.L"),
    "Hand.L": ((0.29, -0.02, 0.90), (0.30, -0.03, 0.80), "LowerArm.L"),
    "UpperArm.R": ((-0.22, 0.0, 1.42), (-0.27, 0.01, 1.15), "Chest"),
    "LowerArm.R": ((-0.27, 0.01, 1.15), (-0.29, -0.02, 0.90), "UpperArm.R"),
    "Hand.R": ((-0.29, -0.02, 0.90), (-0.30, -0.03, 0.80), "LowerArm.R"),

    "UpperLeg.L": ((0.10, 0, 0.98), (0.11, 0, 0.54), "Hips"),
    "LowerLeg.L": ((0.11, 0, 0.54), (0.11, 0.02, 0.10), "UpperLeg.L"),
    "Foot.L": ((0.11, 0.02, 0.10), (0.11, -0.14, 0.02), "LowerLeg.L"),
    "UpperLeg.R": ((-0.10, 0, 0.98), (-0.11, 0, 0.54), "Hips"),
    "LowerLeg.R": ((-0.11, 0, 0.54), (-0.11, 0.02, 0.10), "UpperLeg.R"),
    "Foot.R": ((-0.11, 0.02, 0.10), (-0.11, -0.14, 0.02), "LowerLeg.R"),

    # Props. The sword points forward out of the right fist; the shield sits on the left forearm.
    "Weapon.R": ((-0.30, -0.04, 0.85), (-0.30, -0.20, 0.85), "Hand.R"),
    "Shield.L": ((0.34, -0.01, 1.00), (0.34, -0.01, 1.10), "LowerArm.L"),

    # Two-handed grip controls (not deformed). SwordCtrl is where the right wrist goes and how the sword
    # is oriented; it has Hand.R's rest frame, so turning it turns the fist and blade. Grip.L is the left
    # wrist's place on the handle, behind the right fist. The poles keep the elbows down and out.
    "SwordCtrl": ((-0.29, -0.02, 0.90), (-0.30, -0.03, 0.80), "Root"),
    "Grip.L": ((-0.29, 0.07, 0.90), (-0.30, 0.06, 0.80), "SwordCtrl"),
    "Pole.L": ((0.60, 0.45, 0.80), (0.60, 0.45, 0.70), "Chest"),
    "Pole.R": ((-0.60, 0.45, 0.80), (-0.60, 0.45, 0.70), "Chest"),
}

CONTROLS = {"Root", "SwordCtrl", "Grip.L", "Pole.L", "Pole.R"}

# Constraint name shared by every two-handed grip constraint, so clips can key one influence.
GRIP = "Grip"

CONNECTED = {"Spine", "Chest", "Neck", "Head", "LowerArm.L", "LowerArm.R", "Hand.L", "Hand.R",
             "LowerLeg.L", "LowerLeg.R", "Foot.L", "Foot.R"}

# Empties parented to bones: blade sample points for hit detection and the lock-on point.
SOCKETS = {
    "Blade_Base": ("Weapon.R", (-0.30, -0.25, 0.85)),
    "Blade_Mid": ("Weapon.R", (-0.30, -0.62, 0.85)),
    "Blade_Tip": ("Weapon.R", (-0.30, -1.00, 0.85)),
    "LockPoint": ("Chest", (0, 0, 1.35)),
}


def build_armature(name="Knight"):
    data = bpy.data.armatures.new(name + "_Rig")
    rig = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)

    bpy.ops.object.mode_set(mode="EDIT")
    edit = data.edit_bones
    for bone_name, (head, tail, parent) in BONES.items():
        b = edit.new(bone_name)
        b.head = Vector(head)
        b.tail = Vector(tail)
        # Predictable local axes: Z points to the character's front (-Y) or up for flat bones.
        b.align_roll(Vector((0, -1, 0)) if abs(b.vector.normalized().y) < 0.9 else Vector((0, 0, 1)))
        if parent:
            b.parent = edit[parent]
            b.use_connect = bone_name in CONNECTED
    bpy.ops.object.mode_set(mode="OBJECT")

    for bone in data.bones:
        bone.use_deform = bone.name not in CONTROLS

    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"

    _add_grip_constraints(rig)
    return rig


def _add_grip_constraints(rig):
    """IK both arms onto the sword controls. Clips key the influence: 0 = free FK arms, 1 = two-handed."""
    for side, target in (("R", "SwordCtrl"), ("L", "Grip.L")):
        ik = rig.pose.bones["LowerArm." + side].constraints.new("IK")
        ik.name = GRIP
        ik.target, ik.subtarget = rig, target
        ik.pole_target, ik.pole_subtarget = rig, "Pole." + side
        ik.pole_angle = POLE_ANGLE[side]
        ik.chain_count = 2
        ik.influence = 0.0
        rot = rig.pose.bones["Hand." + side].constraints.new("COPY_ROTATION")
        rot.name = GRIP
        rot.target, rot.subtarget = rig, target
        rot.influence = 0.0


POLE_ANGLE = {"R": math.radians(-90), "L": math.radians(-90)}


def grip_constraints(rig):
    return [c for pb in rig.pose.bones for c in pb.constraints if c.name == GRIP]


def add_sockets(rig):
    from mathutils import Matrix
    sockets = {}
    for name, (bone_name, world_pos) in SOCKETS.items():
        empty = bpy.data.objects.new(name, None)
        empty.empty_display_size = 0.05
        bpy.context.scene.collection.objects.link(empty)
        empty.parent = rig
        empty.parent_type = "BONE"
        empty.parent_bone = bone_name
        bpy.context.view_layer.update()
        empty.matrix_world = Matrix.Translation(world_pos)
        sockets[name] = empty
    bpy.context.view_layer.update()
    return sockets
