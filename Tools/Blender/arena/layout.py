"""
The boss arena from the concept art, composed from kit pieces.

Blender arena space: the fight floor is centred on the origin, the temple is to the north
(+Y), the player enters from the south. The layout is exported to JSON in Unity's frame
(see to_unity) so the Unity builder can assemble the same arena from the kit prefabs.
"""

import math
import random

from .pieces_architecture import WALKWAY_RADIUS, plateau_outline

CANYON_Z = -20.0
PILLAR_RING = 15.4


def _facing_center(angle):
    """rot_z that turns a piece's front (-Y) toward the arena centre from `angle` (degrees)."""
    return angle - 90.0


def _polar(angle, r, z=0.0):
    a = math.radians(angle)
    return (math.cos(a) * r, math.sin(a) * r, z)


def build():
    """Returns (placements, spawns, blockers).
    placement = (piece, group, (x, y, z), rot_z, scale) in Blender arena space."""
    P = []

    def put(name, group, loc, rot=0.0, scale=1.0):
        P.append((name, group, tuple(loc), rot, scale))

    # --- ground
    put("Plateau_Large", "Terrain", (0, 0, 0))
    put("Ground_Canyon", "Terrain", (0, 0, CANYON_Z))
    for x, y in ((47, 0), (-47, 0), (0, -47)):
        put("Plateau_Small", "Terrain", (x, y, 0), rot=x * 3 + y)

    # --- arena floor
    put("Floor_Arena_Center", "Floor", (0, 0, 0))
    for k in range(8):
        put("Floor_Arena_Segment", "Floor", (0, 0, 0), rot=k * 45.0)
        put("Floor_Walkway_Segment", "Floor", (0, 0, 0), rot=k * 45.0)

    # --- ring wall with openings east, west, south and a wide one north for the temple stairs
    openings = {0, 180, 270, 75, 90, 105}
    for k in range(24):
        a = k * 15
        if a in openings:
            continue
        put("Wall_Arena_Segment", "Walls", (0, 0, 0), rot=a - 90.0)
    for a in (-7.5, 7.5, 172.5, 187.5, 262.5, 277.5, 67.5, 112.5):
        put("Pillar_Short", "Walls", _polar(a, WALKWAY_RADIUS + 0.4), rot=_facing_center(a))

    # --- stands in four arcs, leaving the south approach open
    for start in (15, 120, 195, 300):
        for k in range(4):
            a = start + k * 15
            put("Stands_Segment", "Walls", (0, 0, 0), rot=a - 90.0)

    # --- banner pillars, awnings and wall pennants
    for k in range(8):
        a = 22.5 + k * 45
        put("Pillar_Banner", "Architecture", _polar(a, PILLAR_RING), rot=_facing_center(a))
    for mid in (45, 135, 225):
        put("Awning_Span_12m", "Cloth", _polar(mid, PILLAR_RING * math.cos(math.radians(22.5)), 6.9),
            rot=mid + 90.0)
    for a in (30, 60, 120, 150, 210, 240, 300, 330):
        put("Banner_Small", "Cloth", _polar(a, WALKWAY_RADIUS - 0.05, 1.08), rot=_facing_center(a))

    # --- temple gate to the north
    temple_y = WALKWAY_RADIUS + 0.2
    put("Temple_Gate", "Architecture", (0, temple_y, 0))
    for x in (-5.6, 5.6):
        put("Banner_Hanging", "Cloth", (x, temple_y + 0.75, 17.2))
    put("Tech_Console", "Tech", (-4.6, temple_y + 0.6, 0), rot=10)

    # --- braziers
    for a in (-10, 10, 170, 190, 260, 280):
        put("Brazier_Standing", "Props", _polar(a, 15.6), rot=_facing_center(a))
    for x in (-5.2, 5.2):
        put("Brazier_Bowl", "Props", (x, 15.4, 0))

    # --- paths and bridges: south entrance, east and west crossings
    for heading in (0, 180, 270):
        rot = heading - 90.0  # path pieces run along Y
        for r in (19.0, 23.0):
            put("Path_Segment", "Floor", _polar(heading, r), rot=rot)
        for r in (29.0, 37.0):
            put("Bridge_Arch_Segment", "Architecture", _polar(heading, r), rot=rot)

    # --- ruins and debris
    for a, r in ((200, 11.2), (338, 11.6), (128, 11.0)):
        put("Pillar_Broken", "Props", _polar(a, r), rot=a * 1.7)
    put("Block_Ruin_A", "Props", (-8.5, -20.5, 0), rot=15)
    put("Block_Ruin_A", "Props", (10.0, -21.5, 0), rot=-40)
    put("Block_Ruin_B", "Props", (-13.0, -19.5, 0), rot=35)
    put("Block_Ruin_B", "Props", (23.5, 6.0, 0), rot=95)
    for loc, rot in (((4.5, -7.5, 0), 10), ((-8.0, 6.0, 0), 70), ((-3.5, -20.0, 0), 0), ((14, -16, 0), 30),
                     ((-23, -6, 0), 50), ((3, 23.5, 0), 0)):
        put("Rubble_Pile", "Props", loc, rot=rot)

    # --- rocks on the plateau
    put("Rock_Cluster", "Nature", (20.5, -14.5, 0), rot=20)
    put("Rock_Cluster", "Nature", (-21.0, -12.5, 0), rot=140)
    put("Rock_Boulder_M", "Nature", (-24.0, 9.0, 0))
    put("Rock_Boulder_M", "Nature", (24.5, 13.0, 0), rot=60)
    for loc in ((6.5, -24.5, 0), (-6.0, -25.5, 0), (25.5, -3.5, 0), (-25.0, 3.5, 0), (16.0, -20.5, 0)):
        put("Rock_Boulder_S", "Nature", loc, rot=loc[0] * 13)

    # --- tech grit
    put("Tech_Vaporator", "Tech", (-18.5, -15.5, 0))
    put("Tech_Vaporator", "Tech", (-20.5, -18.0, 0), rot=40)
    put("Tech_Vaporator", "Tech", (19.0, -18.5, 0), rot=15)
    put("Tech_Generator", "Tech", (-22.0, 7.0, 0), rot=80)
    put("Tech_PipeRun", "Tech", (-22.3, 12.5, 0), rot=-8)
    put("Tech_Antenna", "Tech", (12.0, 25.0, 0))
    put("Tech_Crate_Stack", "Tech", (6.0, -20.5, 0), rot=15)
    put("Tech_Crate_Stack", "Tech", (21.5, -6.5, 0), rot=-70)
    for x in (-18.0, -10.0):
        put("Tech_Pole", "Tech", (x, -23.0, 0))
    put("Tech_Cable_Span", "Tech", (-14.0, -23.0, 3.15))
    put("Tech_Wreck_Hull", "Tech", (-38.0, -30.0, CANYON_Z), rot=35)
    put("Tech_Wreck_Engine", "Tech", (40.0, 26.0, CANYON_Z), rot=-30)

    # --- backdrop
    for name, x, y, rot, s in (
            ("Rock_Mesa_A", -50, 35, 0, 1.0), ("Rock_Mesa_B", 62, 45, 30, 1.0), ("Rock_Mesa_B", -72, -30, 80, 1.2),
            ("Rock_Mesa_A", 58, -55, 140, 1.1), ("Rock_Spire_A", -36, -72, 0, 1.0), ("Rock_Spire_A", 26, 72, 50, 1.2),
            ("Rock_Spire_B", -26, 78, 10, 1.3), ("Rock_Mesa_A", 4, 105, 200, 1.6), ("Rock_Spire_B", 82, -4, 70, 1.0),
            ("Rock_Spire_A", -86, 6, 120, 1.0), ("Rock_Arch", -62, 62, 30, 1.0), ("Rock_Mesa_B", 36, -84, 10, 1.0),
            ("Rock_Spire_B", 20, -66, 200, 0.9), ("Rock_Mesa_A", -95, 60, 90, 1.4), ("Rock_Spire_A", 95, 70, 0, 1.3)):
        put(name, "Backdrop", (x, y, CANYON_Z), rot=rot, scale=s)
    # canyon walls: a near ring that towers over the arena and a far ring that fades into the haze
    for k in range(12):
        a = k * 30 + 8
        if a == 98:
            continue  # gap behind the temple so the far spires show through, as in the concept
        r = 82 + (k % 3) * 6
        put("Rock_Cliff_Wall", "Backdrop", _polar(a, r, CANYON_Z), rot=_facing_center(a),
            scale=1.35 + 0.25 * ((k * 7) % 3))
    for k in range(16):
        a = k * 22.5
        put("Rock_Cliff_Wall", "Backdrop", _polar(a, 165, CANYON_Z - 4), rot=_facing_center(a), scale=2.4)
    for x, y, s in ((-14, 125, 1.6), (18, 135, 1.9), (2, 150, 2.2)):
        put("Rock_Spire_A", "Backdrop", (x, y, CANYON_Z), rot=x * 5, scale=s)
    put("Ship_SkyBarge", "Backdrop", (34, 62, 30), rot=200)

    # --- spawns: (kind, (x, y, z), facing angle in degrees, CCW from +X)
    spawns = [
        ("Player", (0, -21.0, 0), 90),
        ("Enemy", (0, 5.0, 0), 270),
        ("Enemy", (-5.5, 0.5, 0), 300),
        ("Enemy", (5.5, 1.5, 0), 240),
    ]

    return P, spawns, _blockers()


