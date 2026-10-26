"""
All knight animations, authored as key poses (see posing.py for the axis conventions).

Clip names here are the contract with Unity: the setup tool builds the Animator Controller
from them and AnimatorStates.cs refers to the action states by the same names.
Durations of attacks, rolls, parry and reactions must match the values in the Unity data
(AttackData assets and the ActionCharacter settings).
"""

import math

import bpy
from mathutils import Quaternion, Vector

from . import rig as rig_module
from .posing import LOCATED, Abs, add, apply_pose, euler_q, merge, mirror

FPS = 30
HIPS_HEIGHT = 0.98  # rest height of the Hips bone head, see rig.py

# --------------------------------------------------------------------------------------------
# Base poses
# --------------------------------------------------------------------------------------------

LEGS_STANCE = {
    "Hips.loc": (0, 0, -0.04),
    "UpperLeg.L": (-14, -3, 0), "LowerLeg.L": (18, 0, 0), "Foot.L": (-4, 0, 0),
    "UpperLeg.R": (8, 3, 0), "LowerLeg.R": (12, 0, 0), "Foot.R": (-20, 0, 0),
}

SWORD_READY = {
    "UpperArm.R": (-20, -5, 0), "LowerArm.R": (-50, 0, 0), "Hand.R": (40, 0, 0),
}

SHIELD_READY = {
    "UpperArm.L": (-15, 10, 0), "LowerArm.L": (-55, 0, -30),
}

TORSO_STANCE = {"Spine": (6, 0, 0), "Chest": (3, 0, 0), "Head": (-6, 0, 0)}

ARMS_READY = merge(SWORD_READY, SHIELD_READY)
STANCE = merge(LEGS_STANCE, TORSO_STANCE, ARMS_READY)

# Shield raised in front of the chest, face forward. The forearm lies across the body.
GUARD_ARMS = merge(SWORD_READY, {
    "UpperArm.L": (-62, 0, -18),
    "LowerArm.L": Abs(-90, 90, 20, order="YXZ"),
    "Hand.L": (0, 0, 0),
    "UpperArm.R": (-25, -5, 0), "LowerArm.R": (-55, 0, 0), "Hand.R": (45, 0, 0),
})
GUARD = merge(STANCE, GUARD_ARMS, {"Chest": (8, 0, -8), "Head": (-10, 0, 8)})


def sword(pitch=0, roll=0, yaw=0):
    """Absolute sword direction: pitch + = tip down, yaw + = tip to the left, roll 90 = flat blade."""
    return Abs(pitch, roll, yaw, order="YXZ")


def grip(wrist, pitch=0, roll=0, yaw=0):
    """Both hands on the hilt: the right wrist at `wrist` (armature space), the blade as in sword()."""
    return {"Sword.loc": wrist, "SwordCtrl": sword(pitch, roll, yaw)}


# Two-handed weapons (the "_2H" clips). At rest the right fist holds the hilt in front of the chest,
# the pommel points forward and down and the flat of the blade lies on the right shoulder, rising
# back past the head; the left arm hangs free. Swings take the hilt in both fists.
SHOULDER_REST = grip((-0.30, -0.40, 1.24), pitch=-144, roll=-50, yaw=5)
LEFT_ARM_RELAXED = {"UpperArm.L": (5, 4, 6), "LowerArm.L": (-25, 0, -10)}
STANCE_2H = merge(LEGS_STANCE, TORSO_STANCE, LEFT_ARM_RELAXED, SHOULDER_REST, {"Chest": (3, 0, -6), "Head": (-6, 0, 6)})
# The blade held up in front: swings pass through here when they come from or go back to the shoulder,
# so the blade never cuts through the body on the way.
PRESENT_2H = merge(STANCE_2H, grip((-0.10, -0.50, 1.25), pitch=-70, roll=-30, yaw=25))
# Two-handed guard: the blade held upright in front, flat toward the enemy.
GUARD_2H = merge(STANCE_2H, {"Chest": (8, 0, -6), "Head": (-10, 0, 6),
                             **grip((-0.06, -0.38, 1.05), pitch=-68, roll=90, yaw=-14)})


# --------------------------------------------------------------------------------------------
# Action helpers
# --------------------------------------------------------------------------------------------

_ACTIONS = []


def _action(rig, name, keys, loop=False, ease=None, grip=None, left=None):
    """
    keys: list of (frame, pose). Frames start at 0. Looping clips repeat their first key.
    ease: optional {frame: (interpolation, easing)} for the segment that starts at that key,
    e.g. {11: ("QUAD", "EASE_IN")} to accelerate into a strike. Other segments stay Bezier.
    grip: influence of the two-handed grip, for the whole clip or as {frame: influence} at keys
    (default 0, free arms).
    left: the same for the left hand alone (default: as `grip`), e.g. 0 to keep a one-fisted hold.
    Every clip keys it, because a clip without the curve would inherit the previous clip's value.
    """
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action

    keys = sorted(keys, key=lambda k: k[0])
    if loop and keys[-1][1] is not keys[0][1]:
        raise ValueError(f"{name}: last key of a loop must be the first pose")

    grip_constraints = [(c, left if left is not None and pb.name.endswith(".L") else grip)
                        for pb in rig.pose.bones for c in pb.constraints if c.name == rig_module.GRIP]
    previous = None
    for frame, pose in keys:
        previous = apply_pose(rig, pose, previous)
        for pb in rig.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
            if pb.name == "Hips" or pb.name in LOCATED or pb.name == "Grip.L":
                pb.keyframe_insert("location", frame=frame, group=pb.name)
        for constraint, weight in grip_constraints:
            constraint.influence = weight if isinstance(weight, float) else (weight or {}).get(frame, 0.0)
            constraint.keyframe_insert("influence", frame=frame)

    if ease:
        _set_easing(action, ease)

    action.frame_range = (keys[0][0], keys[-1][0])
    action.use_frame_range = True
    action.use_cyclic = loop
    _ACTIONS.append(action)
    return action


def _fcurves(action):
    """F-curves of an action, for both the legacy and the layered (slotted) action API."""
    legacy = getattr(action, "fcurves", None)
    if legacy is not None:
        return list(legacy)
    curves = []
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                curves.extend(bag.fcurves)
    return curves


def _set_easing(action, ease):
    for curve in _fcurves(action):
        for point in curve.keyframe_points:
            spec = ease.get(int(round(point.co.x)))
            if spec:
                point.interpolation, point.easing = spec


def _cycle(length, poses):
    """Evenly spaced looping keys from a list of poses."""
    step = length / len(poses)
    keys = [(round(i * step), p) for i, p in enumerate(poses)]
    keys.append((length, poses[0]))
    return keys


def _reverse(length, keys):
    return [(length - f, p) for f, p in keys]


