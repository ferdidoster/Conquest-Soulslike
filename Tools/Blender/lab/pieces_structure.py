"""Building blocks: floors, walls, pillars, doors, gates, stairs, catwalks, railings, ceiling."""

import math

from . import palette as P
from .kit import piece

WALL_T = 0.5     # wall thickness
WALL_H = 5.0     # wall height
DOOR_W = 3.0     # sliding door opening
DOOR_H = 3.4
GATE_W = 4.0     # blast gate opening
GATE_H = 4.2


# ---------------------------------------------------------------------------------- helpers
def hazard_stripes(b, x0, x1, z0, z1, y, depth=0.03, stripe=0.32, face=-1):
    """Diagonal-looking yellow/black stripes as alternating slanted boxes on a vertical face."""
    w = x1 - x0
    n = max(1, int(w / stripe))
    step = w / n
    b.box((w, depth, z1 - z0), ((x0 + x1) / 2, y, z0), P.METAL_DARK)
    for i in range(0, n, 2):
        cx = x0 + (i + 0.5) * step
        b.box((step * 0.9, depth + 0.01, z1 - z0), (cx, y + face * 0.005, z0), P.HAZARD)


def floor_hazard(b, x0, x1, y0, y1, stripe=0.36):
    """Yellow and dark stripes painted on the floor, running along X."""
    w = x1 - x0
    n = max(1, int(w / stripe))
    step = w / n
    b.box((w, y1 - y0, 0.012), ((x0 + x1) / 2, (y0 + y1) / 2, 0.0), P.METAL_DARK)
    for i in range(0, n, 2):
        b.box((step * 0.92, y1 - y0, 0.02), (x0 + (i + 0.5) * step, (y0 + y1) / 2, 0.0), P.HAZARD,
              rot_z=0.0)


def bolts(b, x, y, z0, z1, step=0.42, size=0.07, mat=P.METAL):
    """A column of bolt heads on a face at x, between z0 and z1."""
    n = max(1, int((z1 - z0) / step))
    for k in range(n + 1):
        b.box((size, 0.04, size), (x, y, z0 + (z1 - z0) * k / n - size / 2), mat)


def wall_face(b, width, side, height=WALL_H, vent=False, pipes=False, marking=None):
    """Heavy riveted plate wall from the concept sheet. One face of a wall running along X;
    side=-1 front (-Y), +1 back."""
    y = side * (WALL_T / 2)
    # grimy kick plate, then three rows of big plates between bolted ribs
    b.box((width, 0.14, 0.55), (0, y + side * 0.05, 0), P.METAL_DARK)
    b.box((width, 0.06, 0.05), (0, y + side * 0.12, 0.55), P.RUST)
    cols = max(1, int(round(width / 2)))
    pw = width / cols
    rows = ((0.6, 1.55, P.WALL_DARK), (1.65, 2.3, P.WALL), (3.95, 0.5, P.WALL_DARK))
    for i in range(cols):
        cx = -width / 2 + (i + 0.5) * pw
        for z0, h, mat in rows:
            b.box((pw - 0.2, 0.07, h), (cx, y + side * 0.035, z0), mat, bevel=0.015)
        # plate seams and rivet rows along them
        for z in (1.6, 3.9):
            b.box((pw - 0.2, 0.09, 0.05), (cx, y + side * 0.045, z), P.METAL_DARK)
            for k in range(5):
                b.box((0.06, 0.05, 0.06), (cx - pw * 0.38 + k * pw * 0.19, y + side * 0.08, z + 0.1), P.METAL)
        if vent and i == 0:
            b.box((pw * 0.62, 0.12, 0.95), (cx, y + side * 0.06, 2.3), P.METAL_DARK)
            for k in range(6):
                b.box((pw * 0.56, 0.06, 0.05), (cx, y + side * 0.13, 2.38 + k * 0.14), P.METAL, rot_x=-20 * side)
        elif marking is None and not pipes and (i % 2 == 1):
            # access hatch with a small amber status light
            b.box((0.7, 0.06, 0.9), (cx, y + side * 0.07, 2.1), P.WALL_LIGHT, bevel=0.01)
            b.box((0.5, 0.04, 0.05), (cx, y + side * 0.1, 2.75), P.METAL_DARK)
            b.box((0.1, 0.05, 0.06), (cx + 0.22, y + side * 0.1, 2.05), P.LAMP_WARM)
    # ribs: heavy vertical I-beams with bolts, at every plate joint
    for i in range(cols + 1):
        x = -width / 2 + i * pw
        b.box((0.26, 0.18, height - 0.55), (x, y + side * 0.07, 0.55), P.METAL_DARK)
        b.box((0.12, 0.24, height - 0.55), (x, y + side * 0.09, 0.55), P.METAL)
        bolts(b, x, y + side * 0.22, 0.8, height - 0.6, mat=P.METAL_DARK)
    # conduit along the top
    b.cylinder(0.07, width, (-width / 2, y + side * 0.18, 4.55), P.METAL_DARK, segments=6, rot_y=90, base=False)
    if pipes:
        for z, r, mat in ((3.05, 0.16, P.METAL), (3.42, 0.1, P.RUST), (2.8, 0.07, P.METAL_DARK)):
            b.cylinder(r, width, (-width / 2, y + side * (0.3 + r), z), mat, segments=8, rot_y=90, base=False)
        for x in (-width / 2 + 0.6, width / 2 - 0.6):
            b.box((0.14, 0.55, 0.9), (x, y + side * 0.3, 2.6), P.METAL_DARK)
            b.box((0.3, 0.1, 0.3), (x, y + side * 0.42, 3.3), P.METAL_DARK)
    if marking:
        draw_marking(b, marking, y + side * 0.075, side, height=1.9, z0=1.75)


