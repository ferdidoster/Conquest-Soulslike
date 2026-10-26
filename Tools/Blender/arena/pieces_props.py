"""Braziers, banners, awnings and ruins."""

import math

from . import palette as P
from .geo import sag_curve
from .kit import piece
from .pieces_architecture import _banner

FIRE = {"color": "#FFA34A", "range": 6.5, "intensity": 3.0}


@piece("Brazier_Standing", "Props", collider="box", box=((0, 0, 0.6), (0.8, 0.8, 1.2)),
       light=dict(FIRE, offset=(0, 0, 1.8)), notes="Stone pedestal with a bronze fire bowl (1.3 m).")
def brazier_standing(b):
    b.box((0.8, 0.8, 0.25), (0, 0, 0), P.STONE_DARK, bevel=0.03)
    b.box((0.6, 0.6, 0.75), (0, 0, 0.25), P.STONE, taper=0.8, bevel=0.03)
    b.box((0.7, 0.7, 0.12), (0, 0, 1.0), P.STONE_LIGHT, bevel=0.02)
    b.cylinder(0.3, 0.32, (0, 0, 1.1), P.BRONZE, segments=8, radius_top=0.58)
    b.cylinder(0.5, 0.04, (0, 0, 1.36), P.VOID, segments=8)
    b.flame((0, 0, 1.38), 1.0)


@piece("Brazier_Bowl", "Props", collider="box", box=((0, 0, 0.5), (1.4, 1.4, 1.0)),
       light=dict(FIRE, offset=(0, 0, 1.5)), notes="Wide bronze bowl on a low iron stand.")
def brazier_bowl(b):
    for k in range(3):
        a = k * 120
        rad = math.radians(a)
        b.box((0.12, 0.12, 0.85), (math.cos(rad) * 0.45, math.sin(rad) * 0.45, 0), P.METAL_DARK, rot_z=a,
              rot_y=-12)
    b.cylinder(0.55, 0.06, (0, 0, 0.35), P.METAL_DARK, segments=6)
    b.cylinder(0.35, 0.35, (0, 0, 0.75), P.BRONZE, segments=10, radius_top=0.8)
    b.cylinder(0.85, 0.06, (0, 0, 1.08), P.BRONZE, segments=10)
    b.cylinder(0.72, 0.03, (0, 0, 1.1), P.VOID, segments=10)
    b.flame((0, 0, 1.1), 1.3)


@piece("Banner_Hanging", "Cloth", collider="none",
       notes="1.2 x 4.5 m banner on a rod. Pivot = rod centre, hangs down; front faces -Y (Unity +Z).")
def banner_hanging(b):
    _banner(b, width=1.2, top=0.0, bottom=-4.5, y=0.0, cols=4, rows=6)


@piece("Banner_Small", "Cloth", collider="none",
       notes="0.5 x 1 m pennant for wall faces. Pivot = rod centre.")
def banner_small(b):
    _banner(b, width=0.5, top=0.0, bottom=-1.0, y=0.0, cols=2, rows=3)


@piece("Awning_Span_12m", "Cloth", collider="none",
       notes="Sagging red cloth roped between two points 12 m apart along X. Pivot = midpoint of the ropes; "
             "put it at the height of the pillar tops.")
def awning_span(b):
    span, width, sag, cols = 12.0, 2.6, 1.7, 14
    grid, sway = [], []
    for r, y in enumerate((-width / 2, 0.0, width / 2)):
        row, srow = [], []
        for c in range(cols + 1):
            t = c / cols
            x = (t - 0.5) * span
            edge = 0.25 if r != 1 else 0.0
            z = -sag * 4 * t * (1 - t) - edge * 4 * t * (1 - t)
            row.append((x, y, z))
            srow.append(4 * t * (1 - t))
        grid.append(row)
        sway.append(srow)
    b.sheet(grid, P.CLOTH, sway)
    for y in (-width / 2, width / 2):
        b.tube(sag_curve((-span / 2 - 0.2, y, 0.05), (span / 2 + 0.2, y, 0.05), sag + 0.3, 12), 0.03,
               P.CLOTH_DARK, sides=4)


@piece("Block_Ruin_A", "Props", notes="Toppled stack of big masonry blocks (about 3 x 2 x 2 m).")
def block_ruin_a(b):
    b.box((1.8, 1.4, 1.1), (0, 0, 0), P.STONE, bevel=0.05, rot_z=8)
    b.box((1.4, 1.2, 0.9), (0.15, 0.1, 1.1), P.STONE_LIGHT, bevel=0.05, rot_z=-12)
    b.box((1.2, 1.0, 1.0), (1.5, -0.6, 0), P.STONE, bevel=0.05, rot_z=30, rot_x=-14)
    b.box((0.6, 0.5, 0.4), (-1.1, 0.7, 0), P.STONE_DARK, bevel=0.03, rot_z=50)


@piece("Block_Ruin_B", "Props", notes="Broken 3.5 m wall fragment with a crumbled end.")
def block_ruin_b(b):
    for i in range(4):
        b.box((0.85, 0.7, 0.6), (-1.35 + i * 0.9, 0, 0), P.STONE, bevel=0.03)
    for i in range(3):
        b.box((0.85, 0.7, 0.6), (-0.9 + i * 0.9, 0, 0.6), P.STONE_LIGHT, bevel=0.03)
    b.box((0.85, 0.7, 0.6), (-0.45, 0, 1.2), P.STONE, bevel=0.03)
    b.box((0.5, 0.5, 0.4), (1.9, 0.5, 0), P.STONE, bevel=0.03, rot_z=25)
    b.box((0.4, 0.4, 0.3), (2.3, -0.4, 0), P.STONE_DARK, bevel=0.03, rot_z=-15)


@piece("Rubble_Pile", "Props", collider="none", notes="Scatter of small stone chunks (about 2 m across).")
def rubble_pile(b):
    rng = b.rng
    for _ in range(10):
        s = rng.uniform(0.18, 0.45)
        b.box((s * rng.uniform(0.8, 1.5), s, s * rng.uniform(0.6, 1.0)),
              (rng.uniform(-1, 1), rng.uniform(-1, 1), -0.03), rng.choice((P.STONE, P.STONE_LIGHT, P.ROCK)),
              rot_z=rng.uniform(0, 90), rot_x=rng.uniform(-15, 15), bevel=0.02)