def _blockers():
    """Invisible walls along the cliff edges so nobody walks off the plateau or the bridges.
    Returns (centre, size, rot_z) boxes in Blender arena space."""
    out = []
    exits = (0.0, 180.0, 270.0)
    outline = plateau_outline(28.0, 6.0, random.Random(7))
    n = len(outline)
    for i in range(n):
        (x0, y0), (x1, y1) = outline[i], outline[(i + 1) % n]
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        angle = math.degrees(math.atan2(my, mx)) % 360
        if any(min(abs(angle - e), 360 - abs(angle - e)) < 6.0 for e in exits):
            continue
        length = math.hypot(x1 - x0, y1 - y0) + 0.6
        inward = 1.0 - 0.8 / math.hypot(mx, my)
        out.append(((mx * inward, my * inward, 1.5), (length, 0.6, 3.0),
                    math.degrees(math.atan2(y1 - y0, x1 - x0))))
    # bridge sides and the far landings
    for heading in exits:
        rot = heading - 90.0
        for side in (-1, 1):
            a = math.radians(heading)
            nx, ny = -math.sin(a) * side, math.cos(a) * side
            cx, cy = math.cos(a) * 33.0 + nx * 2.45, math.sin(a) * 33.0 + ny * 2.45
            out.append(((cx, cy, 1.5), (0.4, 16.0, 3.0), rot))
        lx, ly = math.cos(math.radians(heading)) * 47.0, math.sin(math.radians(heading)) * 47.0
        for k in range(10):
            a = heading + 180 + 36 * k + 18
            if min(abs((a - heading - 180) % 360), 360 - abs((a - heading - 180) % 360)) < 20:
                continue
            ar = math.radians(a)
            out.append(((lx + math.cos(ar) * 5.8, ly + math.sin(ar) * 5.8, 1.5), (4.0, 0.5, 3.0), a + 90))
    return out


def to_unity_position(loc):
    """Blender arena space -> Unity arena space: temple toward +Z, east toward +X."""
    x, y, z = loc
    return [round(x, 4), round(z, 4), round(y, 4)]


def to_unity_yaw(rot_z):
    """Yaw of a placed kit piece. The FBX import maps piece space (x, y, z) to Unity (x, z, y),
    the same mirror as the arena frame, so a Blender turn is simply a Unity turn the other way."""
    return round((-rot_z) % 360.0, 3)


def direction_to_unity_yaw(angle):
    """Facing given as a compass angle in Blender space (CCW from +X) -> Unity yaw."""
    a = math.radians(angle)
    return round(math.degrees(math.atan2(math.cos(a), math.sin(a))) % 360.0, 3)