# blocky 5x7 glyphs for wall markings ("B-3", sector letters)
GLYPHS = {
    "B": ["1110", "1001", "1001", "1110", "1001", "1001", "1110"],
    "3": ["1110", "0001", "0001", "0110", "0001", "0001", "1110"],
    "-": ["0000", "0000", "0000", "1111", "0000", "0000", "0000"],
    "L": ["1000", "1000", "1000", "1000", "1000", "1000", "1111"],
    "A": ["0110", "1001", "1001", "1111", "1001", "1001", "1001"],
    "1": ["0010", "0110", "0010", "0010", "0010", "0010", "0111"],
    "2": ["1110", "0001", "0001", "0110", "1000", "1000", "1111"],
    "4": ["1001", "1001", "1001", "1111", "0001", "0001", "0001"],
    "5": ["1111", "1000", "1000", "1110", "0001", "0001", "1110"],
}


def draw_marking(b, text, y, side, height=1.5, z0=1.2, mat=P.MARKING):
    cell = height / 7
    gw = cell * 4
    total = len(text) * gw + (len(text) - 1) * cell
    x = -total / 2
    for ch in text:
        rows = GLYPHS[ch]
        for r, row in enumerate(rows):
            for c, bit in enumerate(row):
                if bit == "1":
                    # mirror on the back face so the text reads correctly from both sides
                    cx = x + (c + 0.5) * cell
                    if side > 0:
                        cx = -cx
                    b.box((cell * 1.02, 0.03, cell * 1.02), (cx, y, z0 + (6 - r) * cell), mat)
        x += gw + cell


def wall(b, width, vent=False, pipes=False, marking=None):
    b.box((width, WALL_T - 0.16, WALL_H), (0, 0, 0), P.WALL_DARK)
    wall_face(b, width, -1, vent=vent, pipes=pipes, marking=marking)
    wall_face(b, width, +1, vent=vent and width > 2, pipes=False, marking=marking)
    b.box((width, WALL_T + 0.12, 0.25), (0, 0, WALL_H - 0.25), P.METAL, bevel=0.02)
    b.box((width, WALL_T - 0.1, 0.08), (0, 0, WALL_H), P.METAL_DARK)


# ---------------------------------------------------------------------------------- floors
@piece("Floor_Tile_4m", "Floors", collider="box", box=((0, 0, -0.1), (4, 4, 0.2)),
       notes="4 x 4 m steel deck plate, top at 0. Tile it on a 4 m grid.")
def floor_tile(b):
    b.box((4, 4, 0.18), (0, 0, -0.2), P.FLOOR_DARK)
    shades = (P.FLOOR, P.FLOOR_LIGHT, P.FLOOR_LIGHT, P.FLOOR)
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        b.box((1.94, 1.94, 0.04), (sx * 1.0, sy * 1.0, -0.04), shades[i], bevel=0.015)
        for bx, by in ((-0.82, -0.82), (0.82, -0.82), (-0.82, 0.82), (0.82, 0.82)):
            b.box((0.08, 0.08, 0.02), (sx * 1.0 + bx, sy * 1.0 + by, 0.0), P.METAL_DARK)


@piece("Floor_Tile_Clean_4m", "Floors", collider="box", box=((0, 0, -0.1), (4, 4, 0.2)),
       notes="4 x 4 m lighter lab floor with a centre seam, for the living quarters.")
def floor_tile_clean(b):
    b.box((4, 4, 0.18), (0, 0, -0.2), P.FLOOR_DARK)
    for sx in (-1, 1):
        b.box((1.96, 3.96, 0.04), (sx * 1.0, 0, -0.04), P.FLOOR_LIGHT)
    b.box((0.1, 3.9, 0.02), (0, 0, 0.0), P.METAL_DARK)


@piece("Floor_Grate_4m", "Floors", collider="box", box=((0, 0, -0.1), (4, 4, 0.2)),
       notes="4 x 4 m grating over a dark service pit, for corridors.")
