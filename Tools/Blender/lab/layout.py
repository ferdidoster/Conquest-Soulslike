"""
The underground laboratory from Ferdinand's level sketch, composed from lab kit pieces.

Blender level space (metres): the hub is centred on the origin, north (+Y) is up on the sketch,
east (+X) leads to the laboratory / boss arena. The layout is exported to JSON in Unity's frame
(see export.to_unity_position) so the Unity builder can assemble the same level from prefabs.

    hub (r 9)  ->  corridor east (8 m wide)  ->  [gate] laboratory (r 25)  ->  [gate] elevator up
    inner ring corridor (9..18), outer ring corridor (18..25), rooms out to 46:
    technical (NE), sleeping (NW), bathroom (W), kitchen / living (SW), storage (SE)

Dotted lines on the sketch are Gate_Blast pieces with ids, closed until an event opens them.
"""

import math
import random

R_HUB = 9.0
R1 = 18.0
R2 = 25.0
R_OUT = 46.0
R_LAB = 25.0
LAB_C = (52.0, 0.0)
CORR = 4.0          # corridor half width
PANEL = 4.0
WALL_HALF = 0.25

DOOR = 6.0          # Door_Sliding width
GATE = 8.0          # Gate_Blast width

GATES = {
    "Gate_InnerRing": "Shortcut from the inner ring corridor to the outer ring (north-east).",
    "Gate_Technical": "Outer ring corridor into the technical maintenance room.",
    "Gate_Lab": "End of the main corridor into the laboratory / boss arena.",
    "Gate_Elevator": "Laboratory to the elevator up (exit after the boss).",
}


def polar(a, r, c=(0.0, 0.0), z=0.0):
    t = math.radians(a)
    return (c[0] + math.cos(t) * r, c[1] + math.sin(t) * r, z)


def half_angle(width, r):
    return math.degrees(math.asin(width / 2 / r))


class Layout:
    def __init__(self):
        self.P = []
        self.spawns = []
        self.blockers = []
        self.rng = random.Random(7)

    # ------------------------------------------------------------------ primitives
    def put(self, piece, group, loc, rot=0.0, sx=1.0, id=""):
        if len(loc) == 2:
            loc = (loc[0], loc[1], 0.0)
        self.P.append({"piece": piece, "group": group, "loc": tuple(loc), "rot": rot, "sx": sx, "id": id})

    def facing_in(self, piece, group, a, r, c=(0.0, 0.0), z=0.0, inset=0.0):
        """Wall-mounted piece on a ring wall's inner face, looking at the ring centre."""
        self.put(piece, group, polar(a, r - inset, c, z), rot=a - 90.0)

    def facing_out(self, piece, group, a, r, c=(0.0, 0.0), z=0.0, inset=0.0):
        self.put(piece, group, polar(a, r + inset, c, z), rot=a + 90.0)

    def panel_choice(self, i, variants, angle=None):
        if callable(variants):
            return variants(i, angle)
        if not variants:
            return "Wall_Panel_4m"
        return variants[i % len(variants)]

    def arc(self, r, a0, a1, group, c=(0.0, 0.0), variants=None, pillar="Wall_Pillar", backfill=False,
            pillar_every=1, end_pillars=True):
        """Wall along a circle from a0 to a1 (degrees CCW), as straight panels on chords with a
        pillar on every joint. Returns the panel centres as (angle, chord)."""
        span = a1 - a0
        n = max(1, round(math.radians(span) * r / PANEL))
        d = span / n
        chord = 2 * r * math.sin(math.radians(d / 2))
        rc = r * math.cos(math.radians(d / 2))
        out = []
        for i in range(n):
            am = a0 + (i + 0.5) * d
            self.put(self.panel_choice(i, variants, am), group, polar(am, rc, c), rot=am - 90.0, sx=chord / PANEL)
            if backfill:
                self.put("Rock_Mass_4m", "Backfill", polar(am, rc + WALL_HALF, c), rot=am - 90.0,
                         sx=chord / PANEL * 1.08)
            out.append((am, chord))
        for i in range(n + 1):
            if not end_pillars and i in (0, n):
                continue
            if i % pillar_every and i not in (0, n):
                continue
            a = a0 + i * d
            self.put(pillar, group, polar(a, r, c), rot=a - 90.0)
        return out

    def line(self, p0, p1, group, variants=None, pillar="Wall_Pillar", end_pillars=(True, True)):
        """Straight wall from p0 to p1 (2D), panels scaled to fit, pillars on the joints."""
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        length = math.hypot(dx, dy)
        n = max(1, round(length / PANEL))
        seg = length / n
        rot = math.degrees(math.atan2(dy, dx))
        for i in range(n):
            t = (i + 0.5) / n
            self.put(self.panel_choice(i, variants), group, (p0[0] + dx * t, p0[1] + dy * t, 0.0), rot=rot,
                     sx=seg / PANEL)
        for i in range(n + 1):
            if (i == 0 and not end_pillars[0]) or (i == n and not end_pillars[1]):
                continue
            t = i / n
            self.put(pillar, group, (p0[0] + dx * t, p0[1] + dy * t, 0.0), rot=rot)
        return rot

    def opening_on_line(self, p0, p1, center_t, width, group, piece=None, id="", variants=None):
        """Straight wall p0..p1 with an opening of `width` centred at fraction center_t; the
        opening holds `piece` (a door or gate) or stays open with end posts."""
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        length = math.hypot(dx, dy)
        ux, uy = dx / length, dy / length
        c = (p0[0] + dx * center_t, p0[1] + dy * center_t)
        a = (c[0] - ux * width / 2, c[1] - uy * width / 2)
        b = (c[0] + ux * width / 2, c[1] + uy * width / 2)
        rot = math.degrees(math.atan2(dy, dx))
        self.line(p0, a, group, variants)
        self.line(b, p1, group, variants)
        if piece:
            self.put(piece, group, (c[0], c[1], 0.0), rot=rot, id=id)
        return c, rot

    def against_line(self, piece, group, p0, p1, t, offset, z=0.0):
        """Piece with its back to the straight wall p0..p1, on the wall's right-hand side (the side
        its front -Y faces after turning by the wall direction)."""
        rot = math.degrees(math.atan2(p1[1] - p0[1], p1[0] - p0[0]))
        n = math.radians(rot - 90.0)
        x = p0[0] + (p1[0] - p0[0]) * t + math.cos(n) * offset
        y = p0[1] + (p1[1] - p0[1]) * t + math.sin(n) * offset
        self.put(piece, group, (x, y, z), rot=rot)

    def door_on_arc(self, piece, group, a, r, width, c=(0.0, 0.0), id=""):
        h = half_angle(width, r)
        self.put(piece, group, polar(a, r * math.cos(math.radians(h)), c), rot=a - 90.0, id=id)
        return h

    # ------------------------------------------------------------------ floors
    def zone(self, x, y):
        r = math.hypot(x, y)
        lab = math.hypot(x - LAB_C[0], y - LAB_C[1])
        if 48 - 2 <= x <= 56 + 2 and R_LAB - 3 <= y <= 46:
            return "elevator"
        # cells completely under the round hub / arena floors are left out; partly covered ones stay
        # (the discs sit 3 cm higher, so the tiles never show through)
        if lab + 2.83 < R_LAB + 0.6:
            return None
        if r + 2.83 < R_HUB + 0.4:
            return None
        if lab < R_LAB:
            return "ring" if r < R2 else ("technical" if y > 0 else "storage")
        if r < R_HUB:
            return "ring"
        if 8 <= x <= 28 and abs(y) < CORR:
            return "corridor"
        if r < R2:
            return "ring"
        if r > R_OUT + 1.8:
            return None
        if y > 0:
            return "sleeping" if x < -2 else "technical"
        if x < 0 and is_bathroom(x, y):
            return "bathroom"
        if x < 0:
            return "kitchen"
        return "storage"

    def floors(self):
        tiles = {
            "corridor": "Floor_Grate_4m", "ring": "Floor_Tile_4m", "elevator": "Floor_Grate_4m",
            "sleeping": "Floor_Tile_Clean_4m", "kitchen": "Floor_Tile_Clean_4m", "bathroom": "Floor_Tile_Bath_4m",
            "technical": "Floor_Tile_4m", "storage": "Floor_Tile_4m",
        }
        for i in range(-13, 22):
            for j in range(-13, 13):
                x, y = i * 4 + 2, j * 4 + 2
                z = self.zone(x, y)
                if z:
                    self.put(tiles[z], "Floor", (x, y, 0.0))
        self.put("Floor_Void_Slab", "Floor", (20, 0, 0.0))
        self.put("Floor_Hub_Disc", "Floor", (0, 0, 0.03))
        self.put("Floor_Arena_Disc", "Floor", (LAB_C[0], LAB_C[1], 0.03))


