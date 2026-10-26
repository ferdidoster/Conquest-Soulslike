"""Floors, walls, stands, the temple gate, stairs, paths, bridges and the plateau."""

import math

from . import palette as P
from .geo import sag_curve
from .kit import piece

ARENA_RADIUS = 13.0      # fighting floor
WALKWAY_RADIUS = 17.0    # stone ring between the floor and the wall
WALL_THICKNESS = 0.8
STANDS_DEPTH = 6.2


def _cracks(b, r0, r1, a0, a1, count):
    """Thin dark cracks lying on a floor wedge."""
    rng = b.rng
    for _ in range(count):
        a = math.radians(rng.uniform(a0 + 3, a1 - 3))
        r = rng.uniform(r0 + 0.5, r1 - 0.5)
        pts = []
        x, y = math.cos(a) * r, math.sin(a) * r
        heading = rng.uniform(0, math.pi * 2)
        for _ in range(rng.randint(3, 5)):
            pts.append((x, y, 0.0))
            heading += rng.uniform(-0.8, 0.8)
            step = rng.uniform(0.4, 0.9)
            x += math.cos(heading) * step
            y += math.sin(heading) * step
        b.ribbon(pts, rng.uniform(0.07, 0.11))


@piece("Floor_Arena_Center", "Floor", notes="Centre disc of the arena floor with the emblem inlay (r = 5 m).")
def floor_arena_center(b):
    # flush, non-overlapping bands: a raised inlay edge would draw a broken ink line
    b.disc(0.75, -0.5, 0.0, P.INLAY, segments=48)
    b.ring_segment(0.75, 0.95, -0.5, 0.0, 0, 360, P.FLOOR_DISC, segments=48)
    b.ring_segment(0.95, 1.05, -0.5, 0.0, 0, 360, P.INLAY, segments=48)
    b.ring_segment(1.05, 3.95, -0.5, 0.0, 0, 360, P.FLOOR_DISC, segments=48)
    b.ring_segment(3.95, 4.45, -0.5, 0.0, 0, 360, P.INLAY, segments=48)
    b.ring_segment(4.45, 5.0, -0.5, 0.0, 0, 360, P.SAND, segments=48)
    for k in range(8):
        rad = math.radians(k * 45)
        b.ribbon([(math.cos(rad) * 1.1, math.sin(rad) * 1.1), (math.cos(rad) * 3.95, math.sin(rad) * 3.95)], 0.1)
    _cracks(b, 1.2, 4.0, 0, 360, 4)


@piece("Floor_Arena_Segment", "Floor",
       notes="45 degree wedge of the arena floor from r = 5 to 13 m with the inlay ring. Pivot = arena centre.")
def floor_arena_segment(b):
    a0, a1 = 67.5, 112.5
    b.ring_segment(5.0, 9.3, -0.5, 0.0, a0, a1, P.SAND)
    b.ring_segment(9.3, 10.1, -0.5, 0.0, a0, a1, P.INLAY)
    b.ring_segment(10.1, ARENA_RADIUS, -0.5, 0.0, a0, a1, P.SAND)
    # radial seam on one edge so adjacent wedges read as separate slabs
    rad = math.radians(a0 + 0.2)
    b.ribbon([(math.cos(rad) * 5.0, math.sin(rad) * 5.0), (math.cos(rad) * 13, math.sin(rad) * 13)], 0.1, z=0.006)
    mid = math.radians(90)
    b.ribbon([(math.cos(mid) * 10.1, math.sin(mid) * 10.1), (math.cos(mid) * 13, math.sin(mid) * 13)], 0.09, z=0.006)
    _cracks(b, 5.0, 13.0, a0, a1, 3)


@piece("Floor_Walkway_Segment", "Floor",
       notes="45 degree wedge of paving blocks from r = 13 to 17 m around the arena floor.")
def floor_walkway_segment(b):
    a0, a1 = 67.5, 112.5
    b.block_ring(ARENA_RADIUS, 15.0, -0.5, 0.0, a0, a1, 5, P.STONE_LIGHT, gap=0.07, bevel=0.03)
    b.block_ring(15.0, WALKWAY_RADIUS, -0.5, 0.0, a0 + 3, a1 + 3, 6, P.SAND_DARK, gap=0.07, bevel=0.03)