# --------------------------------------------------------------------------------------------
# Locomotion
# --------------------------------------------------------------------------------------------

def _stride(amount, knee, lift, bob):
    """Forward stride, four key poses (left contact, passing, right contact, passing)."""
    contact = {
        "Hips.loc": (0, 0, -0.04 - bob),
        "UpperLeg.L": (-amount, 0, 0), "LowerLeg.L": (8, 0, 0), "Foot.L": (-5, 0, 0),
        "UpperLeg.R": (amount * 0.8, 0, 0), "LowerLeg.R": (12, 0, 0), "Foot.R": (-15, 0, 0),
        "Hips": (0, 0, -5), "Spine": (0, 0, 8),
    }
    passing = {
        "Hips.loc": (0, 0, -0.04 + bob),
        "UpperLeg.L": (amount * 0.1, 0, 0), "LowerLeg.L": (6, 0, 0), "Foot.L": (0, 0, 0),
        "UpperLeg.R": (-lift, 0, 0), "LowerLeg.R": (knee, 0, 0), "Foot.R": (-10, 0, 0),
        "Hips": (0, 0, 0), "Spine": (0, 0, 0),
    }
    return [contact, passing, mirror(contact), mirror(passing)]


def _sidestep(amount, knee, bob):
    """Side step toward the character's left (+X)."""
    wide = {
        "Hips.loc": (0, 0, -0.05 - bob),
        "UpperLeg.L": (-8, -amount, 0), "LowerLeg.L": (12, 0, 0), "Foot.L": (0, amount * 0.7, 0),
        "UpperLeg.R": (6, amount * 0.7, 0), "LowerLeg.R": (10, 0, 0), "Foot.R": (-5, -amount * 0.5, 0),
        "Hips": (0, 0, 0),
    }
    gather = {
        "Hips.loc": (0, 0, -0.05 + bob),
        "UpperLeg.L": (-6, -amount * 0.3, 0), "LowerLeg.L": (10, 0, 0),
        "UpperLeg.R": (-20, -amount * 0.2, 0), "LowerLeg.R": (knee, 0, 0), "Foot.R": (-10, 0, 0),
        "Hips": (0, 0, 0),
    }
    narrow = {
        "Hips.loc": (0, 0, -0.05 - bob * 0.5),
        "UpperLeg.L": (-8, amount * 0.2, 0), "LowerLeg.L": (12, 0, 0),
        "UpperLeg.R": (6, -amount * 0.2, 0), "LowerLeg.R": (12, 0, 0),
        "Hips": (0, 0, 0),
    }
    reach = {
        "Hips.loc": (0, 0, -0.05 + bob),
        "UpperLeg.L": (-20, -amount * 0.6, 0), "LowerLeg.L": (knee, 0, 0), "Foot.L": (-10, 0, 0),
        "UpperLeg.R": (6, amount * 0.4, 0), "LowerLeg.R": (10, 0, 0),
        "Hips": (0, 0, 0),
    }
    return [wide, gather, narrow, reach]


TWO_HANDED = "_2H"  # suffix of the clips for two-handed weapons; Unity swaps them in with an override controller


def _with_upper(legs, upper, arm_swing=0.0, lean=0.0):
    """
    Puts an upper body on top of leg poses; the sword arm swings against the right leg.
    A two-handed upper body (with "Sword.loc") carries the grip along with the hips' bob and the lean.
    """
    out = []
    for i, leg in enumerate(legs):
        swing = (1, 0, -1, 0)[i % 4] * arm_swing
        pose = merge(upper, leg)
        pose = add(pose, {"Spine": (lean, 0, 0), "UpperArm.R": (swing, 0, 0), "UpperArm.L": (-swing * 0.5, 0, 0)})
        if "Sword.loc" in upper:
            bob = pose.get("Hips.loc", (0, 0, 0))[2] - LEGS_STANCE["Hips.loc"][2]
            sway = (0.0, -0.006 * lean, 0.0) if i % 2 else (0.004 * arm_swing, -0.006 * lean, 0.0)
            pose = add(pose, {"Sword.loc": (sway[0], sway[1], bob - 0.002 * abs(lean))})
        out.append(pose)
    return out


def _locomotion(rig, stance, upper, sprint_upper, suffix, grip_weight, left_weight=None):
    def act(name, keys):
        _action(rig, name + suffix, keys, loop=True, grip=grip_weight, left=left_weight)

    breathe = add(stance, {"Chest": (3, 0, 0), "Head": (-2, 0, 0), "Hips.loc": (0, 0, -0.008),
                           "UpperArm.R": (-3, 0, 0), "UpperArm.L": (-3, 0, 0)})
    if "Sword.loc" in stance:
        breathe = add(breathe, {"Sword.loc": (0, 0, -0.01)})
    act("Idle", [(0, stance), (30, breathe), (60, stance)])

    act("Walk_F", _cycle(28, _with_upper(_stride(24, 40, 22, 0.015), upper, arm_swing=8)))
    act("Walk_B", _reverse(28, _cycle(28, _with_upper(_stride(18, 35, 20, 0.012), upper, 6, -4))))
    act("Walk_L", _cycle(28, _with_upper(_sidestep(14, 30, 0.012), upper)))
    act("Walk_R", _cycle(28, _with_upper([mirror_legs(p) for p in _sidestep(14, 30, 0.012)], upper)))

    act("Run_F", _cycle(20, _with_upper(_stride(42, 85, 45, 0.035), upper, arm_swing=18, lean=10)))
    act("Run_B", _reverse(20, _cycle(20, _with_upper(_stride(30, 70, 35, 0.03), upper, 12, -6))))
    act("Run_L", _cycle(20, _with_upper(_sidestep(24, 55, 0.03), upper, lean=4)))
    act("Run_R", _cycle(20, _with_upper([mirror_legs(p) for p in _sidestep(24, 55, 0.03)], upper, lean=4)))

    act("Sprint", _cycle(18, _with_upper(_stride(55, 105, 60, 0.05), sprint_upper, arm_swing=35, lean=22)))


def build_locomotion(rig):
    # One-handed: sword ready in the right hand, bracer arm up.
    sprint_upper = merge(TORSO_STANCE, {
        "UpperArm.R": (-10, -8, 0), "LowerArm.R": (-70, 0, 0), "Hand.R": (60, 0, 0),
        "UpperArm.L": (-5, 12, 0), "LowerArm.L": (-70, 0, -20), "Head": (-15, 0, 0),
    })
    _locomotion(rig, STANCE, merge(TORSO_STANCE, ARMS_READY), sprint_upper, "", 0.0)

    # Two-handed: the blade on the shoulder in the right fist, also at a sprint; the left arm swings free.
    upper_2h = {k: v for k, v in STANCE_2H.items() if k not in LEGS_STANCE}
    sprint_2h = merge(upper_2h, {"Head": (-15, 0, 0)})
    _locomotion(rig, STANCE_2H, upper_2h, sprint_2h, TWO_HANDED, 1.0, left_weight=0.0)