# bathroom: west of the diagonal wall A-B-C, south of the west wall
DIAG_A = (-40.0, 0.0)
DIAG_B = (-28.0, -24.0)


def diag_c():
    """Where the lower diagonal leg meets the outer wall."""
    bx, by = DIAG_B
    dx, dy = -8.0, -10.0
    n = math.hypot(dx, dy)
    dx, dy = dx / n, dy / n
    # |B + s d| = R_OUT
    bd = bx * dx + by * dy
    s = -bd + math.sqrt(bd * bd - (bx * bx + by * by - R_OUT * R_OUT))
    return (bx + dx * s, by + dy * s)


def _side(p, a, b):
    return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])


def is_bathroom(x, y):
    if y > 0:
        return False
    c = diag_c()
    if y >= DIAG_B[1]:
        return _side((x, y), DIAG_A, DIAG_B) < 0
    return _side((x, y), DIAG_B, c) < 0


def build():
    L = Layout()
    L.floors()
    lab_c = LAB_C

    # ----------------------------------------------------------------- hub
    hub_open = half_angle(2 * CORR, R_HUB)
    def hub_variant(i, am):
        # the sealed airlock the player arrives through, opposite the corridor, flanked by B-3 markings
        if abs(am - 180) < 13:
            return "Door_Airlock_Locked"
        if abs(am - 180) < 40:
            return "Wall_Panel_4m_B3"
        return ("Wall_Panel_4m", "Wall_Panel_4m_Vent", "Wall_Panel_4m", "Wall_Panel_4m_Pipes")[i % 4]

    hub_walls = L.arc(R_HUB, hub_open, 360 - hub_open, "Hub/Walls", variants=hub_variant)
    # the concept's hub: cold reactor light in the middle, warm emergency lamps on the walls
    for n, (am, chord) in enumerate(hub_walls[1::2]):
        if abs(am - 180) > 20:
            kind = "Lamp_Emergency" if n % 2 == 0 else "Lamp_Wall_Cold"
            L.facing_in(kind, "Hub/Lights", am, R_HUB * math.cos(math.radians(12.8)) - WALL_HALF + 0.02)
    L.put("Platform_Dais_6m", "Hub/Centre", (0, 0, 0.03))
    # railing round the upper tier of the dais, open toward the four consoles
    for q in range(4):
        for k in range(3):
            a = q * 90 + 22.5 + k * 22.5
            if k == 1:
                continue
            L.put("Railing_2m", "Hub/Centre", polar(a, 4.45, z=0.53), rot=a - 90, sx=0.86)
    L.put("Reactor_Column", "Hub/Centre", (0, 0, 0.53))
    for a in (45, 135, 225, 315):
        L.put("Console_Control", "Hub/Centre", polar(a, 7.2, z=0.03), rot=a + 90)
    L.put("Light_Floor_Strip_2m", "Hub/Centre", (8.5, 2.2, 0.03))
    L.put("Light_Floor_Strip_2m", "Hub/Centre", (8.5, -2.2, 0.03))

    # ----------------------------------------------------------------- main corridor
    x0 = R_HUB * math.cos(math.radians(hub_open))
    lab_gate_h = half_angle(GATE, R_LAB)
    x1 = lab_c[0] - R_LAB * math.cos(math.radians(lab_gate_h))
    corridor_n = (x0, CORR), (x1, CORR)
    L.opening_on_line((x0, CORR), (x1, CORR), (13.0 - x0) / (x1 - x0), 6.0, "Corridor/Walls",
                      variants=["Wall_Panel_4m_Pipes"])
    L.line((x0, -CORR), (x1, -CORR), "Corridor/Walls",
           variants=["Wall_Panel_4m", "Wall_Panel_4m_B3", "Wall_Panel_4m_Vent", "Wall_Panel_4m"])
    for x in (12.0, 20.5):
        L.put("Lamp_Wall_Cold", "Corridor/Lights", (x, -CORR + WALL_HALF, 0.0), rot=180)
    for x in (19.5, 25.0):
        L.put("Lamp_Wall_Cold", "Corridor/Lights", (x, CORR - WALL_HALF, 0.0), rot=0)
    for x in (14.0, 22.0):
        L.put("Cable_Hanging_4m", "Corridor/Props", (x, 0, 0.0), rot=90, sx=2.0)
    L.put("Lamp_Alarm_Red", "Corridor/Lights", (x1 - 1.2, CORR - WALL_HALF, 0.0), rot=0)
    L.put("Lamp_Alarm_Red", "Corridor/Lights", (x1 - 1.2, -CORR + WALL_HALF, 0.0), rot=180)
    L.put("Debris_Scrap", "Corridor/Props", (18.0, -1.5, 0.0), rot=30)

    # ----------------------------------------------------------------- inner ring wall (R1)
    c1 = half_angle(2 * CORR, R1)
    g1 = half_angle(GATE, R1)
    ring1 = []
    ring1 += L.arc(R1, c1, 45 - g1, "InnerRing/Walls")
    L.door_on_arc("Gate_Blast", "InnerRing/Gates", 45, R1, GATE, id="Gate_InnerRing")
    ring1 += L.arc(R1, 45 + g1, 330, "InnerRing/Walls",
                   variants=["Wall_Panel_4m", "Wall_Panel_4m_Pipes", "Wall_Panel_4m", "Wall_Panel_4m_Vent"])
    # 330..360-c1 stays open: the way from the inner ring into the outer ring, beside the corridor
    for am in (75, 120, 165, 210, 255, 300):
        L.facing_in("Lamp_Wall_Cold", "InnerRing/Lights", am, R1 - 0.3)
        L.facing_out("Lamp_Wall_Cold", "OuterRing/Lights", am + 22, R1 + 0.3)
    for am in (100, 190, 280):
        L.facing_in("Lamp_Emergency", "InnerRing/Lights", am, R1 - 0.3)

    # ----------------------------------------------------------------- outer ring wall (R2) with room doors
    c2 = half_angle(2 * CORR, R2)
    g2 = half_angle(GATE, R2)
    d2 = half_angle(DOOR, R2)
    doors = (138.0, 218.0, 310.0)
    L.arc(R2, c2, 55 - g2, "OuterRing/Walls")
    L.door_on_arc("Gate_Blast", "OuterRing/Gates", 55, R2, GATE, id="Gate_Technical")
    starts = [55 + g2]
    L.room_side = []
    for a in doors:
        L.room_side += L.arc(R2, starts[-1], a - d2, "OuterRing/Walls",
                             variants=["Wall_Panel_4m", "Wall_Panel_4m_Vent", "Wall_Panel_4m_Pipes", "Wall_Panel_4m"])
        L.door_on_arc("Door_Sliding", "OuterRing/Doors", a, R2, DOOR)
        starts.append(a + d2)
    L.room_side += L.arc(R2, starts[-1], 360 - c2, "OuterRing/Walls")
    # divider between the two halves of the outer ring (south), so the ring is not one loop
    L.line(polar(270, R1)[:2], polar(270, R2)[:2], "OuterRing/Walls")

    # ----------------------------------------------------------------- outer boundary + rooms
    lab_meet = math.degrees(math.atan2(*reversed(_outer_lab_meet())))
    L.outer_wall = L.arc(R_OUT, lab_meet, 360 - lab_meet, "Rooms/OuterWall", backfill=True,
          variants=["Wall_Panel_4m", "Wall_Panel_4m", "Wall_Panel_4m_Pipes", "Wall_Panel_4m_Vent"])
    for a in range(int(lab_meet) + 8, int(360 - lab_meet), 16):
        kind = "Lamp_Emergency" if a % 3 == 0 else "Lamp_Wall_Cold"
        L.facing_in(kind, "Rooms/Lights", a + 4, R_OUT - 0.3)

    # north wall between technical and sleeping, with a door
    n0, n1 = polar(94, R2)[:2], polar(94, R_OUT)[:2]
    L.opening_on_line(n0, n1, (37.0 - R2) / (R_OUT - R2), DOOR, "Rooms/Walls", piece="Door_Sliding")
    # west wall between sleeping and bathroom / kitchen (solid)
    L.line((-R2, 0), (-R_OUT, 0), "Rooms/Walls", variants=["Wall_Panel_4m", "Wall_Panel_4m_Vent"])
    # diagonal bathroom wall with its door, and the lower leg to the outer wall
    L.opening_on_line(DIAG_A, DIAG_B, 0.5, DOOR, "Rooms/Walls", piece="Door_Sliding")
    L.line(DIAG_B, diag_c(), "Rooms/Walls")
    # south wall between kitchen and storage, with a door
    s0, s1 = polar(270, R2)[:2], polar(270, R_OUT)[:2]
    L.opening_on_line(s0, s1, (37.5 - R2) / (R_OUT - R2), DOOR, "Rooms/Walls", piece="Door_Sliding")

    furnish_technical(L)
    furnish_sleeping(L)
    furnish_bathroom(L)
    furnish_kitchen(L)
    furnish_storage(L)
    dress_room_walls(L, lab_meet)
    furnish_rings(L)

    # ----------------------------------------------------------------- laboratory / boss arena
    lab_gate_up = half_angle(GATE, R_LAB)
    lab_meet_local = math.degrees(math.atan2(_outer_lab_meet()[1] - lab_c[1], _outer_lab_meet()[0] - lab_c[0]))
    lab_variants = ["Wall_Panel_4m", "Wall_Panel_4m_Pipes", "Wall_Panel_4m", "Wall_Panel_4m_Vent"]
    L.arc(R_LAB, 90 + lab_gate_up, lab_meet_local, "Lab/Walls", c=lab_c, backfill=True, variants=lab_variants)
    L.arc(R_LAB, lab_meet_local, 180 - lab_gate_h, "Lab/Walls", c=lab_c, variants=lab_variants)
    L.door_on_arc("Gate_Blast", "Lab/Gates", 180, R_LAB, GATE, c=lab_c, id="Gate_Lab")
    L.arc(R_LAB, 180 + lab_gate_h, 360 - lab_meet_local, "Lab/Walls", c=lab_c, variants=lab_variants)
    L.arc(R_LAB, -lab_meet_local, 90 - lab_gate_up, "Lab/Walls", c=lab_c, backfill=True, variants=lab_variants)
    L.door_on_arc("Gate_Blast", "Lab/Gates", 90, R_LAB, GATE, c=lab_c, id="Gate_Elevator")
    furnish_lab(L)

    # ----------------------------------------------------------------- elevator shaft
    ex0, ex1 = lab_c[0] - 4.0, lab_c[0] + 4.0
    y0 = lab_c[1] + R_LAB * math.cos(math.radians(lab_gate_up))
    y1 = 44.0
    for x in (ex0, ex1):
        L.line((x, y0), (x, y1), "Elevator/Walls")
    L.put("Rock_Mass_4m", "Backfill", (ex0 - WALL_HALF, (y0 + y1) / 2, 0.0), rot=90.0, sx=(y1 - y0) / PANEL * 1.05)
    L.put("Rock_Mass_4m", "Backfill", (ex1 + WALL_HALF, (y0 + y1) / 2, 0.0), rot=-90.0, sx=(y1 - y0) / PANEL * 1.05)
    L.line((ex0, y1), (ex1, y1), "Elevator/Walls")
    L.put("Rock_Mass_4m", "Backfill", (lab_c[0], y1 + WALL_HALF, 0.0), rot=0.0, sx=2.2)
    L.put("Elevator_Platform", "Elevator", (lab_c[0], y1 - 4.2, 0.0))
    L.put("Lamp_Alarm_Red", "Elevator", (ex0 + WALL_HALF, y0 + 2.5, 0.0), rot=90)
    L.put("Lamp_Wall_Cold", "Elevator", (ex1 - WALL_HALF, y0 + 6.0, 0.0), rot=-90)
    L.put("Lamp_Wall_Cold", "Elevator", (ex0 + WALL_HALF, y0 + 11.0, 0.0), rot=90)

    scatter_grime(L)
    fill_lights(L)

    # ----------------------------------------------------------------- spawns (first = gunner, last = boss)
    S = L.spawns
    S.append(("Enemy", (21.0, 21.5, 0.0), 230))          # Warden behind the outer-ring gate approach
    S.append(("Enemy", polar(250, 13.5), 70))             # Sentinel in the inner ring, south
    S.append(("Enemy", polar(300, 21.5), 120))            # Sentinel in the outer ring, south-east
    S.append(("Enemy", (-17.0, -33.0, 0.0), 60))          # Sentinel in the kitchen
    S.append(("Enemy", (-24.0, 28.0, 0.0), 300))          # Sentinel in the sleeping area
    S.append(("Enemy", (lab_c[0] - 7.0, 0.0, 0.0), 180))  # the Bulwark, guardian of the laboratory
    L.spawns.insert(0, ("Player", (-7.2, 0.0, 0.05), 0))
    return L.P, L.spawns, L.blockers, [{"id": k, "notes": v} for k, v in GATES.items()]