@piece("Wall_Arena_Segment", "Walls",
       notes="15 degree segment of the low ring wall at r = 17 m. Leave segments out for openings.")
def wall_arena_segment(b):
    a0, a1 = 82.5, 97.5
    r0, r1 = WALKWAY_RADIUS, WALKWAY_RADIUS + WALL_THICKNESS
    b.block_ring(r0, r1, 0.0, 0.55, a0, a1, 3, P.STONE, gap=0.05, bevel=0.03)
    b.block_ring(r0, r1, 0.55, 1.1, a0, a1, 2, P.STONE, gap=0.05, bevel=0.03)
    b.ring_segment(r0 - 0.1, r1 + 0.1, 1.1, 1.3, a0, a1, P.STONE_LIGHT, bevel=0.03)


@piece("Stands_Segment", "Walls",
       notes="15 degree segment of tiered stone seating behind the ring wall (r = 17.8 to 24 m).")
def stands_segment(b):
    a0, a1 = 82.5, 97.5
    r_wall = WALKWAY_RADIUS + WALL_THICKNESS
    r_back = r_wall + STANDS_DEPTH
    tiers = 5
    for i in range(tiers):
        r_in = r_wall + i * (STANDS_DEPTH - 0.6) / tiers
        mat = P.SANDSTONE if i % 2 == 0 else P.ROCK_LIGHT
        b.block_ring(r_in, r_back, 0.0, 0.55 * (i + 1) + 0.6, a0, a1, 2, mat, gap=0.04, bevel=0.03)
    b.block_ring(r_back - 0.6, r_back, 0.0, 0.55 * tiers + 1.6, a0, a1, 2, P.SANDSTONE_DARK, gap=0.05, bevel=0.03)


@piece("Pillar_Banner", "Architecture", collider="box", box=((0, 0, 3.6), (1.6, 1.6, 7.2)),
       light={"offset": (0, 0, 7.8), "color": "#FFA34A", "range": 7.0, "intensity": 2.5},
       notes="7 m stone pillar with a red banner on its front (-Y / Unity +Z) and a fire bowl on top.")
def pillar_banner(b):
    b.box((1.6, 1.6, 0.6), (0, 0, 0), P.STONE_DARK, bevel=0.05)
    for i, h in enumerate((2.2, 2.0, 2.0)):
        z = 0.6 + sum((2.2, 2.0, 2.0)[:i])
        b.box((1.15, 1.15, h - 0.04), (0, 0, z), P.STONE, bevel=0.04, rot_z=b.rng.uniform(-2, 2))
    b.box((1.5, 1.5, 0.4), (0, 0, 6.8), P.STONE_LIGHT, bevel=0.05)
    b.cylinder(0.35, 0.25, (0, 0, 7.2), P.BRONZE, segments=8, radius_top=0.55)
    b.flame((0, 0, 7.4), 0.9)
    _banner(b, width=0.9, top=6.35, bottom=2.0, y=-0.7)


def _banner(b, width, top, bottom, y, cols=4, rows=6, mat=P.CLOTH, rod=True, glyph=True):
    """A hanging banner in the XZ plane at depth y with a tattered, notched bottom edge."""
    grid, sway = [], []
    for r in range(rows + 1):
        t = r / rows
        row, srow = [], []
        for c in range(cols + 1):
            u = c / cols
            # pointed tails at odd columns, shorter corners: the torn edge from the concept
            bottom_c = bottom - width * 0.25 if c % 2 == 1 else bottom + (width * 0.3 if c in (0, cols) else 0.0)
            z = top + (bottom_c - top) * t
            row.append(((u - 0.5) * width, y - 0.04 * math.sin(t * math.pi), z))
            srow.append(t * t)
        grid.append(row)
        sway.append(srow)
    b.sheet(grid, mat, sway)
    if rod:
        b.box((width + 0.3, 0.08, 0.08), (0, y, top - 0.04), P.METAL_DARK)
    if glyph:
        height = (top - bottom) * 0.62
        _glyph_sheet(b, (0, top - (top - bottom) * 0.45), height, y - 0.06, top, bottom)