def floor_grate(b):
    b.box((4, 4, 0.05), (0, 0, -0.35), P.VOID)
    for sx in (-1, 1):
        b.box((0.2, 4, 0.35), (sx * 1.9, 0, -0.35), P.METAL_DARK)
    for sy in (-1, 1):
        b.box((4, 0.2, 0.35), (0, sy * 1.9, -0.35), P.METAL_DARK)
    for i in range(16):
        y = -1.8 + 0.1 + i * 0.226
        b.box((3.6, 0.09, 0.05), (0, y, -0.05), P.GRATE)
    for x in (-0.9, 0.9):
        b.box((0.08, 3.6, 0.08), (x, 0, -0.12), P.METAL_DARK)
    for sy in (-1, 1):
        b.box((4, 0.2, 0.05), (0, sy * 1.9, -0.05), P.METAL)


@piece("Floor_Tile_Bath_4m", "Floors", collider="box", box=((0, 0, -0.1), (4, 4, 0.2)),
       notes="4 x 4 m small white tiles for bathrooms and the medical bay.")
def floor_tile_bath(b):
    b.box((4, 4, 0.18), (0, 0, -0.2), P.WALL_DARK)
    n = 8
    s = 4 / n
    for i in range(n):
        for j in range(n):
            mat = P.TILE if (i + j) % 5 else P.PORCELAIN
            b.box((s - 0.04, s - 0.04, 0.03), (-2 + (i + 0.5) * s, -2 + (j + 0.5) * s, -0.03), mat)


@piece("Floor_Hazard_Strip_4m", "Floors", collider="none",
       notes="Painted hazard stripes, 4 x 0.6 m. Lies on top of any floor tile.")
def floor_hazard_strip(b):
    floor_hazard(b, -2, 2, -0.3, 0.3)


@piece("Floor_Void_Slab", "Floors", collider="box", box=((0, 0, -0.45), (260, 260, 0.3)),
       notes="Huge dark base slab under a level, so gaps between floor pieces never show light.")
def floor_void(b):
    b.box((260, 260, 0.3), (0, 0, -0.6), P.VOID)


@piece("Floor_Hub_Disc", "Floors", collider="mesh",
       notes="Round floor for a 9 m hub with ring inlays and a glowing service ring. Pivot at the centre.")
def floor_hub_disc(b):
    b.cylinder(9.4, 0.18, (0, 0, -0.2), P.FLOOR_DARK, segments=48)
    rings = ((9.2, 7.2, P.FLOOR), (7.1, 6.95, P.GLOW_SOFT), (6.9, 4.6, P.FLOOR_LIGHT), (4.5, 0.0, P.FLOOR))
    for r_out, r_in, mat in rings:
        if r_in <= 0:
            b.cylinder(r_out, 0.04, (0, 0, -0.04), mat, segments=40)
        else:
            b.ring_segment(r_in, r_out, -0.04, 0.0, 0, 360, mat, segments=48)
    for k in range(16):
        a = k * 22.5
        r = math.radians(a)
        b.box((0.08, 2.3, 0.02), (math.cos(r) * 5.75, math.sin(r) * 5.75, 0.0), P.METAL_DARK, rot_z=a - 90)
    for k in range(8):
        a = k * 45 + 22.5
        r = math.radians(a)
        b.box((0.4, 0.4, 0.03), (math.cos(r) * 8.2, math.sin(r) * 8.2, 0.0), P.LAMP_WARM, rot_z=a)


@piece("Floor_Arena_Disc", "Floors", collider="mesh",
       notes="Round floor for the 25 m laboratory / boss arena: rings, radial seams and a glowing "
             "summoning circle in the middle. Pivot at the centre.")
def floor_arena_disc(b):
    R = 25.6
    k = R / 28.6
    b.cylinder(R, 0.2, (0, 0, -0.22), P.FLOOR_DARK, segments=72)
    bands = ((28.4, 22.5, P.FLOOR), (22.4, 21.9, P.METAL_DARK), (21.8, 12.6, P.FLOOR_LIGHT),
             (12.5, 12.0, P.METAL_DARK), (11.9, 7.0, P.FLOOR), (6.85, 6.6, P.GLOW_SOFT), (6.5, 0.0, P.FLOOR_DARK))
    bands = [(a * k if a > 7 else a, c * k if c > 7 else c, m) for a, c, m in bands]
    for r_out, r_in, mat in bands:
        if r_in <= 0:
            b.cylinder(r_out, 0.04, (0, 0, -0.04), mat, segments=48)
        else:
            b.ring_segment(r_in, r_out, -0.04, 0.0, 0, 360, mat, segments=72)
    for k in range(24):
        a = k * 15
        r = math.radians(a)
        b.box((0.07, 9.2 * k, 0.02), (math.cos(r) * 17.2 * k, math.sin(r) * 17.2 * k, 0.0), P.METAL_DARK, rot_z=a - 90)
    for k in range(12):
        a = k * 30 + 7.5
        r = math.radians(a)
        b.box((0.07, 4.0, 0.02), (math.cos(r) * 9.0, math.sin(r) * 9.0, 0.0), P.METAL_DARK, rot_z=a - 90)
    # inner sigil: a glowing ring of short dashes
    for k in range(36):
        a = k * 10
        r = math.radians(a)
        b.box((0.5, 0.16, 0.02), (math.cos(r) * 3.6, math.sin(r) * 3.6, 0.0), P.GLOW_SOFT, rot_z=a + 90)