def _outer_lab_meet():
    """Upper point where the outer wall circle meets the laboratory circle."""
    d = LAB_C[0]
    x = (R_OUT ** 2 - R_LAB ** 2 + d * d) / (2 * d)
    y = math.sqrt(R_OUT ** 2 - x * x)
    return (x, y)


# ---------------------------------------------------------------------------------- furnishing
def furnish_rings(L):
    rng = L.rng
    # inner ring corridor: work lights, crates and wrecks along the ring
    for a in (60, 150, 240):
        L.put("Light_Post", "InnerRing/Props", polar(a, 16.6), rot=a)
    for a, piece in ((95, "Crate_Stack"), (135, "Crate_Storage"), (178, "Broken_Robot"), (205, "Barrel"),
                     (232, "Crate_Large"), (290, "Debris_Scrap"), (318, "Crate_Storage")):
        L.put(piece, "InnerRing/Props", polar(a, 15.8), rot=a - 90 + rng.uniform(-15, 15))
    for a in (110, 170, 260):
        L.facing_in("Terminal_Wall", "InnerRing/Props", a, R1 - 0.3)
    # outer ring corridor
    for a, piece in ((80, "Barrel"), (84, "Barrel"), (160, "Crate_Storage"), (196, "Debris_Scrap"),
                     (240, "Crate_Stack"), (285, "Broken_Robot"), (335, "Crate_Large")):
        L.put(piece, "OuterRing/Props", polar(a, 22.6), rot=a - 90 + rng.uniform(-20, 20))
    for a in (100, 175, 250, 325):
        L.facing_in("Pipe_Wall_Run_4m", "OuterRing/Props", a, R2 - 0.3)
    for a in (125, 200, 290):
        L.facing_in("Lamp_Emergency", "OuterRing/Lights", a, R2 - 0.3)


