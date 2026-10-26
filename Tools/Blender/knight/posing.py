"""
Pose helpers that work in intuitive armature axes instead of per-bone local axes.

A pose is a dict of bone -> rotation. Rotations are XYZ euler degrees around the ARMATURE axes,
applied relative to the parent's posed frame (forward kinematics). The character faces -Y, so:

    X+  pitch: the part's upper end moves forward (spine leans forward, a hanging arm swings BACK)
    Y+  roll:  the part's upper end tilts to the character's left (+X)
    Z+  yaw:   the part turns to the character's left

Wrap a rotation in Abs(...) to set the bone's absolute orientation (ignoring its parents).
The special key "Hips.loc" offsets the hips in metres (armature axes). "Sword.loc" places the
two-handed grip control (the right wrist) at an absolute position in armature space.
"LeftGrip.loc" (with an optional "LeftGrip.rot" quaternion, absolute) moves the left hand's grip
control instead, so a clip keying the left grip influence can put the left wrist anywhere (the
nano-injector heal slams the left fist onto the right gauntlet this way).
"""

import math

from mathutils import Euler, Matrix, Quaternion, Vector


class Abs:
    """Absolute orientation. `order` is the euler order, e.g. "YXZ" = roll, then pitch, then yaw."""

    def __init__(self, x=0.0, y=0.0, z=0.0, order="XYZ"):
        self.euler = (x, y, z)
        self.order = order


def euler_q(x, y, z, order="XYZ"):
    return Euler((math.radians(x), math.radians(y), math.radians(z)), order).to_quaternion()


def merge(*poses, **overrides):
    """Combines poses left to right; later values win."""
    out = {}
    for p in poses:
        out.update(p)
    out.update({k.replace("_", "."): v for k, v in overrides.items()})
    return out


def add(base, delta):
    """Adds per-axis degrees of `delta` to `base` (relative rotations only)."""
    out = dict(base)
    for bone, value in delta.items():
        if bone == "Hips.loc":
            b = out.get(bone, (0, 0, 0))
            out[bone] = tuple(b[i] + value[i] for i in range(3))
        elif isinstance(value, Abs) or isinstance(out.get(bone), Abs):
            out[bone] = value
        else:
            b = out.get(bone, (0, 0, 0))
            out[bone] = tuple(b[i] + value[i] for i in range(3))
    return out


def mirror(pose):
    """Mirrors a pose left/right (swap .L/.R, negate Y and Z rotations and X offset)."""
    out = {}
    for bone, value in pose.items():
        name = bone.replace(".L", ".TMP").replace(".R", ".L").replace(".TMP", ".R")
        if bone == "Hips.loc":
            out[bone] = (-value[0], value[1], value[2])
        elif isinstance(value, Abs):
            x, y, z = value.euler
            out[name] = Abs(x, -y, -z, value.order)
        else:
            x, y, z = value
            out[name] = (x, -y, -z)
    return out


# Bones whose position a pose may set, and the pose key holding it.
LOCATED = {"SwordCtrl": "Sword.loc"}

# Scales the vertical hips offset of every pose, for rigs with shorter legs than the knight's
# (see gunner_model.shorten_rig), so crouches and falls still reach the ground.
HIPS_Z_SCALE = 1.0


def apply_pose(rig, pose, previous=None):
    """Writes a pose to the rig's pose bones. Returns the quaternions used (for continuity)."""
    totals = {}
    used = {}
    for pb in rig.pose.bones:
        bone = pb.bone
        rest = bone.matrix_local.to_3x3()
        parent_total = totals.get(bone.parent.name, Quaternion()) if bone.parent else Quaternion()

        value = pose.get(bone.name)
        if value is None:
            relative = Quaternion()
        elif isinstance(value, Abs):
            relative = parent_total.inverted() @ euler_q(*value.euler, order=value.order)
        else:
            relative = euler_q(*value)

        totals[bone.name] = parent_total @ relative
        local = (rest.inverted() @ relative.to_matrix() @ rest).to_quaternion()
        if previous is not None and bone.name in previous and previous[bone.name].dot(local) < 0:
            local.negate()
        pb.rotation_quaternion = local
        used[bone.name] = local

        if bone.name == "Hips":
            offset = Vector(pose.get("Hips.loc", (0, 0, 0)))
            offset.z *= HIPS_Z_SCALE
            pb.location = rest.inverted() @ offset
        elif bone.name in LOCATED and LOCATED[bone.name] in pose:
            pb.location = rest.inverted() @ (Vector(pose[LOCATED[bone.name]]) - bone.head_local)
        else:
            pb.location = (0, 0, 0)
    _place_left_grip(rig, pose)
    return used


def _place_left_grip(rig, pose):
    """Puts the Grip.L control at pose["LeftGrip.loc"] (armature space), oriented by pose["LeftGrip.rot"]."""
    target = pose.get("LeftGrip.loc")
    if target is None or "Grip.L" not in rig.pose.bones:
        return
    pb = rig.pose.bones["Grip.L"]
    rotation = pose.get("LeftGrip.rot", Quaternion())
    desired = Matrix.Translation(Vector(target)) @ (rotation.to_matrix() @ pb.bone.matrix_local.to_3x3()).to_4x4()
    # Solved by hand from the parents' basis matrices: a depsgraph update here would re-evaluate the
    # action being keyed and overwrite the pose.
    parent = pb.parent
    rest_offset = parent.bone.matrix_local.inverted() @ pb.bone.matrix_local
    pb.matrix_basis = (_posed(parent) @ rest_offset).inverted() @ desired


def _posed(pb):
    """Armature-space matrix of a pose bone from its own and its parents' basis (no constraints)."""
    if pb.parent is None:
        return pb.bone.matrix_local @ pb.matrix_basis
    offset = pb.parent.bone.matrix_local.inverted() @ pb.bone.matrix_local
    return _posed(pb.parent) @ offset @ pb.matrix_basis