# ---------------------------------------------------------------------------------- walls
@piece("Wall_Panel_4m", "Walls", fade=True,
       notes="4 m long, 5 m tall, 0.5 m thick wall panel, detailed on both faces. Centred on the wall line.")
def wall_4(b):
    wall(b, 4)


@piece("Wall_Panel_2m", "Walls", fade=True, notes="2 m version of Wall_Panel_4m for closing gaps.")
def wall_2(b):
    wall(b, 2)


@piece("Wall_Panel_4m_Vent", "Walls", fade=True, notes="Wall panel with a ventilation grille.")
def wall_vent(b):
    wall(b, 4, vent=True)


@piece("Wall_Panel_4m_Pipes", "Walls", fade=True, notes="Wall panel with a pipe run on its front face.")
def wall_pipes(b):
    wall(b, 4, pipes=True)


@piece("Wall_Panel_4m_B3", "Walls", fade=True, notes="Wall panel with the painted 'B-3' sector marking.")
def wall_b3(b):
    wall(b, 4, marking="B-3")


@piece("Wall_Pillar", "Walls", collider="box", box=((0, 0, 2.7), (1.0, 1.0, 5.4)), fade=True,
       notes="Structural steel pillar, 1 m square and 5.4 m tall: riveted flanges, collars and a small "
             "amber service light near the foot. Put one on every wall joint.")
def wall_pillar(b):
    b.box((1.2, 1.2, 0.5), (0, 0, 0), P.METAL_DARK, bevel=0.03)
    b.box((1.05, 1.05, 0.12), (0, 0, 0.5), P.RUST)
    b.box((0.86, 0.86, 4.5), (0, 0, 0.5), P.WALL_DARK)
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        b.box((0.2, 0.2, 4.5), (sx * 0.43, sy * 0.43, 0.5), P.METAL)
    for side in (-1, 1):
        # recessed web with bolt rows, like the concept's riveted columns
        b.box((0.5, 0.05, 4.1), (0, side * 0.44, 0.7), P.WALL)
        b.box((0.05, 0.5, 4.1), (side * 0.44, 0, 0.7), P.WALL)
        for x in (-0.17, 0.17):
            bolts(b, x, side * 0.47, 0.9, 4.6, step=0.5, size=0.06, mat=P.METAL_DARK)
        b.box((0.22, 0.05, 0.08), (0, side * 0.48, 0.78), P.LAMP_WARM)
    for z in (2.0, 3.6):
        b.box((1.04, 1.04, 0.16), (0, 0, z), P.METAL_DARK)
    b.box((1.14, 1.14, 0.4), (0, 0, 5.0), P.METAL, bevel=0.03)
    b.box((0.7, 0.7, 0.12), (0, 0, 5.4), P.METAL_DARK)


@piece("Wall_Pillar_Large", "Walls", collider="box", box=((0, 0, 4.0), (1.8, 1.8, 8.0)), fade=True,
       notes="2 m heavy support column, 8 m tall, for big halls and the arena rim.")
def wall_pillar_large(b):
    b.box((2.0, 2.0, 0.6), (0, 0, 0), P.METAL_DARK, bevel=0.04)
    b.box((1.6, 1.6, 7.0), (0, 0, 0.6), P.WALL_DARK)
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        b.box((0.26, 0.26, 7.0), (sx * 0.78, sy * 0.78, 0.6), P.METAL)
    for side in (-1, 1):
        b.box((0.9, 0.05, 4.2), (0, side * 0.81, 1.4), P.WALL)
        for x in (-0.3, 0.3):
            bolts(b, x, side * 0.84, 1.5, 5.5, step=0.5, size=0.07, mat=P.METAL_DARK)
        b.box((0.3, 0.05, 0.1), (0, side * 0.85, 1.25), P.LAMP_WARM)
        hazard_stripes(b, -0.7, 0.7, 0.6, 1.1, side * 0.82, face=side)
    for z in (2.8, 5.2):
        b.box((1.8, 1.8, 0.2), (0, 0, z), P.METAL)
    b.box((2.0, 2.0, 0.4), (0, 0, 7.6), P.METAL, bevel=0.04)