def furnish_technical(L):
    L.put("Machine_Pump", "Technical", polar(70, 35.0))
    L.put("Machine_Pump", "Technical", polar(46, 37.5))
    for a, r in ((84, 30.0), (84, 40.5)):
        L.put("Generator_Unit", "Technical", polar(a, r), rot=a - 90)
    n0, n1 = polar(94, R2)[:2], polar(94, R_OUT)[:2]
    L.against_line("Workbench", "Technical", n0, n1, 0.22, 0.7)
    L.against_line("Server_Rack", "Technical", n0, n1, 0.78, 0.78)
    L.against_line("Server_Rack", "Technical", n0, n1, 0.83, 0.78)
    for a in (58, 64):
        L.put("Pipe_Large_4m", "Technical", polar(a, 31.0), rot=a + 90)
    L.put("Pipe_U_Bend", "Technical", polar(40, 42.6), rot=40 - 90)
    L.put("Pipe_Elbow", "Technical", polar(76, 43.5), rot=76 - 90)
    for a in (36, 52, 88):
        L.facing_in("Pipe_Vertical_5m", "Technical", a, R_OUT - 0.25)
    L.put("Console_Control", "Technical", polar(60, 41.8), rot=60 - 90)
    L.put("Crate_Large", "Technical", polar(34, 29.0), rot=10)
    L.put("Broken_Robot", "Technical", polar(55, 33.5), rot=200)
    for a in (40, 66, 80):
        L.facing_out("Lamp_Emergency", "Technical/Lights", a, R2 + 0.3)