def mirror_legs(pose):
    """Mirrors only the lower body, so the sword stays in the right hand."""
    lower = {k: v for k, v in pose.items() if "Arm" not in k and "Hand" not in k}
    upper = {k: v for k, v in pose.items() if k not in lower}
    return merge(mirror(lower), upper)


# --------------------------------------------------------------------------------------------
# Attacks (timings must match the AttackData assets)
# --------------------------------------------------------------------------------------------

LUNGE_LEGS = {
    "Hips.loc": (0, 0, -0.12),
    "UpperLeg.L": (-38, -4, 0), "LowerLeg.L": (38, 0, 0), "Foot.L": (0, 0, 0),
    "UpperLeg.R": (22, 4, 0), "LowerLeg.R": (16, 0, 0), "Foot.R": (-24, 0, 0),
}

# Weight comes from timing: a slow wind-up into a held coil, a strike over 3 frames that
# accelerates into the hit, an overshoot that decelerates, then a long, committed recovery.
INTO_HIT = ("QUAD", "EASE_IN")
OUT_OF_HIT = ("QUAD", "EASE_OUT")


def _attack(rig, name, keys, strike, contact):
    """
    A two-handed swing. From the `strike` key (the end of the coil) the blade accelerates, keeps full
    speed through any arc keys and decelerates after `contact`. The left fist joins the hilt after the
    first key (the shoulder rest) and lets go again into the last.
    """
    left = {f: 1.0 for f, _ in keys}
    left[keys[0][0]] = left[keys[-1][0]] = 0.0
    ease = {f: ("LINEAR", "AUTO") for f, _ in keys if strike < f < contact}
    ease.update({strike: INTO_HIT, contact: OUT_OF_HIT})
    _action(rig, name + TWO_HANDED, keys, ease=ease, grip=1.0, left=left)


def build_attacks_1h(rig):
    """One-handed moveset: quick slashes with the bracer arm held in guard."""
    # Attack_1: horizontal slash right -> left.
    wind = merge(STANCE, SHIELD_READY, {
        "Spine": (4, 0, -20), "Chest": (0, 0, -30), "Head": (-6, 0, 30),
        "UpperArm.R": (-25, 75, -45), "LowerArm.R": (-35, 0, 0), "Hand.R": sword(-5, 90, -140),
    })
    strike = merge(STANCE, LUNGE_LEGS, SHIELD_READY, {
        "Spine": (12, 0, 0), "Chest": (5, 0, 0),
        "UpperArm.R": (-80, 25, 0), "LowerArm.R": (-8, 0, 0), "Hand.R": sword(0, 90, 0),
    })
    follow = merge(STANCE, LUNGE_LEGS, SHIELD_READY, {
        "Spine": (12, 0, 20), "Chest": (4, 0, 30), "Head": (-6, 0, -30),
        "UpperArm.R": (-65, -15, 55), "LowerArm.R": (-15, 0, 0), "Hand.R": sword(5, 90, 110),
    })
    coil = add(wind, {"Hips.loc": (0, 0, -0.03), "Spine": (2, 0, -8), "Chest": (0, 0, -10), "Head": (0, 0, 10),
                      "UpperArm.R": (-5, 8, -10)})
    follow_over = add(follow, {"Spine": (3, 0, 8), "Chest": (2, 0, 10), "Head": (0, 0, -8)})
    # 32 frames (1.07 s), hits frames ~11-17.
    _action(rig, "Attack_1", [(0, STANCE), (6, wind), (11, coil), (14, strike), (17, follow_over), (23, follow),
                              (32, STANCE)], ease={11: INTO_HIT, 14: OUT_OF_HIT})

    # Attack_2: backhand slash left -> right.
    wind2 = merge(STANCE, SHIELD_READY, {
        "Spine": (6, 0, 18), "Chest": (2, 0, 30), "Head": (-6, 0, -30),
        "UpperArm.R": (-60, -25, 60), "LowerArm.R": (-55, 0, 0), "Hand.R": sword(-10, -90, 120),
    })
    strike2 = merge(STANCE, LUNGE_LEGS, SHIELD_READY, {
        "Spine": (12, 0, 0), "Chest": (5, 0, 0),
        "UpperArm.R": (-80, 20, 0), "LowerArm.R": (-8, 0, 0), "Hand.R": sword(0, -90, 0),
    })
    follow2 = merge(STANCE, LUNGE_LEGS, SHIELD_READY, {
        "Spine": (10, 0, -18), "Chest": (4, 0, -30), "Head": (-6, 0, 30),
        "UpperArm.R": (-30, 70, -40), "LowerArm.R": (-15, 0, 0), "Hand.R": sword(5, -90, -120),
    })
    coil2 = add(wind2, {"Hips.loc": (0, 0, -0.03), "Spine": (2, 0, 8), "Chest": (0, 0, 10), "Head": (0, 0, -10)})
    follow2_over = add(follow2, {"Spine": (3, 0, -8), "Chest": (2, 0, -10), "Head": (0, 0, 8)})
    # 31 frames (1.03 s), hits ~10-16.
    _action(rig, "Attack_2", [(0, STANCE), (5, wind2), (10, coil2), (13, strike2), (16, follow2_over), (22, follow2),
                              (31, STANCE)], ease={10: INTO_HIT, 13: OUT_OF_HIT})

    # Attack_3: overhead chop.
    raise_ = merge(STANCE, SHIELD_READY, {
        "Spine": (-10, 0, -5), "Chest": (-8, 0, 0), "Head": (5, 0, 0),
        "UpperArm.R": (-165, 10, 0), "LowerArm.R": (-30, 0, 0), "Hand.R": sword(-140, 0, 0),
    })
    chop = merge(STANCE, LUNGE_LEGS, SHIELD_READY, {
        "Hips.loc": (0, 0, -0.13),
        "Spine": (28, 0, 0), "Chest": (10, 0, 0), "Head": (-20, 0, 0),
        "UpperArm.R": (-85, 5, 0), "LowerArm.R": (-8, 0, 0), "Hand.R": sword(18, 0, 0),
    })
    chop_follow = merge(chop, {
        "Spine": (34, 0, 0), "UpperArm.R": (-55, 5, 0), "Hand.R": sword(20, 0, 0),
    })
    raise_coil = add(raise_, {"Hips.loc": (0, 0, 0.02), "Spine": (-6, 0, 0), "Chest": (-5, 0, 0),
                              "UpperArm.R": (-8, 0, 0)})
    chop_over = add(chop_follow, {"Hips.loc": (0, 0, -0.03), "Spine": (6, 0, 0)})
    # 38 frames (1.27 s), hits ~14-21.
    _action(rig, "Attack_3", [(0, STANCE), (8, raise_), (14, raise_coil), (17, chop), (20, chop_over),
                              (27, chop_follow), (38, STANCE)], ease={14: INTO_HIT, 17: OUT_OF_HIT})

    # Attack_Heavy: big two-step overhead.
    heavy_raise = merge(STANCE, {
        "Hips.loc": (0, 0, -0.02),
        "Spine": (-18, 0, -12), "Chest": (-10, 0, -10), "Head": (12, 0, 10),
        "UpperArm.R": (-175, 20, 0), "LowerArm.R": (-45, 0, 0), "Hand.R": sword(-160, 0, -10),
        "UpperArm.L": (-60, -10, 0), "LowerArm.L": (-60, 0, -20),
        "UpperLeg.L": (-25, -3, 0), "LowerLeg.L": (10, 0, 0),
    })
    heavy_chop = merge(STANCE, LUNGE_LEGS, {
        "Hips.loc": (0, 0, -0.2),
        "UpperLeg.L": (-50, -4, 0), "LowerLeg.L": (55, 0, 0), "Foot.L": (-5, 0, 0),
        "UpperLeg.R": (30, 4, 0), "LowerLeg.R": (25, 0, 0),
        "Spine": (35, 0, 0), "Chest": (15, 0, 0), "Head": (-30, 0, 0),
        "UpperArm.R": (-80, 0, 0), "LowerArm.R": (0, 0, 0), "Hand.R": sword(4, 0, 0),
        "UpperArm.L": (10, 30, 0), "LowerArm.L": (-30, 0, -20),
    })
    heavy_follow = merge(heavy_chop, {"Spine": (42, 0, 0), "UpperArm.R": (-55, 0, 0), "Hand.R": sword(2, 0, 0)})
    heavy_coil = add(heavy_raise, {"Hips.loc": (0, 0, -0.03), "Spine": (-5, 0, -4), "Chest": (-4, 0, -4)})
    heavy_over = add(heavy_follow, {"Hips.loc": (0, 0, -0.03), "Spine": (2, 0, 0)})
    # 50 frames (1.67 s), hits ~22-29; AttackData.chargeAt holds frame 21 while the button is held.
    _action(rig, "Attack_Heavy", [(0, STANCE), (10, heavy_raise), (21, heavy_coil), (25, heavy_chop),
                                  (28, heavy_over), (39, heavy_follow), (50, STANCE)],
            ease={21: INTO_HIT, 25: OUT_OF_HIT})