@piece("Wall_Cap_End", "Walls", collider="box", box=((0, 0, 2.6), (0.8, 0.8, 5.2)), fade=True,
       notes="Slim end post that finishes a wall run at an opening.")
def wall_cap_end(b):
    b.box((0.8, 0.8, 0.35), (0, 0, 0), P.METAL_DARK)
    b.box((0.64, 0.64, 4.85), (0, 0, 0.35), P.WALL_DARK)
    for side in (-1, 1):
        hazard_stripes(b, -0.3, 0.3, 0.35, 2.6, side * 0.33, stripe=0.15, face=side)
    b.box((0.8, 0.8, 0.25), (0, 0, 4.95), P.METAL)


@piece("Rock_Mass_4m", "Walls", collider="box", box=((0, 3.0, 2.8), (4.1, 6.0, 5.6)), fade=True,
       notes="Raw rock and concrete backfill, 4 m long and 6 m deep, that sits behind an outer wall "
             "(front at the wall's back face, mass toward +Z in Unity). Hides floor edges and frames the level.")
def rock_mass(b):
    b.box((4.1, 6.0, 5.3), (0, 3.0, 0), P.CONCRETE)
    b.box((4.1, 3.2, 0.5), (0, 3.6, 5.3), P.WALL_DARK, taper=0.9)
    b.box((4.1, 0.5, 0.35), (0, 0.25, 5.2), P.METAL_DARK)


# ---------------------------------------------------------------------------------- doors and gates
def door_frame(b, total_w, open_w, open_h, height=WALL_H, heavy=False):
    """Wall with a rectangular opening in the middle; returns the jamb x position."""
    side_w = (total_w - open_w) / 2
    for s in (-1, 1):
        cx = s * (open_w / 2 + side_w / 2)
        b.box((side_w, WALL_T - 0.16 + (0.3 if heavy else 0), height), (cx, 0, 0), P.WALL_DARK)
        for face in (-1, 1):
            y = face * (WALL_T / 2 + (0.15 if heavy else 0))
            b.box((side_w - 0.2, 0.08, height - 1.0), (cx, y, 0.4), P.WALL, bevel=0.02)
            b.box((side_w, 0.12, 0.35), (cx, y, 0), P.METAL_DARK)
    hdr_h = height - open_h
    b.box((open_w, WALL_T - 0.16 + (0.3 if heavy else 0), hdr_h), (0, 0, open_h), P.WALL_DARK)
    # jamb and lintel trims, both faces
    t = 0.32 if heavy else 0.22
    for face in (-1, 1):
        y = face * (WALL_T / 2 + (0.2 if heavy else 0.06))
        for s in (-1, 1):
            b.box((t, 0.2, open_h + t), (s * (open_w / 2 + t / 2), y, 0), P.METAL, bevel=0.02)
        b.box((open_w + 2 * t, 0.2, t), (0, y, open_h), P.METAL, bevel=0.02)
    b.box((total_w, WALL_T + 0.12 + (0.3 if heavy else 0), 0.25), (0, 0, height - 0.25), P.METAL, bevel=0.02)
    return side_w


@piece("Door_Sliding", "Doors", parts=("Leaf_L", "Leaf_R"),
       gate={"travel": 1.45, "auto": True, "startsOpen": False, "sensor": (0, 0, 1.5, 3.4, 6.0, 3.0)},
       lights=[{"offset": (0, -0.55, 3.75), "color": "#9ED8FF", "range": 5.0, "intensity": 1.6}],
       notes="6 m wall section with a 3 x 3.4 m sliding double door. Opens on its own when a character "
             "comes near (Gate with Auto Open). Fits a 6 m gap in a wall run.")
def door_sliding(b, parts):
    door_frame(b, 6.0, DOOR_W, DOOR_H)
    for face in (-1, 1):
        y = face * 0.36
        b.box((0.9, 0.06, 0.18), (0, y, DOOR_H + 0.3), P.METAL_DARK)
        b.box((0.7, 0.04, 0.1), (0, y + face * 0.03, DOOR_H + 0.34), P.LIGHT_GREEN)
    floor_hazard(b, -DOOR_W / 2, DOOR_W / 2, -0.4, 0.4)
    for name, s in (("Leaf_L", -1), ("Leaf_R", 1)):
        leaf = parts[name]
        cx = s * DOOR_W / 4
        leaf.box((DOOR_W / 2 + 0.04, 0.16, DOOR_H), (cx, 0, 0), P.WALL_LIGHT, bevel=0.02)
        for face in (-1, 1):
            leaf.box((DOOR_W / 2 - 0.3, 0.04, 1.4), (cx, face * 0.09, 1.7), P.WALL)
            leaf.box((0.12, 0.05, 2.6), (s * 0.08, face * 0.09, 0.4), P.METAL_DARK)
            leaf.box((DOOR_W / 2 - 0.3, 0.04, 0.12), (cx, face * 0.1, 1.0), P.HAZARD)