def furnish_sleeping(L):
    for a in range(103, 178, 9):
        L.put("Bed_Bunk", "Sleeping", polar(a, R_OUT - 1.5), rot=a - 90)
    for a in range(110, 176, 11):
        L.put("Bed_Bunk", "Sleeping", polar(a, 39.6), rot=a - 90)
        L.put("Bed_Bunk", "Sleeping", polar(a, 38.5), rot=a + 90)
        if abs(a - 138) > 9:
            L.put("Bed_Bunk", "Sleeping", polar(a, 31.6), rot=a - 90)
            L.put("Bed_Bunk", "Sleeping", polar(a, 30.5), rot=a + 90)
    for a in (110, 124, 152, 166):
        L.put("Locker_Row", "Sleeping", polar(a, R2 + 0.6), rot=a + 90)
    for t in (0.2, 0.32, 0.7):
        L.against_line("Locker_Row", "Sleeping", (-R2, 0), (-R_OUT, 0), t, 0.55)
    n0, n1 = polar(94, R2)[:2], polar(94, R_OUT)[:2]
    L.against_line("Locker_Row", "Sleeping", n1, n0, 0.25, 0.55)
    L.against_line("Locker_Row", "Sleeping", n1, n0, 0.75, 0.55)
    L.put("Table_Dining", "Sleeping", polar(140, 35.0), rot=140 - 90, sx=0.6)
    for side in (-1, 1):
        L.put("Chair", "Sleeping", polar(140 + side * 2.0, 35.0), rot=140 + side * 90 - 90)
    L.put("Terminal_Offline", "Sleeping", polar(116, 35.0), rot=116 - 90)
    L.put("Crate_Storage", "Sleeping", polar(160, 35.0), rot=30)
    L.put("Debris_Scrap", "Sleeping", polar(128, 35.0), rot=0)
    for a in (120, 156):
        L.put("Light_Post", "Sleeping", polar(a, 35.0))