# Wide, low base for swinging a huge blade: left foot forward, knees bent, weight sunk.
GREAT_LEGS = {
    "Hips.loc": (0, 0, -0.10),
    "UpperLeg.L": (-30, -8, 0), "LowerLeg.L": (32, 0, 0), "Foot.L": (0, 4, 0),
    "UpperLeg.R": (18, 8, 0), "LowerLeg.R": (22, 0, 0), "Foot.R": (-18, -4, 0),
}
DEEP_LUNGE = {
    "Hips.loc": (0, 0, -0.24),
    "UpperLeg.L": (-58, -8, 0), "LowerLeg.L": (66, 0, 0), "Foot.L": (-6, 0, 0),
    "UpperLeg.R": (32, 8, 0), "LowerLeg.R": (32, 0, 0), "Foot.R": (-22, 0, 0),
}
# The coil before a greatsword strike: weight on the right leg, the lead foot lifted, ready to stomp
# forward into DEEP_LUNGE as the blade comes down (AttackData moves the knight forward in sync).
STEP_LIFT = {
    "Hips.loc": (0, 0, -0.04),
    "UpperLeg.L": (-72, -6, 0), "LowerLeg.L": (96, 0, 0), "Foot.L": (14, 0, 0),
    "UpperLeg.R": (6, 6, 0), "LowerLeg.R": (16, 0, 0), "Foot.R": (-12, -4, 0),
}