def _glyph_sheet(b, center, height, y, top, bottom):
    before = set(b.bm.verts)
    b.glyph(center, height, y)
    for v in [v for v in b.bm.verts if v not in before]:
        t = (top - v.co.z) / max(top - bottom, 1e-3)
        v[b.sway] = max(0.0, min(1.0, t)) ** 2


@piece("Pillar_Short", "Architecture", collider="box", box=((0, 0, 0.8), (1.1, 1.1, 1.6)),
       notes="Waist-high stone post; use it to cap wall openings.")
def pillar_short(b):
    b.box((1.1, 1.1, 0.9), (0, 0, 0), P.STONE, bevel=0.05)
    b.box((0.95, 0.95, 0.7), (0.03, -0.02, 0.9), P.STONE_LIGHT, bevel=0.05, rot_z=6)


@piece("Pillar_Broken", "Architecture", collider="box", box=((0, 0, 1.2), (1.2, 1.2, 2.4)),
       notes="Broken pillar stump with a fallen chunk.")
def pillar_broken(b):
    b.box((1.25, 1.25, 0.4), (0, 0, 0), P.STONE_DARK, bevel=0.04)
    b.box((1.0, 1.0, 1.4), (0, 0, 0.4), P.STONE, bevel=0.04)
    b.box((1.0, 0.55, 0.7), (0.0, 0.22, 1.8), P.STONE, bevel=0.04, rot_y=8)
    b.box((0.45, 0.4, 0.35), (-0.18, -0.2, 1.8), P.STONE_LIGHT, bevel=0.03, rot_z=20)
    b.box((0.7, 0.6, 0.5), (1.2, -0.6, 0), P.STONE, bevel=0.04, rot_z=35, rot_x=12)


@piece("Stairs_Wide", "Architecture",
       notes="6 m wide stairs, 6 steps rising 1.5 m toward +Y (Unity -Z from the front). Pivot = bottom front.")
def stairs_wide(b):
    steps, rise, run, width = 6, 0.25, 0.5, 6.0
    for i in range(steps):
        b.box((width, run * (steps - i), rise), (0, run * i + run * (steps - i) / 2, rise * i),
              P.STONE_LIGHT if i % 2 == 0 else P.STONE, bevel=0.025)
    for x in (-width / 2 - 0.35, width / 2 + 0.35):
        b.prism([(-0.35, 0), (0.35, 0), (0.35, steps * run), (-0.35, steps * run)], 0, steps * rise + 0.4,
                P.STONE_DARK, loc=(x, 0, 0), bevel=0.0)


@piece("Path_Segment", "Floor", notes="4 x 4 m paved path with curbs; tile it along Y.")
def path_segment(b):
    for ix in range(2):
        for iy in range(2):
            b.box((1.94, 1.94, 0.5), (-1 + ix * 2, -1 + iy * 2, -0.5), P.SAND_DARK if (ix + iy) % 2 else P.STONE_LIGHT,
                  bevel=0.03)
    for x in (-2.2, 2.2):
        b.box((0.4, 3.96, 0.85), (x, 0, -0.5), P.STONE, bevel=0.04)


@piece("Bridge_Arch_Segment", "Architecture",
       notes="8 m bridge span on arches over the canyon, deck top at 0. Chain along Y; piers reach 20 m down.")