def furnish_bathroom(L):
    for k in range(3):
        L.against_line("Shower_Stall", "Bathroom", (-R_OUT, 0), (-R2, 0), (1.2 + k * 1.45) / (R_OUT - R2), 1.0)
    for t in (0.2, 0.8):
        L.against_line("Sink_Row", "Bathroom", DIAG_A, DIAG_B, t, 0.55)
    for t in (0.25, 0.55):
        L.against_line("Locker_Row", "Bathroom", DIAG_B, diag_c(), t, 0.55)
    for k in range(3):
        L.put("Toilet_Stall", "Bathroom", polar(196 + k * 2.6, R_OUT - 1.1), rot=196 + k * 2.6 - 90)
    L.put("Lamp_Wall_Cold", "Bathroom", (-42.0, -WALL_HALF, 0.0), rot=180)
    L.put("Debris_Scrap", "Bathroom", (-40.0, -14.0, 0.0), rot=40)


def furnish_kitchen(L):
    for k, (x, y) in enumerate(((-12.0, -32.5), (-12.0, -38.0))):
        L.put("Table_Dining", "Kitchen", (x, y, 0.0), rot=90)
        for side in (-1, 1):
            for dy in (-0.9, 0.0, 0.9):
                L.put("Chair", "Kitchen", (x + side * 1.0, y + dy, 0.0), rot=90 * side)
    s0, s1 = polar(270, R2)[:2], polar(270, R_OUT)[:2]
    L.against_line("Kitchen_Counter", "Kitchen", s0, s1, 0.12, 0.6)
    L.against_line("Kitchen_Counter", "Kitchen", s0, s1, 0.27, 0.6)
    L.against_line("Fridge", "Kitchen", s0, s1, 0.88, 0.65)
    L.put("Sofa", "Kitchen", polar(250, R_OUT - 1.3), rot=250 - 90)
    L.put("Sofa", "Kitchen", polar(238, R_OUT - 1.3), rot=238 - 90)
    L.put("Table_Dining", "Kitchen", polar(244, R_OUT - 4.0), rot=244 - 90, sx=0.5)
    L.put("Terminal_Offline", "Kitchen", (-25.5, -28.0, 0.0), rot=45)
    # warm, flickering emergency light in the living area (concept: orange accents)
    for t in (0.2, 0.55, 0.85):
        L.against_line("Lamp_Emergency", "Kitchen", s0, s1, t, 0.25)
    for a in (232, 248, 262):
        L.facing_out("Lamp_Emergency", "Kitchen", a, R2 + 0.3)
    L.put("Light_Post", "Kitchen", (-20.0, -36.0, 0.0))
    # the painting's living area is packed: a work island, a second dining spot, crates by the wall
    for k, side in enumerate((-1, 1)):
        n = math.radians(135 + 90)
        L.put("Lab_Table", "Kitchen", (-21.5 + side * 0.44 * math.cos(n), -27.5 + side * 0.44 * math.sin(n), 0.0),
              rot=135 if side > 0 else 315)
    L.put("Table_Dining", "Kitchen", polar(258, 33.5), rot=258, sx=0.7)
    for side in (-1, 1):
        L.put("Chair", "Kitchen", polar(258 + side * 1.9, 33.5), rot=258 + (90 if side > 0 else -90) - 90)
    for a, r, piece in ((264, 29.0, "Crate_Storage"), (265.5, 30.5, "Barrel"), (229, 28.5, "Crate_Stack"),
                        (246, 28.0, "Debris_Scrap")):
        L.put(piece, "Kitchen", polar(a, r), rot=a + 20)


def furnish_storage(L):
    rng = L.rng
    for a in (282, 294, 306):
        for r in (30.0, 34.0, 38.0):
            L.put("Shelf_Storage", "Storage", polar(a, r), rot=a)
    for a, r, piece in ((318, 30.0, "Crate_Large"), (322, 35.5, "Crate_Stack"), (312, 41.5, "Crate_Large"),
                        (300, 42.5, "Crate_Stack"), (287, 43.0, "Crate_Storage"), (276, 33.0, "Barrel"),
                        (276, 34.0, "Barrel"), (278, 41.0, "Server_Rack"), (326, 28.5, "Crate_Storage")):
        L.put(piece, "Storage", polar(a, r), rot=rng.uniform(0, 360) if piece != "Server_Rack" else 0)
    L.put("Light_Post", "Storage", polar(300, 28.0))
    for a in (285, 315):
        L.facing_out("Lamp_Emergency", "Storage", a, R2 + 0.3)