def build_attacks_2h(rig):
    """
    Two-handed greatsword moveset after Elden Ring's ultra greatswords: every swing has a long,
    readable wind-up, the whole body turns into the blow, and the blade's weight drags the
    follow-through low before a slow recovery.
    """
    # Attack_1: diagonal cleave from over the right shoulder down to the left hip.
    wind = merge(STANCE, GREAT_LEGS, {
        "Spine": (0, 0, -28), "Chest": (-4, 0, -26), "Head": (-4, 0, 50),
        **grip((-0.40, 0.10, 1.48), pitch=-140, roll=-30, yaw=50),
    })
    coil = merge(add(wind, {"Spine": (0, 0, -8), "Chest": (0, 0, -6), "Head": (0, 0, 12),
                            "Sword.loc": (-0.03, 0.04, 0.03)}), STEP_LIFT)
    # The blade swings out to the right and comes around flat, so its arc reads from above.
    arc = merge(STANCE, GREAT_LEGS, {
        "Hips.loc": (0, 0, -0.16),
        "Spine": (10, 0, -16), "Chest": (2, 0, -12), "Head": (-8, 0, 28),
        **grip((-0.30, -0.42, 1.30), pitch=-25, roll=-50, yaw=-70),
    })
    strike = merge(STANCE, DEEP_LUNGE, {
        "Spine": (18, 0, 8), "Chest": (6, 0, 10), "Head": (-14, 0, -18),
        **grip((0.04, -0.50, 1.00), pitch=18, roll=-70, yaw=45),
    })
    follow = merge(STANCE, DEEP_LUNGE, {
        "Spine": (24, 0, 28), "Chest": (8, 0, 24), "Head": (-20, 0, -48),
        **grip((0.36, -0.36, 0.74), pitch=20, roll=-85, yaw=78),
    })
    follow_over = add(follow, {"Hips.loc": (0, 0, -0.03), "Spine": (4, 0, 6), "Sword.loc": (0.03, 0.03, -0.04)})
    keys = [(0, STANCE_2H), (9, wind), (14, coil), (16, arc), (18, strike), (21, follow_over), (29, follow),
            (37, PRESENT_2H), (44, STANCE_2H)]
    # 44 frames (1.47 s), hits frames ~16-22.
    _attack(rig, "Attack_1", keys, strike=14, contact=18)

    # Attack_2: the blade comes back around, a flat sweep from the left to the right at waist height.
    wind2 = merge(STANCE, GREAT_LEGS, {
        "Spine": (10, 0, 30), "Chest": (4, 0, 28), "Head": (-10, 0, -55),
        **grip((0.30, -0.12, 1.06), pitch=-8, roll=80, yaw=115),
    })
    coil2 = merge(add(wind2, {"Spine": (0, 0, 8), "Chest": (0, 0, 6), "Head": (0, 0, -12),
                              "Sword.loc": (0.03, 0.04, 0)}), STEP_LIFT)
    strike2 = merge(STANCE, DEEP_LUNGE, {
        "Spine": (14, 0, -4), "Chest": (4, 0, -8), "Head": (-12, 0, 10),
        **grip((-0.02, -0.55, 1.08), pitch=0, roll=90, yaw=-20),
    })
    follow2 = merge(STANCE, DEEP_LUNGE, {
        "Spine": (16, 0, -32), "Chest": (6, 0, -26), "Head": (-12, 0, 55),
        **grip((-0.40, -0.30, 1.02), pitch=10, roll=90, yaw=-125),
    })
    follow2_over = add(follow2, {"Hips.loc": (0, 0, -0.03), "Spine": (2, 0, -6), "Sword.loc": (-0.03, 0.03, -0.02)})
    # Out to the right in front of the body, before it comes back up to the shoulder.
    settle2 = merge(follow2, {"Spine": (12, 0, -18), "Chest": (4, 0, -14), "Head": (-10, 0, 30),
                              **grip((-0.32, -0.48, 1.10), pitch=-15, roll=90, yaw=-80)})
    keys2 = [(0, STANCE_2H), (4, PRESENT_2H), (8, wind2), (13, coil2), (16, strike2), (19, follow2_over),
             (26, follow2), (30, settle2), (36, PRESENT_2H), (42, STANCE_2H)]
    # 42 frames (1.4 s), hits ~14-20.
    _attack(rig, "Attack_2", keys2, strike=13, contact=16)

    # Attack_3: the finisher, an overhead slam that buries the blade in the ground.
    raise3 = merge(STANCE, GREAT_LEGS, {
        "Spine": (-14, 0, -6), "Chest": (-10, 0, 0), "Head": (6, 0, 6),
        **grip((-0.12, -0.08, 1.78), pitch=-150, roll=0, yaw=0),
    })
    coil3 = merge(add(raise3, {"Spine": (-6, 0, 0), "Chest": (-4, 0, 0),
                               "Sword.loc": (0, 0.06, 0.02), "SwordCtrl": Abs(-160, 0, 0, order="YXZ")}), STEP_LIFT)
    # Over the top: the blade passes straight up in front, so the swing reads as one big arc.
    over3 = merge(STANCE, GREAT_LEGS, {
        "Hips.loc": (0, 0, -0.14),
        "Spine": (12, 0, 0), "Chest": (6, 0, 0), "Head": (-10, 0, 0),
        **grip((-0.05, -0.50, 1.52), pitch=-75, roll=0, yaw=0),
    })
    slam = merge(STANCE, DEEP_LUNGE, {
        "Spine": (34, 0, 0), "Chest": (12, 0, 0), "Head": (-26, 0, 0),
        **grip((-0.04, -0.58, 0.80), pitch=22, roll=0, yaw=0),
    })
    buried = merge(slam, {"Hips.loc": (0, 0, -0.28), "Spine": (40, 0, 0), **grip((-0.04, -0.55, 0.64), pitch=19)})
    keys3 = [(0, STANCE_2H), (12, raise3), (19, coil3), (21, over3), (23, slam), (26, buried), (36, buried),
             (46, PRESENT_2H), (54, STANCE_2H)]
    # 54 frames (1.8 s), hits ~21-26.
    _attack(rig, "Attack_3", keys3, strike=19, contact=23)

    # Attack_Heavy: the blade is hauled up behind the head and held (the charge point, frame 24),
    # then brought down in one full-body slam.
    heavy_raise = merge(STANCE, GREAT_LEGS, {
        "Hips.loc": (0, 0, -0.06),
        "Spine": (-20, 0, -22), "Chest": (-12, 0, -14), "Head": (14, 0, 34),
        **grip((-0.24, 0.0, 1.72), pitch=-165, roll=-10, yaw=25),
    })
    heavy_coil = merge(add(heavy_raise, {"Spine": (-6, 0, -4), "Chest": (-4, 0, -4),
                                         "Sword.loc": (0.06, -0.02, -0.05)}), STEP_LIFT)
    heavy_over = merge(STANCE, GREAT_LEGS, {
        "Hips.loc": (0, 0, -0.16),
        "Spine": (14, 0, 0), "Chest": (6, 0, 0), "Head": (-12, 0, 0),
        **grip((-0.05, -0.52, 1.55), pitch=-78, roll=0, yaw=0),
    })
    heavy_slam = merge(STANCE, DEEP_LUNGE, {
        "Hips.loc": (0, 0, -0.30),
        "UpperLeg.L": (-66, -8, 0), "LowerLeg.L": (78, 0, 0),
        "Spine": (42, 0, 0), "Chest": (14, 0, 0), "Head": (-32, 0, 0),
        **grip((-0.04, -0.62, 0.70), pitch=20, roll=0, yaw=0),
    })
    heavy_buried = merge(heavy_slam, {"Hips.loc": (0, 0, -0.34), "Spine": (46, 0, 0),
                                      **grip((-0.04, -0.58, 0.56), pitch=16)})
    heavy_keys = [(0, STANCE_2H), (14, heavy_raise), (24, heavy_coil), (26, heavy_over), (28, heavy_slam),
                  (31, heavy_buried), (44, heavy_buried), (55, PRESENT_2H), (64, STANCE_2H)]
    # 64 frames (2.13 s), hits ~26-31; AttackData.chargeAt holds frame 24 while the button is held.
    _attack(rig, "Attack_Heavy", heavy_keys, strike=24, contact=28)


# --------------------------------------------------------------------------------------------
# Dodges
# --------------------------------------------------------------------------------------------

TUCK = {
    "UpperLeg.L": (-110, 0, 0), "LowerLeg.L": (130, 0, 0), "Foot.L": (-20, 0, 0),
    "UpperLeg.R": (-100, 0, 0), "LowerLeg.R": (125, 0, 0), "Foot.R": (-20, 0, 0),
    "Spine": (30, 0, 0), "Chest": (20, 0, 0), "Head": (25, 0, 0),
    "UpperArm.R": (-50, -10, 0), "LowerArm.R": (-80, 0, 0), "Hand.R": (40, 0, 0),
    "UpperArm.L": (-60, 10, 0), "LowerArm.L": (-80, 0, -20),
}