@piece("Gate_Blast", "Gates", parts=("Leaf_L", "Leaf_R"),
       gate={"travel": 2.05, "auto": False, "startsOpen": False},
       lights=[{"offset": (0, -0.75, 4.75), "color": "#FF3A30", "range": 7.0, "intensity": 3.0},
               {"offset": (0, 0.75, 4.75), "color": "#FF3A30", "range": 7.0, "intensity": 3.0}],
       notes="Event gate: 8 m heavy wall section with a 4 x 4.2 m split blast door and a red status bar "
             "that turns green when open. Closed until a script calls Gate.Open() (or Gate.Open(\"id\")).")
def gate_blast(b, parts):
    side = door_frame(b, 8.0, GATE_W, GATE_H, height=6.0, heavy=True)
    for face in (-1, 1):
        y = face * 0.58
        # status bar over the opening and hazard frames on the housings
        b.box((2.4, 0.12, 0.3), (0, y, 4.6), P.METAL_DARK)
        b.box((2.2, 0.06, 0.16), (0, y + face * 0.06, 4.67), P.LIGHT_RED)
        for s in (-1, 1):
            x_in, x_out = s * (GATE_W / 2 + 0.45), s * (GATE_W / 2 + side - 0.55)
            hazard_stripes(b, min(x_in, x_out), max(x_in, x_out), 0.3, 0.9, y, face=face)
            b.box((0.5, 0.1, 0.7), (s * (GATE_W / 2 + 1.0), y, 2.4), P.METAL_DARK)
            b.box((0.3, 0.06, 0.18), (s * (GATE_W / 2 + 1.0), y + face * 0.05, 2.75), P.LIGHT_RED)
        for s in (-1, 1):
            b.cylinder(0.16, 5.2, (s * (GATE_W / 2 + side - 0.3), y + face * 0.1, 0), P.METAL, segments=8)
    floor_hazard(b, -GATE_W / 2, GATE_W / 2, -0.7, 0.7)
    for name, s in (("Leaf_L", -1), ("Leaf_R", 1)):
        leaf = parts[name]
        cx = s * GATE_W / 4
        leaf.box((GATE_W / 2 + 0.06, 0.42, GATE_H), (cx, 0, 0), P.METAL, bevel=0.03)
        for face in (-1, 1):
            y = face * 0.22
            for z in (0.5, 1.6, 2.7):
                leaf.box((GATE_W / 2 - 0.35, 0.08, 0.7), (cx, y, z), P.WALL)
            leaf.box((0.22, 0.1, GATE_H - 0.4), (s * 0.12, y, 0.2), P.METAL_DARK)
            leaf.box((GATE_W / 2 - 0.25, 0.09, 0.22), (cx, y, 3.75), P.HAZARD)
            # interlocking teeth on the meeting edge
            for z in (0.8, 2.0, 3.2):
                leaf.box((0.18, 0.12, 0.4), (s * 0.04, y, z), P.METAL_DARK)


@piece("Gate_Shutter", "Gates", parts=("Leaf_Down",),
       gate={"travel": 4.0, "auto": False, "startsOpen": False},
       lights=[{"offset": (0, -0.6, 4.3), "color": "#FF3A30", "range": 5.5, "intensity": 2.2}],
       notes="Event gate: 6 m wall section with a 4 x 4 m shutter that sinks into the floor when opened.")
def gate_shutter(b, parts):
    door_frame(b, 6.0, 4.0, 4.0)
    for face in (-1, 1):
        y = face * 0.38
        b.box((1.6, 0.06, 0.16), (0, y, 4.32), P.LIGHT_RED)
        for s in (-1, 1):
            hazard_stripes(b, s * 2.25 - 0.25, s * 2.25 + 0.25, 0.4, 3.9, y, stripe=0.25, face=face)
    leaf = parts["Leaf_Down"]
    leaf.box((4.0, 0.24, 4.0), (0, 0, 0), P.METAL_DARK)
    for face in (-1, 1):
        for i in range(8):
            leaf.box((3.9, 0.06, 0.32), (0, face * 0.13, 0.1 + i * 0.48), P.METAL if i % 2 else P.WALL)
        leaf.box((3.9, 0.07, 0.25), (0, face * 0.14, 3.7), P.HAZARD)


@piece("Door_Airlock_Locked", "Doors", fade=True,
       notes="Decoration: a sealed round vault door set into a 4 m wall panel (never opens).")