def bridge_arch_segment(b):
    length, width = 8.0, 4.0
    half = length / 2
    b.box((width, length - 0.04, 0.6), (0, 0, -0.6), P.STONE_LIGHT, bevel=0.04)
    # parapets as separate blocks, one knocked out
    for side in (-1, 1):
        for i in range(4):
            if side == 1 and i == 2:
                continue
            b.box((0.3, 1.9, 0.7), (side * (width / 2 + 0.05), -half + 1.0 + i * 2.0, 0), P.STONE, bevel=0.03)
    # spandrel above the arch, built from convex slices
    arch_r, spring = 3.4, -4.6
    n = 8
    for i in range(n):
        y0 = -arch_r + 2 * arch_r * i / n
        y1 = -arch_r + 2 * arch_r * (i + 1) / n
        z0 = spring + math.sqrt(max(arch_r ** 2 - y0 ** 2, 0))
        z1 = spring + math.sqrt(max(arch_r ** 2 - y1 ** 2, 0))
        pts = [(y0, z0), (y1, z1), (y1, -0.6), (y0, -0.6)]
        _slab_yz(b, pts, width - 0.6, P.STONE)
        # arch voussoir band
        _slab_yz(b, [(y0, z0 - 0.35), (y1, z1 - 0.35), (y1, z1), (y0, z0)], width - 0.4, P.STONE_DARK)
    for side in (-1, 1):
        y = side * (half - 0.3)
        b.box((width - 0.6, 0.6 + 0.02, 20.0), (0, y, -20.6), P.STONE, bevel=0.0)
        b.box((width - 0.4, 0.64, 0.4), (0, y, spring - 0.4), P.STONE_DARK)
    # dangling cable under the deck: the tech grit
    b.tube(sag_curve((-1.2, -half, -0.8), (-1.2, half, -0.8), 1.1, 8), 0.05, P.METAL_DARK)