def _roll(axis, sign):
    """Tumble on `axis` (0 = forward/back, 1 = sideways) around the body's centre. 24 frames (0.8 s)."""
    def spin(deg):
        r = [0, 0, 0]
        r[axis] = deg * sign
        return tuple(r)

    def tumble(deg, centre_height=0.42, centre_offset=0.22):
        # Keep the torso centre (just above the hips) at a fixed height while the hips rotate.
        c = euler_q(*spin(deg)) @ Vector((0, 0, centre_offset))
        return {"Hips": spin(deg), "Hips.loc": (-c.x, -c.y, centre_height - HIPS_HEIGHT - c.z)}

    crouch = merge(STANCE, {"Hips.loc": (0, 0, -0.25), "Hips": spin(25),
                            "UpperLeg.L": (-60, 0, 0), "LowerLeg.L": (80, 0, 0),
                            "UpperLeg.R": (-40, 0, 0), "LowerLeg.R": (70, 0, 0)})
    keys = [(0, STANCE), (3, crouch)]
    for i, deg in enumerate(range(70, 361, 50)):
        keys.append((5 + i * 2, merge(TUCK, tumble(deg))))
    rise = merge(STANCE, {"Hips.loc": (0, 0, -0.22), "Hips": spin(380),
                          "UpperLeg.L": (-70, 0, 0), "LowerLeg.L": (90, 0, 0),
                          "UpperLeg.R": (-20, 0, 0), "LowerLeg.R": (60, 0, 0), "Spine": (20, 0, 0)})
    keys += [(18, rise), (24, STANCE)]
    return keys


def build_dodges(rig):
    _action(rig, "Roll_F", _roll(0, 1))
    _action(rig, "Roll_B", _roll(0, -1))
    _action(rig, "Roll_L", _roll(1, 1))
    _action(rig, "Roll_R", _roll(1, -1))

    hop = merge(STANCE, {"Hips.loc": (0, 0, 0.04), "Spine": (18, 0, 0),
                         "UpperLeg.L": (-5, 0, 0), "LowerLeg.L": (35, 0, 0),
                         "UpperLeg.R": (5, 0, 0), "LowerLeg.R": (40, 0, 0), "Foot.R": (-10, 0, 0)})
    land = merge(STANCE, {"Hips.loc": (0, 0, -0.12), "Spine": (14, 0, 0),
                          "UpperLeg.L": (-35, 0, 0), "LowerLeg.L": (45, 0, 0),
                          "UpperLeg.R": (-5, 0, 0), "LowerLeg.R": (40, 0, 0)})
    _action(rig, "Backstep", [(0, STANCE), (4, hop), (9, land), (15, STANCE)])

    legs = lambda pose: {k: v for k, v in pose.items() if k in LEGS_STANCE}  # noqa: E731
    hop2 = merge(STANCE_2H, legs(hop), {"Sword.loc": _lift(SHOULDER_REST, 0.06)})
    land2 = merge(STANCE_2H, legs(land), {"Sword.loc": _lift(SHOULDER_REST, -0.07)})
    _action(rig, "Backstep" + TWO_HANDED, [(0, STANCE_2H), (4, hop2), (9, land2), (15, STANCE_2H)], grip=1.0, left=0.0)


# --------------------------------------------------------------------------------------------
# Defence and reactions
# --------------------------------------------------------------------------------------------

def _lift(grip_pose, dz):
    """The grip's wrist moved up or down, e.g. to follow the hips."""
    x, y, z = grip_pose["Sword.loc"]
    return (x, y, z + dz)


def build_defence_2h(rig):
    """Guard, block and flinch for two-handed weapons: the blade is held upright as a shield."""
    guard_breathe = add(GUARD_2H, {"Chest": (2, 0, 0)})
    _action(rig, "Guard_Idle" + TWO_HANDED, [(0, GUARD_2H), (20, guard_breathe), (40, GUARD_2H)], loop=True,
            grip=1.0)

    recoil = merge(GUARD_2H, {"Spine": (-14, 0, 0), "Hips.loc": (0, 0, -0.07),
                              "UpperLeg.L": (-22, 0, 0), "LowerLeg.L": (28, 0, 0),
                              "UpperLeg.R": (14, 0, 0), "LowerLeg.R": (25, 0, 0),
                              **grip((-0.06, -0.28, 1.02), pitch=-74, roll=90, yaw=-14)})
    _action(rig, "Block_Hit" + TWO_HANDED, [(0, GUARD_2H), (3, recoil), (11, GUARD_2H)], grip=1.0)

    flinch = merge(STANCE_2H, {"Spine": (-18, 0, 5), "Chest": (-10, 0, 0), "Head": (15, 0, 0),
                               "Hips.loc": (0, 0, -0.06), "Sword.loc": _lift(SHOULDER_REST, -0.03)})
    _action(rig, "Hit_React" + TWO_HANDED, [(0, STANCE_2H), (3, flinch), (15, STANCE_2H)], grip=1.0, left=0.0)


# The parry pistol aimed straight ahead at chest height: the left shoulder turns forward, the arm
# is out straight. Merged over either stance, so the right hand keeps its sword.
GUN_AIM = {"Chest": (3, 0, -25), "Head": (-6, 0, 29),
           "UpperArm.L": (-85, -30, 20), "LowerArm.L": (-6, 0, 0), "Hand.L": (0, 0, 0)}
GUN_RECOIL = {"Chest": (-3, 0, -20), "Head": (-2, 0, 25),
              "UpperArm.L": (-102, -30, 20), "LowerArm.L": (-14, 0, 0), "Hand.L": (-28, 0, 0)}


def build_shots(rig):
    """
    Shoot: draw and fire the parry pistol from the left hand. 20 frames (0.67 s), the shot leaves
    the muzzle on frame 5 (ParrySettings.shotAt); Unity shows the pistol in hand for the clip.
    """
    for stance, suffix, grip, left in ((STANCE, "", 0.0, None), (STANCE_2H, TWO_HANDED, 1.0, 0.0)):
        aim = merge(stance, GUN_AIM)
        recoil = merge(stance, GUN_RECOIL)
        settle = add(aim, {"UpperArm.L": (-3, 0, 0)})
        _action(rig, "Shoot" + suffix, [(0, stance), (4, aim), (5, recoil), (9, settle), (13, aim), (20, stance)],
                ease={4: ("CONSTANT", "AUTO")}, grip=grip, left=left)