def furnish_lab(L):
    c = LAB_C
    L.put("Containment_Tube_Large", "Lab/Centre", (c[0], c[1], 0.03))
    for k in range(6):
        a = k * 60
        L.put("Machine_Column", "Lab/Centre", polar(a, 10.5, c, 0.03), rot=a + 90)
    for k in range(6):
        a = 30 + k * 60
        piece = "Containment_Tube_Broken" if k in (1, 4) else "Containment_Tube"
        L.put(piece, "Lab/Tubes", polar(a, 15.0, c, 0.03), rot=a + 90)
    for a in (125, 140, 220, 235):
        L.put("Console_Control", "Lab/Props", polar(a, 21.0, c, 0.03), rot=a + 90)
    for a in (20, 340):
        L.put("Lab_Table", "Lab/Props", polar(a, 21.5, c, 0.03), rot=a + 90)
    for a in (60, 300):
        L.put("Bed_Medical", "Lab/Props", polar(a, 21.5, c, 0.03), rot=a)
    for a in (8, 352, 42, 318):
        L.facing_in("Server_Rack", "Lab/Props", a, R_LAB - 0.9, c, 0.03)
    for a in (150, 210):
        L.facing_in("Med_Cabinet", "Lab/Props", a, R_LAB - 0.6, c, 0.03)
    for a in (75, 105, 250, 290):
        L.put("Debris_Scrap", "Lab/Props", polar(a, 18.0, c, 0.03), rot=a)
    # cold lamps all around, red alarms by the gates
    for a in range(0, 360, 30):
        if a in (90, 180):
            continue
        L.facing_in("Lamp_Wall_Cold", "Lab/Lights", a + 15, R_LAB - 0.35, c)
    for a in (180, 90):
        L.facing_in("Lamp_Alarm_Red", "Lab/Lights", a + 14, R_LAB - 0.35, c)
        L.facing_in("Lamp_Alarm_Red", "Lab/Lights", a - 14, R_LAB - 0.35, c)
    # catwalks along the wall with stairs, leaving the gates clear
    for a0, a1, stairs in ((24, 70, 48), (110, 156, 132), (204, 250, 226), (290, 336, 312)):
        r = R_LAB - 1.6
        n = max(1, round(math.radians(a1 - a0) * r / 4.0))
        d = (a1 - a0) / n
        chord = 2 * r * math.sin(math.radians(d / 2))
        for i in range(n):
            am = a0 + (i + 0.5) * d
            piece = "Catwalk_Landing" if abs(am - stairs) < d / 2 else "Catwalk_4m_Wall"
            sx = chord / (2.4 if piece == "Catwalk_Landing" else 4.0)
            L.put(piece, "Lab/Catwalks", polar(am, r * math.cos(math.radians(d / 2)), c, 0.03), rot=am - 90, sx=sx)
            if piece == "Catwalk_Landing":
                L.put("Stairs_Wide", "Lab/Catwalks", polar(am, r - 1.2 - 2.7, c, 0.03), rot=am - 90)


# wall dressing that fills the rooms' walls like the concept paintings: per room, the angle span on
# the outer wall (R_OUT) and on the room side of the ring wall (R2), the pieces to cycle through,
# and angles to keep clear (doors, radial walls, things already standing by the wall)
ROOM_WALLS = (
    ("Technical", "outer", (0, 93), ("Server_Rack", "Pipe_Wall_Run_4m", "Generator_Unit", "Vent_Wall",
                                     "Shelf_Storage", "Pipe_Wall_Run_4m"), (36, 52, 76, 84, 88)),
    ("Technical", "ring", (66, 92), ("Pipe_Wall_Run_4m", "Terminal_Wall", "Shelf_Storage", "Crate_Stack"), ()),
    ("Kitchen", "outer", (222, 268), ("Shelf_Storage", "Locker_Single", "Shelf_Storage", "Vent_Wall"),
     (238, 244, 250)),
    ("Kitchen", "ring", (226, 268), ("Kitchen_Counter", "Fridge", "Kitchen_Counter", "Shelf_Storage"), ()),
    ("Storage", "outer", (272, 360), ("Shelf_Storage", "Crate_Stack", "Shelf_Storage", "Pipe_Wall_Run_4m"), ()),
    ("Storage", "ring", (272, 328), ("Shelf_Storage", "Crate_Stack", "Server_Rack"), (310,)),
)
# depth from the wall face to the piece's pivot (wall-mounted pieces have their back at 0)
WALL_INSET = {"Server_Rack": 0.55, "Generator_Unit": 0.8, "Shelf_Storage": 0.4, "Crate_Stack": 0.75,
              "Locker_Single": 0.3, "Kitchen_Counter": 0.36, "Fridge": 0.4}
TALL_ON_WALL = {"Pipe_Wall_Run_4m", "Vent_Wall"}   # would run into the wall lamps


def dress_room_walls(L, lab_meet):
    lamps_out = [a + 4 for a in range(int(lab_meet) + 8, int(360 - lab_meet), 16)]
    lamps_ring = (40, 66, 80, 232, 248, 262, 285, 315)
    for group, wall, (a0, a1), kinds, keep_clear in ROOM_WALLS:
        panels = L.outer_wall if wall == "outer" else L.room_side
        r = R_OUT if wall == "outer" else R2
        lamps = lamps_out if wall == "outer" else lamps_ring
        k = 0
        for am, chord in panels:
            am_n = am % 360
            if not (a0 <= am_n <= a1) or any(abs(am_n - c) < 3.0 for c in keep_clear):
                continue
            if any(abs(am_n - rad) < 2.5 for rad in (94, 180, 270)):
                continue
            piece = kinds[k % len(kinds)]
            k += 1
            if piece in TALL_ON_WALL and any(abs(am_n - la) < 2.6 for la in lamps):
                piece = "Shelf_Storage"
            rm = math.sqrt(r * r - (chord / 2) ** 2)
            inset = WALL_INSET.get(piece, 0.0) + 0.02
            if wall == "outer":
                L.facing_in(piece, group, am, rm - WALL_HALF - inset)
            else:
                L.facing_out(piece, group, am, rm + WALL_HALF + inset)
    # the technical room's overhead crane and two more machines, after the concept painting
    L.put("Crane_Gantry", "Technical", polar(62, (R2 + R_OUT) / 2), rot=62)
    L.put("Machine_Column", "Technical", polar(80, 36.0), rot=80 + 90)
    L.put("Machine_Column", "Technical", polar(40, 31.5), rot=40 + 90)
    fill_room_floors(L)