def door_airlock(b):
    b.box((4, WALL_T - 0.16, WALL_H), (0, 0, 0), P.WALL_DARK)
    wall_face(b, 4, +1)
    b.box((4, WALL_T + 0.12, 0.25), (0, 0, WALL_H - 0.25), P.METAL, bevel=0.02)
    # front: the vault door
    y = -WALL_T / 2
    b.box((4, 0.12, 0.35), (0, y - 0.04, 0), P.METAL_DARK)
    b.cylinder(1.75, 0.3, (0, y, 2.1), P.METAL_DARK, segments=24, rot_x=90, base=False)
    b.cylinder(1.5, 0.3, (0, y - 0.12, 2.1), P.METAL, segments=24, rot_x=90, base=False)
    b.cylinder(1.15, 0.2, (0, y - 0.28, 2.1), P.WALL, segments=20, rot_x=90, base=False)
    for k in range(8):
        a = math.radians(k * 45)
        b.box((0.25, 0.18, 0.25), (math.cos(a) * 1.6, y - 0.18, 2.1 - 0.125 + math.sin(a) * 1.6), P.METAL_DARK)
    for k in range(3):
        a = k * 120
        b.box((0.16, 0.12, 1.0), (0, y - 0.4, 2.1), P.METAL_DARK, rot_y=a)
    b.cylinder(0.25, 0.25, (0, y - 0.42, 2.1), P.RUST, segments=10, rot_x=90, base=False)
    b.box((0.4, 0.08, 0.14), (0, y - 0.1, 4.15), P.LIGHT_RED)


# ---------------------------------------------------------------------------------- vertical
STAIR_RISE = 3.0
STAIR_RUN = 5.4


@piece("Stairs_Wide", "Stairs", parts=("COL_Ramp",),
       notes="4 m wide stairs climbing 3 m over 5.4 m toward +Z (Unity), with railings. Meets Catwalk_4m. "
             "An invisible ramp gives smooth walking.")
def stairs_wide(b, parts):
    steps = 12
    rise, run = STAIR_RISE / steps, STAIR_RUN / steps
    for i in range(steps):
        z = i * rise
        y = -STAIR_RUN / 2 + (i + 0.5) * run
        b.box((3.6, run + 0.02, 0.08), (0, y, z + rise - 0.08), P.GRATE)
        b.box((3.6, 0.06, 0.06), (0, y - run / 2 + 0.03, z + rise - 0.06), P.HAZARD)
    for s in (-1, 1):
        x = s * 1.9
        pts = [(x, -STAIR_RUN / 2, 0.0), (x, STAIR_RUN / 2, STAIR_RISE)]
        b.tube(pts, 0.14, P.METAL_DARK, sides=4)
        rail = [(x, -STAIR_RUN / 2, 1.0), (x, STAIR_RUN / 2, STAIR_RISE + 1.0)]
        b.tube(rail, 0.05, P.METAL, sides=6)
        mid = [(x, -STAIR_RUN / 2, 0.55), (x, STAIR_RUN / 2, STAIR_RISE + 0.55)]
        b.tube(mid, 0.03, P.METAL_DARK, sides=5)
        for t in (0.0, 0.5, 1.0):
            y = -STAIR_RUN / 2 + t * STAIR_RUN
            z = t * STAIR_RISE
            b.box((0.08, 0.08, 1.0), (x, y, z), P.METAL)
    for y in (-0.8, 1.6):
        z = (y + STAIR_RUN / 2) / STAIR_RUN * STAIR_RISE
        b.box((0.2, 0.2, z), (0, y, 0), P.METAL_DARK)
    ramp = parts["COL_Ramp"]
    ramp.prism([(-STAIR_RUN / 2, 0), (STAIR_RUN / 2, 0), (STAIR_RUN / 2, STAIR_RISE)], -1.8, 1.8, P.VOID,
               matrix=_side_matrix())


def _side_matrix():
    """Turns a prism drawn in the (y, z) plane and extruded along X into place."""
    from mathutils import Matrix
    return Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


@piece("Stairs_Short", "Stairs", parts=("COL_Ramp",),
       notes="4 m wide, three steps up 0.6 m toward +Z (Unity). For daises and raised rooms.")
def stairs_short(b, parts):
    for i in range(3):
        b.box((4, 0.4 * (3 - i), 0.2 * (i + 1)), (0, -0.6 + 0.2 * i + 0.4 * (3 - i) / 2 - 0.0, 0), P.METAL_DARK)
        b.box((4, 0.06, 0.05), (0, -0.6 + 0.4 * i + 0.03, 0.2 * (i + 1) - 0.05), P.HAZARD)
    ramp = parts["COL_Ramp"]
    ramp.prism([(-0.6, 0), (0.6, 0), (0.6, 0.6)], -2.0, 2.0, P.VOID, matrix=_side_matrix())


def railing(b, length, y=0.0, z=0.0):
    posts = max(2, int(round(length / 1.0)) + 1)
    for i in range(posts):
        x = -length / 2 + i * length / (posts - 1)
        b.box((0.07, 0.07, 1.05), (x, y, z), P.METAL)
    b.cylinder(0.045, length, (-length / 2, y, z + 1.05), P.METAL, segments=6, rot_y=90, base=False)
    b.cylinder(0.03, length, (-length / 2, y, z + 0.6), P.METAL_DARK, segments=5, rot_y=90, base=False)
    b.box((length, 0.03, 0.14), (0, y, z + 0.02), P.HAZARD)