def build_defence(rig):
    guard_breathe = add(GUARD, {"Chest": (2, 0, 0)})
    _action(rig, "Guard_Idle", [(0, GUARD), (20, guard_breathe), (40, GUARD)], loop=True)

    # Block_Hit: the guard layer keeps the arms up; spine and legs absorb the impact. 11 frames.
    recoil = merge(GUARD, {"Spine": (-14, 0, 0), "Hips.loc": (0, 0, -0.07),
                           "UpperLeg.L": (-22, 0, 0), "LowerLeg.L": (28, 0, 0),
                           "UpperLeg.R": (14, 0, 0), "LowerLeg.R": (25, 0, 0)})
    _action(rig, "Block_Hit", [(0, GUARD), (3, recoil), (11, GUARD)])

    # Guard_Break: arms flung wide, stumble back. 33 frames (1.1 s).
    flung = merge(STANCE, {"Spine": (-25, 0, 0), "Chest": (-10, 0, 0), "Head": (15, 0, 0),
                           "UpperArm.L": (-40, -70, 0), "LowerArm.L": (-20, 0, 0),
                           "UpperArm.R": (-40, 70, 0), "LowerArm.R": (-20, 0, 0),
                           "UpperLeg.L": (10, 0, 0), "LowerLeg.L": (20, 0, 0),
                           "UpperLeg.R": (-25, 0, 0), "LowerLeg.R": (25, 0, 0)})
    slump = merge(STANCE, {"Hips.loc": (0, 0, -0.1), "Spine": (20, 0, 0), "Head": (10, 0, 0),
                           "UpperArm.L": (5, -15, 0), "LowerArm.L": (-20, 0, 0),
                           "UpperArm.R": (5, 15, 0), "LowerArm.R": (-20, 0, 0)})
    _action(rig, "Guard_Break", [(0, GUARD), (4, flung), (12, flung), (22, slump), (33, STANCE)])

    # Parry: quick outward sweep of the shield. 24 frames (0.8 s); active ~frames 2-10.
    sweep = merge(STANCE, {"Spine": (8, 0, 15), "Chest": (4, 0, 25),
                           "UpperArm.L": (-70, -40, 0), "LowerArm.L": Abs(-90, 90, 50, order="YXZ")})
    sweep_out = merge(sweep, {"Chest": (4, 0, 35), "UpperArm.L": (-60, -65, 0),
                              "LowerArm.L": Abs(-90, 90, 85, order="YXZ")})
    _action(rig, "Parry", [(0, STANCE), (3, sweep), (9, sweep_out), (14, sweep_out), (24, STANCE)])

    # Hit_React: flinch. 15 frames (0.5 s).
    flinch = merge(STANCE, {"Spine": (-18, 0, 5), "Chest": (-10, 0, 0), "Head": (15, 0, 0),
                            "UpperArm.L": (0, 15, 0), "UpperArm.R": (0, -10, 0), "Hips.loc": (0, 0, -0.06)})
    _action(rig, "Hit_React", [(0, STANCE), (3, flinch), (15, STANCE)])

    # Stagger (after being parried): 48 frames (1.6 s).
    reel = merge(flung, {"Spine": (-30, 0, 10)})
    hunch = merge(STANCE, {"Hips.loc": (0, 0, -0.16), "Spine": (38, 0, 0), "Chest": (10, 0, 0), "Head": (10, 0, 0),
                           "UpperLeg.L": (-40, 0, 0), "LowerLeg.L": (55, 0, 0),
                           "UpperLeg.R": (-10, 0, 0), "LowerLeg.R": (50, 0, 0),
                           "UpperArm.L": (-10, -10, 0), "LowerArm.L": (-20, 0, 0),
                           "UpperArm.R": (-10, 10, 0), "LowerArm.R": (-20, 0, 0), "Hand.R": sword(12, 0, -20)})
    sway = add(hunch, {"Spine": (-6, 0, 6)})
    _action(rig, "Stagger", [(0, STANCE), (5, reel), (14, hunch), (26, sway), (38, hunch), (48, STANCE)])

    # Death: knees, then collapse backward. 45 frames (1.5 s), holds the last pose.
    kneel = merge(STANCE, {"Hips.loc": (0, 0, -0.42), "Spine": (10, 0, 0), "Head": (20, 0, 0),
                           "UpperLeg.L": (-10, 0, 0), "LowerLeg.L": (95, 0, 0), "Foot.L": (30, 0, 0),
                           "UpperLeg.R": (-5, 0, 0), "LowerLeg.R": (95, 0, 0), "Foot.R": (30, 0, 0),
                           "UpperArm.L": (10, 10, 0), "LowerArm.L": (-10, 0, 0),
                           "UpperArm.R": (10, -10, 0), "LowerArm.R": (-10, 0, 0), "Hand.R": sword(5, 0, -30)})
    fallen = merge(kneel, {"Hips.loc": (0, 0.35, -0.82), "Hips": (-85, 0, 0), "Spine": (-5, 0, 0), "Head": (-10, 0, 20),
                           "UpperLeg.L": (70, 0, 0), "LowerLeg.L": (20, 0, 0), "Foot.L": (0, 0, 0),
                           "UpperLeg.R": (80, 0, 0), "LowerLeg.R": (10, 0, 0), "Foot.R": (0, 0, 0),
                           "UpperArm.L": (-20, -40, 0), "UpperArm.R": (-20, 40, 0), "Hand.R": sword(0, 90, -60)})
    _action(rig, "Death", [(0, STANCE), (5, flinch), (16, kneel), (30, fallen), (45, fallen)])


def build_jumps(rig):
    """
    Jump: 18 frames (0.6 s), about one jump's airtime. A quick crouch, knees tucked in the air,
    then legs reaching down for the landing; the CharacterController does the actual rising.
    """
    crouch = {"Hips.loc": (0, 0, -0.12), "Spine": (14, 0, 0),
              "UpperLeg.L": (-40, 0, 0), "LowerLeg.L": (60, 0, 0), "Foot.L": (-15, 0, 0),
              "UpperLeg.R": (-20, 0, 0), "LowerLeg.R": (55, 0, 0), "Foot.R": (-25, 0, 0)}
    tuck = {"Hips.loc": (0, 0, 0.05), "Spine": (8, 0, 0),
            "UpperLeg.L": (-70, -4, 0), "LowerLeg.L": (95, 0, 0), "Foot.L": (20, 0, 0),
            "UpperLeg.R": (-30, 4, 0), "LowerLeg.R": (85, 0, 0), "Foot.R": (15, 0, 0)}
    float_ = add(tuck, {"UpperLeg.L": (6, 0, 0), "LowerLeg.L": (-10, 0, 0), "Spine": (-3, 0, 0)})
    reach = {"Hips.loc": (0, 0, 0.0), "Spine": (6, 0, 0),
             "UpperLeg.L": (-30, -3, 0), "LowerLeg.L": (30, 0, 0), "Foot.L": (5, 0, 0),
             "UpperLeg.R": (-8, 3, 0), "LowerLeg.R": (25, 0, 0), "Foot.R": (0, 0, 0)}
    poses = [(0, None), (3, crouch), (7, tuck), (13, float_), (18, reach)]

    _action(rig, "Jump", [(f, STANCE if p is None else merge(STANCE, p)) for f, p in poses])
    lift = {3: -0.1, 7: 0.05, 13: 0.04, 18: 0.0}
    keys_2h = [(0, STANCE_2H)] + [(f, merge(STANCE_2H, p, {"Sword.loc": _lift(SHOULDER_REST, lift[f])}))
                                  for f, p in poses[1:]]
    _action(rig, "Jump" + TWO_HANDED, keys_2h, grip=1.0, left=0.0)