# props for the open middle of the big rooms (the size stays; the floor fills up like the
# paintings), each as (group, angle, radius, piece, extra turn), leaving the enemy posts and
# the walking lines between doors clear
ROOM_FLOOR = (
    ("Technical", 76, 28.0, "Generator_Unit", 0),
    ("Technical", 88, 33.0, "Crate_Stack", 15), ("Technical", 84, 37.0, "Crate_Storage", -20),
    ("Technical", 58, 37.0, "Server_Rack", 0), ("Technical", 59.6, 37.0, "Server_Rack", 0),
    ("Technical", 52, 38.0, "Pipe_Elbow", 0), ("Technical", 36, 37.0, "Barrel", 0),
    ("Technical", 37.2, 37.4, "Barrel", 40), ("Technical", 36.4, 38.4, "Barrel", 80),
    ("Technical", 44, 33.5, "Debris_Scrap", 0),
    ("Kitchen", 236, 32.5, "Sofa", 0), ("Kitchen", 236, 30.6, "Crate_Storage", 0),
    ("Kitchen", 222, 33.5, "Crate_Stack", 30), ("Kitchen", 223.5, 35.5, "Barrel", 0),
    ("Kitchen", 254, 29.0, "Debris_Scrap", 0),
    ("Sleeping", 106, 35.0, "Crate_Storage", 10), ("Sleeping", 147, 35.2, "Crate_Storage", -15),
    ("Sleeping", 170, 35.0, "Barrel", 0), ("Sleeping", 171.2, 35.4, "Barrel", 0),
    ("Sleeping", 132, 35.0, "Locker_Single", 0), ("Sleeping", 164, 35.0, "Debris_Scrap", 0),
    ("Storage", 290, 27.5, "Crate_Storage", 5), ("Storage", 292, 27.6, "Crate_Storage", -8),
)


def fill_room_floors(L):
    for group, a, r, piece, turn in ROOM_FLOOR:
        L.put(piece, group, polar(a, r), rot=a - 90 + turn)
    # cables slung between the technical room's machines
    L.put("Cable_Hanging_4m", "Technical", polar(58, 36.2), rot=58 - 90 + 10, sx=1.6)


# grime decals and loose floor cables per section: (decals, cable runs)
GRIME = {"Hub": (5, 2), "Corridor": (3, 2), "InnerRing": (10, 4), "OuterRing": (14, 5), "Technical": (9, 6),
         "Sleeping": (7, 1), "Bathroom": (5, 0), "Kitchen": (7, 1), "Storage": (8, 3), "Lab": (10, 6),
         "Elevator": (2, 1)}


def scatter_grime(L):
    """Dirt, puddles and cables over the floors, so no room reads as a clean tile grid."""
    from . import sections
    rng = random.Random(23)
    for name, (decals, cables) in GRIME.items():
        for piece, count in (("Floor_Grime", decals), ("Cable_Floor_Run", cables)):
            placed, tries = 0, 0
            while placed < count and tries < 4000:
                tries += 1
                x, y = rng.uniform(-50, 82), rng.uniform(-50, 50)
                # keep a margin so the patch stays inside the section
                if any(sections.section_of(x + dx, y + dy) != name
                       for dx, dy in ((0, 0), (1.8, 0), (-1.8, 0), (0, 1.8), (0, -1.8))):
                    continue
                group = "Hub/Centre" if name == "Hub" else name + "/Dressing"
                L.put(piece, group, (x, y, 0.02), rot=rng.uniform(0, 360))
                placed += 1


# room fill lights after the concept's moods: kind and spacing per section
FILL = {"Hub": ("Light_Fill_Cold", 9.0), "Corridor": ("Light_Fill_Cold", 8.0),
        "InnerRing": ("Light_Fill_Cold", 11.0), "OuterRing": ("Light_Fill_Cold", 12.0),
        "Technical": ("Light_Fill_Warm", 8.5), "Sleeping": ("Light_Fill_Cold", 9.0),
        "Bathroom": ("Light_Fill_Cold", 7.0), "Kitchen": ("Light_Fill_Warm", 9.0),
        "Storage": ("Light_Fill_Warm", 9.0), "Lab": ("Light_Fill_Cold", 11.0), "Elevator": ("Light_Fill_Cold", 8.0)}


def fill_lights(L):
    from . import sections
    for name, (piece, step) in FILL.items():
        y = -50.0 + step / 2
        while y < 50.0:
            x = -50.0 + step / 2
            while x < 82.0:
                if sections.section_of(x, y) == name:
                    group = "Hub/Lights" if name == "Hub" else name + "/Lights"
                    L.put(piece, group, (x, y, 0.0))
                x += step
            y += step