def _slab_yz(b, pts_yz, width, mat):
    """Convex polygon in the YZ plane extruded along X (centred)."""
    from mathutils import Matrix
    pts = [(y, z) for y, z in pts_yz]
    # prism extrudes along Z; rotate so its local Z becomes world X, local X -> Y, local Y -> Z
    m = Matrix(((0, 0, 1, -width / 2), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    b.prism(pts, 0.0, width, mat, matrix=m)


@piece("Temple_Gate", "Architecture",
       notes="The boss temple: stepped tower, dark gate, giant banner and front stairs. Pivot = foot of the "
             "stairs, front faces -Y (Unity +Z). About 17 x 12 m footprint, 41 m tall.")
def temple_gate(b):
    # platform + stairs
    b.box((17, 11.5, 1.5), (0, 5.75, 0), P.STONE_DARK, bevel=0.06)
    for i in range(6):
        b.box((6.5, 0.5 * (6 - i), 0.25), (0, -3 + 0.5 * i + 0.25 * (6 - i), 0.25 * i), P.STONE_LIGHT,
              bevel=0.02)
    # stepped tower: a tall, tapering monolith that dominates the arena like the concept
    tiers = [((12.5, 9, 14.0), 1.0, 1.5, 0.94), ((9.8, 7.4, 10.0), 1.8, 15.5, 0.93),
             ((7.2, 5.6, 8.0), 2.6, 25.5, 0.9), ((4.6, 3.8, 5.0), 3.4, 33.5, 0.82), ((2.4, 2.2, 3.0), 4.2, 38.5, 0.4)]
    for (sx, sy, sz), y0, z0, taper in tiers:
        b.box((sx, sy, sz), (0, y0 + sy / 2, z0), P.STONE_TEMPLE, taper=taper, bevel=0.06)
        if sz > 4.5:
            b.box((sx + 0.5, sy + 0.5, 0.35), (0, y0 + sy / 2, z0 + sz - 0.1), P.STONE_LIGHT, bevel=0.04)
    # arched window in the crown, the round-topped niche of the concept
    for w, h, z in ((1.8, 2.4, 34.0), (1.4, 0.35, 36.4), (0.9, 0.3, 36.75), (0.4, 0.2, 37.05)):
        b.box((w, 0.4, h), (0, 3.3, z), P.VOID)
    for x in (-1.15, 1.15):
        b.box((0.35, 0.5, 3.3), (x, 3.25, 33.8), P.STONE_LIGHT, bevel=0.03)
    # vertical rock-cut ribs on the upper tiers
    for x in (-3.9, 3.9):
        b.box((0.8, 0.6, 9.5), (x, 1.75, 15.6), P.STONE_DARK, taper=0.9)
    for x in (-2.7, 2.7):
        b.box((0.6, 0.5, 7.5), (x, 2.55, 25.6), P.STONE_DARK, taper=0.9)
    # buttress towers flanking the gate
    for x in (-5.6, 5.6):
        b.box((2.6, 2.6, 18.5), (x, 2.3, 1.5), P.STONE_TEMPLE, taper=0.82, bevel=0.06)
        b.box((2.4, 2.4, 0.5), (x, 2.3, 19.7), P.STONE_LIGHT, bevel=0.05)
        b.box((1.2, 1.2, 2.6), (x, 2.3, 20.1), P.STONE_DARK, taper=0.4)
        for z in (5.0, 9.5, 13.5, 17.0):
            b.box((2.7, 0.2, 0.25), (x, 0.95, z), P.STONE_DARK)
    # gate: dark doorway in a stepped frame
    for x in (-2.0, 2.0):
        b.box((0.6, 0.6, 7.6), (x, 0.55, 1.5), P.STONE_LIGHT, bevel=0.05)
    b.box((3.4, 0.3, 6.3), (0, 0.75, 1.5), P.VOID)
    b.box((5.4, 0.8, 0.6), (0, 0.55, 9.1), P.STONE_DARK, bevel=0.04)
    # recessed panel with the giant banner
    b.box((5.0, 1.3, 16.5), (0, 1.3, 9.7), P.STONE_DARK, bevel=0.04)
    # stepped arch over the banner recess, like the arched niche in the concept
    for i, w in enumerate((4.4, 3.4, 2.2)):
        b.box((w, 2.0, 0.8), (0, 1.6, 26.2 + i * 0.8), P.STONE_DARK, bevel=0.03)
    b.box((5.8, 2.0, 0.5), (0, 1.55, 25.9), P.STONE_LIGHT, bevel=0.03)
    _banner(b, width=3.6, top=25.4, bottom=11.0, y=0.5, cols=6, rows=10)
    # ribs on the facade
    for x in (-3.3, 3.3):
        b.box((0.35, 0.3, 9.5), (x, 0.9, 1.5), P.STONE_DARK)
    # tech grit: conduits up the side, an antenna and junction boxes
    for side in (-1, 1):
        x = side * 6.6
        b.tube([(x, 4.5, 1.5), (x, 4.5, 14.5), (side * 5.0, 4.5, 16.0), (side * 5.0, 4.5, 24.5)], 0.14, P.METAL_DARK)
        b.box((0.7, 0.5, 0.9), (x + side * 0.2, 4.5, 3.0), P.METAL, bevel=0.03)
        b.box((0.2, 0.06, 0.12), (x + side * 0.2, 4.24, 3.6), P.TECH_LIGHT)
    b.cylinder(0.06, 4.0, (0.6, 5.3, 41.3), P.METAL_DARK, segments=4)
    b.cylinder(0.5, 0.2, (0.6, 5.3, 43.8), P.METAL, segments=8, radius_top=0.1, rot_x=30)


def plateau_outline(radius, north_bulge, rng, points=56):
    """Irregular outline of a plateau; north_bulge pushes the +Y side out (for the temple)."""
    pts = []
    for k in range(points):
        a = 2 * math.pi * k / points
        r = radius + north_bulge * max(0.0, math.sin(a)) ** 4 + rng.uniform(-0.8, 0.8)
        pts.append((math.cos(a) * r, math.sin(a) * r))
    return pts


def _plateau(b, radius, north_bulge, depth):
    rng = b.rng
    outline = plateau_outline(radius, north_bulge, rng)
    b.prism(outline, -0.35, -0.05, P.SAND_DARK)
    z = -0.35
    scale = 1.0
    layer = 0
    while z > -depth:
        h = rng.uniform(2.5, 4.5)
        pts = [(x * scale * rng.uniform(0.985, 1.015), y * scale * rng.uniform(0.985, 1.015)) for x, y in outline]
        b.prism(pts, z - h, z + 0.02, P.ROCK if layer % 2 == 0 else P.ROCK_LIGHT, top_scale=1.0)
        z -= h
        scale *= rng.uniform(1.02, 1.05)
        layer += 1


@piece("Plateau_Large", "Nature",
       notes="Rock plateau the arena stands on: r = 28 m with a bulge to the north for the temple, top at 0, "
             "cliffs 20 m down.")
def plateau_large(b):
    b.rng.seed(7)
    _plateau(b, 28.0, 6.0, 20.0)


@piece("Plateau_Small", "Nature", notes="Small rock landing (r = 7 m) for bridge ends. Top at 0, 20 m tall.")
def plateau_small(b):
    b.rng.seed(11)
    _plateau(b, 7.0, 0.0, 20.0)