# --------------------------------------------------------------------------------------------
# Nano-injector heal
# --------------------------------------------------------------------------------------------

# The injector port on the right gauntlet's front plate (rest pose, armature space) and the way it faces.
# weapons.build_injector puts the port mesh and the Injector_Port socket here.
PORT_REST = Vector((-0.29, -0.083, 1.035))
PORT_NORMAL_REST = Vector((0, -1, 0))
# From the left wrist to the needle of the cartridge held in the left fist, pointing down out of it.
CARTRIDGE_REACH = 0.19


def _port_frame(rig, pose, grip_weight, left_weight):
    """Where the port is and which way it faces once `pose` is applied with these grip influences."""
    action = rig.animation_data.action if rig.animation_data else None
    if action is not None:
        rig.animation_data.action = None  # or the update below would play it over the pose
    influences = {}
    apply_pose(rig, pose)
    for pb in rig.pose.bones:
        for constraint in pb.constraints:
            if constraint.name == rig_module.GRIP:
                influences[constraint] = constraint.influence
                constraint.influence = left_weight if pb.name.endswith(".L") else grip_weight
    bpy.context.view_layer.update()
    pb = rig.pose.bones["LowerArm.R"]
    to_posed = pb.matrix @ pb.bone.matrix_local.inverted()
    for constraint, influence in influences.items():
        constraint.influence = influence
    if action is not None:
        rig.animation_data.action = action
        if hasattr(rig.animation_data, "action_slot") and action.slots:
            rig.animation_data.action_slot = action.slots[0]
    return to_posed @ PORT_REST, (to_posed.to_3x3() @ PORT_NORMAL_REST).normalized()


def _onto_port(rig, pose, grip_weight, lift, push=(0, 0, 0), twist=0.0):
    """
    `pose` with the left fist above the port, the cartridge pointing into it: `lift` metres of air
    between needle and port (0 = slammed home). `push` nudges the fist (armature axes) and `twist`
    turns it about the cartridge.
    """
    port, normal = _port_frame(rig, pose, grip_weight, 1.0)
    wrist = port + normal * (CARTRIDGE_REACH + lift) + Vector(push)
    # The rest left hand points straight down; turn that onto -normal, then twist about it.
    aim = Vector((0, 0, -1)).rotation_difference(-normal)
    rot = Quaternion(-normal, math.radians(twist)) @ aim
    return merge(pose, {"LeftGrip.loc": tuple(wrist), "LeftGrip.rot": rot})


def build_heal(rig):
    """
    Heal: the nano-injector. 48 frames (1.6 s, NanoInjector.duration). The left hand pulls a cartridge
    from the belt (frame 6, drawAt), raises it over the right gauntlet, slams it into the port
    (frame 20, injectAt), holds it while it empties and tosses it away (frame 30, stowAt); then the
    knight straightens as the nanites spread. The left hand reaches the port through the two-handed
    grip's left IK, placed with "LeftGrip.loc".
    """
    for stance, suffix, two_handed in ((STANCE, "", 0.0), (STANCE_2H, TWO_HANDED, 1.0)):
        # The right forearm is held up across the body, front plate (and port) facing up.
        if two_handed:
            present = merge(stance, grip((-0.16, -0.40, 1.16), pitch=-150, roll=-48, yaw=28))
        else:
            present = merge(stance, {"UpperArm.R": (-30, 8, -35), "LowerArm.R": (-80, 0, 10),
                                     "Hand.R": sword(-20, 90, -60)})
        turn = {"Spine": (8, 0, -10), "Chest": (6, 0, -14), "Head": (16, 0, -6)}
        reach_belt = merge(stance, {"UpperArm.L": (22, -4, 10), "LowerArm.L": (-50, 0, -10), "Hand.L": (10, 0, 0),
                                    "Chest": (3, 0, 6), "Head": (4, 0, 0)})
        raised = _onto_port(rig, merge(present, turn), two_handed, 0.13, push=(0.03, -0.05, 0.0))
        wind = _onto_port(rig, merge(present, turn, {"Chest": (2, 0, -16)}), two_handed, 0.17, push=(0.05, -0.07, 0.0))
        slam_body = merge(present, turn, {"Hips.loc": (0, 0, -0.07), "Spine": (14, 0, -10), "Chest": (8, 0, -14),
                                          "Head": (22, 0, -6),
                                          "UpperLeg.L": (-24, -3, 0), "LowerLeg.L": (34, 0, 0),
                                          "UpperLeg.R": (2, 3, 0), "LowerLeg.R": (24, 0, 0)})
        slam = _onto_port(rig, slam_body, two_handed, -0.01)
        hold = _onto_port(rig, merge(slam_body, {"Hips.loc": (0, 0, -0.06)}), two_handed, -0.005)
        pull = _onto_port(rig, merge(present, turn), two_handed, 0.10, push=(0.10, -0.02, 0.0))
        toss = merge(stance, {"UpperArm.L": (-40, -55, 10), "LowerArm.L": (-25, 0, 0), "Hand.L": (0, 0, -30),
                              "Chest": (4, 0, 8), "Head": (-4, 0, 0)})
        relief = merge(stance, {"Spine": (-4, 0, 0), "Chest": (-8, 0, 0), "Head": (-14, 0, 0),
                                "UpperArm.L": (4, 8, 8), "LowerArm.L": (-20, 0, -8), "Hips.loc": (0, 0, -0.03)})
        keys = [(0, stance), (6, reach_belt), (12, raised), (17, wind), (20, slam), (25, hold), (29, pull),
                (33, toss), (40, relief), (48, stance)]
        left = {0: 0.0, 6: 0.0, 12: 1.0, 17: 1.0, 20: 1.0, 25: 1.0, 29: 1.0, 33: 0.0, 40: 0.0, 48: 0.0}
        right = two_handed
        _action(rig, "Heal" + suffix, keys, ease={17: ("QUAD", "EASE_IN"), 20: ("CONSTANT", "AUTO")},
                grip=right, left=left)


def build_all(rig):
    _ACTIONS.clear()
    build_locomotion(rig)
    build_attacks_1h(rig)
    build_attacks_2h(rig)
    build_dodges(rig)
    build_defence(rig)
    build_defence_2h(rig)
    build_shots(rig)
    build_jumps(rig)
    build_heal(rig)
    apply_pose(rig, {})
    for constraint in rig_module.grip_constraints(rig):
        constraint.influence = 0.0
    rig.animation_data.action = None
    return list(_ACTIONS)
