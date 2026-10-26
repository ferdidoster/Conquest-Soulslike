"""Rocks, mesas, spires and the canyon floor."""

import math

from . import palette as P
from .kit import piece


@piece("Rock_Boulder_S", "Nature", collider="none", notes="Knee-high boulder (r = 0.6 m).")
def rock_boulder_s(b):
    b.boulder(0.6, mat=P.ROCK_LIGHT)


@piece("Rock_Boulder_M", "Nature", notes="Large boulder (r = 1.5 m).")
def rock_boulder_m(b):
    b.boulder(1.5, mat=P.ROCK, detail=1)


@piece("Rock_Cluster", "Nature", notes="Group of three boulders with a flat slab, about 4 m across.")
def rock_cluster(b):
    b.boulder(1.2, (0, 0, 0), P.ROCK)
    b.boulder(0.8, (1.4, 0.6, 0), P.ROCK_LIGHT)
    b.boulder(0.5, (-1.2, -0.7, 0), P.ROCK_DARK)
    b.box((1.6, 1.1, 0.35), (0.4, -1.3, 0), P.ROCK_LIGHT, rot_z=20, rot_x=6)


@piece("Rock_Mesa_A", "Nature", notes="Layered mesa (r = 9 m, 26 m tall). Stand it on the canyon floor.")
def rock_mesa_a(b):
    b.rng.seed(21)
    b.strata_rock(9.0, 26.0, layers=6, taper=0.6, mat=P.ROCK, accent=P.ROCK_LIGHT, sides=9)


@piece("Rock_Mesa_B", "Nature", notes="Wide flat-topped mesa (r = 14 m, 18 m tall).")
def rock_mesa_b(b):
    b.rng.seed(22)
    b.strata_rock(14.0, 18.0, layers=4, taper=0.82, mat=P.ROCK_DARK, accent=P.ROCK, sides=11, jitter=0.12)


@piece("Rock_Spire_A", "Nature", notes="Tall needle spire (r = 4 m, 36 m tall).")
def rock_spire_a(b):
    b.rng.seed(23)
    b.strata_rock(4.5, 36.0, layers=8, taper=0.35, mat=P.ROCK, accent=P.ROCK_DARK, sides=7, ledge=0.95)


@piece("Rock_Spire_B", "Nature", notes="Twin-stepped spire (r = 3.5 m, 24 m tall).")
def rock_spire_b(b):
    b.rng.seed(24)
    b.strata_rock(3.5, 15.0, layers=4, taper=0.6, mat=P.ROCK_LIGHT, accent=P.ROCK, sides=7)
    b.strata_rock(1.8, 9.0, layers=3, taper=0.5, loc=(0.6, 0.3, 0), z_start=15.0, mat=P.ROCK, sides=6)


@piece("Rock_Arch", "Nature", notes="Natural stone arch, 22 m wide and 20 m tall.")
def rock_arch(b):
    b.rng.seed(25)
    b.strata_rock(3.5, 16.0, layers=4, taper=0.7, loc=(-9, 0, 0), mat=P.ROCK, accent=P.ROCK_LIGHT, sides=7)
    b.strata_rock(3.2, 16.0, layers=4, taper=0.7, loc=(9, 0, 0), mat=P.ROCK, accent=P.ROCK_LIGHT, sides=7)
    pts = []
    for k in range(9):
        t = k / 8
        x = -11 + 22 * t
        pts.append((x, 15.0 + 3.0 * math.sin(t * math.pi)))
    for k in range(8):
        (x0, z0), (x1, z1) = pts[k], pts[k + 1]
        b.box((x1 - x0 + 0.3, 3.2 - abs(k - 3.5) * 0.15, 3.0), ((x0 + x1) / 2, 0, (z0 + z1) / 2 - 1.2),
              P.ROCK_DARK if k % 3 == 0 else P.ROCK, rot_y=-math.degrees(math.atan2(z1 - z0, x1 - x0)))


@piece("Ground_Canyon", "Nature",
       notes="320 x 320 m canyon floor with low dunes. Place it 20 m below the plateau top.")
def ground_canyon(b):
    rng = b.rng
    rng.seed(31)
    n, size = 20, 320.0
    heights = [[0.0] * (n + 1) for _ in range(n + 1)]
    for i in range(n + 1):
        for j in range(n + 1):
            x, y = (i / n - 0.5) * size, (j / n - 0.5) * size
            heights[i][j] = (math.sin(x * 0.045) * math.cos(y * 0.03) * 1.2
                             + math.sin((x + y) * 0.08) * 0.4 + rng.uniform(-0.25, 0.25))
    mark = b._begin()
    verts = [[b.bm.verts.new(((i / n - 0.5) * size, (j / n - 0.5) * size, heights[i][j]))
              for j in range(n + 1)] for i in range(n + 1)]
    for i in range(n):
        for j in range(n):
            b.bm.faces.new((verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]))
    b._end(mark, P.SAND_DEEP)


@piece("Rock_Cliff_Wall", "Nature", collider="none",
       notes="48 m stretch of canyon wall, 14 m deep and 24 to 38 m tall: vertical rock columns split by dark "
             "clefts, with jutting strata ledges. Front faces -Y (Unity +Z). Ring them around a level to close "
             "off the horizon.")
def rock_cliff_wall(b):
    rng = b.rng
    rng.seed(41)
    width, depth = 48.0, 14.0
    bands = (P.ROCK, P.ROCK_LIGHT, P.ROCK, P.ROCK, P.ROCK_LIGHT)
    # dark back slab fills the clefts between columns
    b.box((width, depth * 0.5, 20.0), (0, depth * 0.2, 0), P.ROCK_DARK, taper=0.96)
    x = -width / 2
    while x < width / 2 - 2.0:
        w = min(rng.uniform(4.5, 8.5), width / 2 - x)
        top = rng.uniform(24.0, 38.0)
        layers = rng.randint(3, 5)
        front = -depth / 2 + rng.uniform(-2.5, 2.0)
        z, setback, offset = 0.0, 0.0, rng.randint(0, 4)
        x0, x1 = x + 0.35, x + w - 0.35  # the gap is the cleft
        for i in range(layers):
            last = i == layers - 1
            h = max(top - z, 2.0) if last else top / layers * rng.uniform(0.85, 1.15)
            y0 = front + setback
            pts = [(x0, y0 + rng.uniform(-0.3, 0.3)),
                   (x0 + (x1 - x0) * rng.uniform(0.35, 0.65), y0 - rng.uniform(0.2, 1.0)),
                   (x1, y0 + rng.uniform(-0.3, 0.3)),
                   (x1, depth / 2),
                   (x0, depth / 2)]
            b.prism(pts, z, z + h + 0.02, bands[(i + offset) % len(bands)], top_scale=0.94 if last else 1.0)
            z += h
            if not last:
                # a jutting ledge of harder rock marks the strata line
                b.box((x1 - x0 + 0.6, 1.0, 0.7), ((x0 + x1) / 2, y0 + 0.2, z - 0.35), P.ROCK_LIGHT,
                      rot_z=rng.uniform(-3, 3))
            setback += rng.uniform(0.4, 1.4)
            x0 += rng.uniform(0.0, 0.4)
            x1 -= rng.uniform(0.0, 0.4)
        x += w