@piece("Railing_4m", "Railings", collider="box", box=((0, 0, 0.55), (4.0, 0.15, 1.1)),
       notes="4 m safety railing along X, 1.1 m tall.")
def railing_4(b):
    railing(b, 4.0)


@piece("Railing_2m", "Railings", collider="box", box=((0, 0, 0.55), (2.0, 0.15, 1.1)),
       notes="2 m safety railing along X.")
def railing_2(b):
    railing(b, 2.0)


CATWALK_H = 3.0


def catwalk_deck(b, length, width, rails):
    z = CATWALK_H
    b.box((length, width, 0.12), (0, 0, z - 0.12), P.GRATE)
    for s in (-1, 1):
        b.box((length, 0.14, 0.3), (0, s * (width / 2 - 0.07), z - 0.3), P.METAL_DARK)
    for i in range(int(length / 0.5)):
        x = -length / 2 + 0.25 + i * 0.5
        b.box((0.05, width - 0.3, 0.05), (x, 0, z - 0.02), P.METAL_DARK)
    for x in (-length / 2 + 0.3, length / 2 - 0.3):
        for s in (-1, 1):
            b.box((0.18, 0.18, z - 0.3), (x, s * (width / 2 - 0.2), 0), P.METAL_DARK)
        b.tube([(x, -width / 2 + 0.2, 0.3), (x, width / 2 - 0.2, z - 0.4)], 0.04, P.METAL, sides=4)
        b.box((0.4, width, 0.12), (x, 0, z - 0.42), P.METAL_DARK)
    for s in rails:
        railing(b, length, y=s * (width / 2 - 0.05), z=z)


@piece("Catwalk_4m", "Catwalks", notes="4 m long, 2.4 m wide grated walkway 3 m up on legs, railings both sides.")
def catwalk_4(b):
    catwalk_deck(b, 4.0, 2.4, (-1, 1))


@piece("Catwalk_4m_Wall", "Catwalks",
       notes="4 m catwalk with a railing on the front (-Z in Unity) only, to run along a wall.")
def catwalk_4_wall(b):
    catwalk_deck(b, 4.0, 2.4, (-1,))


@piece("Catwalk_Landing", "Catwalks",
       notes="2.4 x 2.4 m catwalk landing without railings, where stairs meet a walkway.")
def catwalk_landing(b):
    catwalk_deck(b, 2.4, 2.4, ())


@piece("Platform_Dais_6m", "Catwalks", parts=("COL_Ramp",),
       notes="Round two-tier dais, 6 m radius and 0.5 m tall, with a glowing edge. Pivot at the centre.")
def platform_dais(b, parts):
    b.cylinder(6.0, 0.25, (0, 0, 0), P.METAL_DARK, segments=40)
    b.cylinder(5.85, 0.02, (0, 0, 0.25), P.FLOOR_LIGHT, segments=40)
    b.ring_segment(5.85, 6.02, 0.2, 0.27, 0, 360, P.GLOW_SOFT, segments=40)
    b.cylinder(4.6, 0.25, (0, 0, 0.25), P.METAL_DARK, segments=36)
    b.cylinder(4.5, 0.02, (0, 0, 0.5), P.FLOOR, segments=36)
    for k in range(12):
        a = math.radians(k * 30 + 15)
        b.box((0.5, 0.12, 0.03), (math.cos(a) * 5.3, math.sin(a) * 5.3, 0.26), P.HAZARD, rot_z=k * 30 + 105)
    ramp = parts["COL_Ramp"]
    ramp.cylinder(6.0, 0.25, (0, 0, 0), P.VOID, segments=20, radius_top=4.6)
    ramp.cylinder(4.6, 0.25, (0, 0, 0.25), P.VOID, segments=20, radius_top=4.5)


@piece("Ceiling_Panel_4m", "Ceilings", collider="box", box=((0, 0, 0.15), (4, 4, 0.3)),
       lights=[{"offset": (0, 0, -0.4), "color": "#BFE6FF", "range": 8.0, "intensity": 2.0}],
       notes="4 x 4 m ceiling panel with a recessed light; its pivot is the underside. Put it at wall height "
             "for third-person interiors (the isometric demo leaves ceilings off so the camera can see in).")
def ceiling_panel(b):
    b.box((4, 4, 0.3), (0, 0, 0), P.WALL_DARK)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((1.9, 1.9, 0.05), (sx, sy, -0.05), P.WALL)
    b.box((1.6, 0.5, 0.06), (0, 0, -0.08), P.LAMP_COLD)
    b.box((4, 0.16, 0.12), (0, 0, -0.12), P.METAL_DARK)
