"""Scavenged machinery: the 'tech-riddled' half of the desert."""

import math

from mathutils import Matrix

from . import palette as P
from .geo import sag_curve
from .kit import piece


@piece("Tech_Vaporator", "Tech", collider="box", box=((0, 0, 1.7), (1.0, 1.0, 3.4)),
       notes="Moisture-farm tower (3.6 m): ringed column, fins and a status light.")
def tech_vaporator(b):
    b.box((0.9, 0.9, 0.35), (0, 0, 0), P.METAL_DARK, bevel=0.03)
    b.cylinder(0.33, 3.0, (0, 0, 0.35), P.METAL, segments=8)
    for z in (1.0, 1.9, 2.7):
        b.cylinder(0.43, 0.14, (0, 0, z), P.METAL_DARK, segments=8)
    b.cylinder(0.36, 0.45, (0, 0, 3.35), P.METAL_DARK, segments=8, radius_top=0.08)
    for k in range(3):
        a = k * 120 + 30
        rad = math.radians(a)
        b.box((0.06, 0.55, 0.9), (math.cos(rad) * 0.45, math.sin(rad) * 0.45, 2.2), P.METAL, rot_z=a + 90)
    b.box((0.12, 0.05, 0.12), (0, -0.34, 1.45), P.TECH_LIGHT)
    b.box((0.35, 0.25, 0.4), (0.3, -0.35, 0.35), P.RUST, bevel=0.02)


@piece("Tech_Antenna", "Tech", collider="box", box=((0, 0, 1.5), (1.6, 1.6, 3.0)),
       notes="9 m lattice mast with a dish and a warning light.")
def tech_antenna(b):
    h, base, top = 9.0, 0.8, 0.25
    corners = [(1, 1), (-1, 1), (-1, -1), (1, -1)]
    for cx, cy in corners:
        b.tube([(cx * base, cy * base, 0), (cx * top, cy * top, h)], 0.06, P.METAL_DARK, sides=4)
    for i in range(6):
        z0, z1 = i * h / 6, (i + 1) * h / 6
        w0 = base + (top - base) * z0 / h
        w1 = base + (top - base) * z1 / h
        for k in range(4):
            (ax, ay), (bx, by) = corners[k], corners[(k + 1) % 4]
            b.tube([(ax * w0, ay * w0, z0), (bx * w1, by * w1, z1)], 0.025, P.METAL, sides=3)
    b.box((1.3, 1.3, 0.4), (0, 0, 0), P.STONE_DARK, bevel=0.03)
    b.cylinder(0.05, 1.2, (0, 0, h), P.METAL_DARK, segments=4)
    b.box((0.12, 0.12, 0.12), (0, 0, h + 1.2), P.TECH_LIGHT)
    m = Matrix.Translation((0.3, -0.4, h - 1.2)) @ Matrix.Rotation(math.radians(-55), 4, "X")
    b.cone(1.1, 0.25, 0.4, m, P.METAL)


@piece("Tech_Generator", "Tech", collider="box", box=((0, 0, 0.75), (2.6, 1.6, 1.5)),
       notes="Boxy power unit with a fuel tank, vents, hoses into the ground and a status panel.")
def tech_generator(b):
    b.box((2.2, 1.3, 1.2), (0, 0, 0), P.METAL_DARK, bevel=0.05)
    for i in range(5):
        b.box((0.08, 1.1, 0.1), (-0.8 + i * 0.4, 0, 1.2), P.METAL)
    b.cylinder(0.42, 2.0, (-1.0, 0.55, 0.55), P.METAL, segments=8, rot_y=90, base=False)
    b.box((0.6, 0.06, 0.4), (0.6, -0.66, 0.6), P.METAL)
    for i in range(3):
        b.box((0.1, 0.04, 0.08), (0.42 + i * 0.18, -0.7, 0.68), P.TECH_LIGHT if i != 1 else P.METAL_DARK)
    b.box((0.7, 0.05, 0.6), (-0.6, -0.66, 0.3), P.RUST)
    b.tube([(1.1, 0.2, 0.4), (1.5, 0.3, 0.3), (1.8, 0.1, -0.05)], 0.08, P.METAL_DARK)
    b.tube([(1.1, -0.3, 0.7), (1.6, -0.5, 0.5), (2.1, -0.4, -0.05)], 0.06, P.METAL_DARK)


@piece("Tech_PipeRun", "Tech", collider="box", box=((0, 0, 0.6), (1.0, 6.0, 1.2)),
       notes="6 m of twin pipes on stands along Y; chain them end to end.")
def tech_pipe_run(b):
    for y in (-2.5, 0.0, 2.5):
        b.box((0.9, 0.25, 1.1), (0, y, 0), P.METAL_DARK, taper=0.8)
    for x, z, r in ((-0.22, 0.75, 0.17), (0.22, 0.95, 0.13)):
        b.cylinder(r, 6.0, (x, -3.0, z), P.METAL if r > 0.15 else P.RUST, segments=8, rot_x=-90)
        for y in (-1.5, 1.5):
            b.cylinder(r + 0.07, 0.12, (x, y, z), P.METAL_DARK, segments=8, rot_x=-90)


@piece("Tech_Crate_Stack", "Tech", notes="Three salvage crates with banding.")
def tech_crate_stack(b):
    b.box((1.4, 0.9, 0.9), (0, 0, 0), P.METAL, bevel=0.04)
    b.box((1.46, 0.96, 0.12), (0, 0, 0.6), P.METAL_DARK)
    b.box((0.9, 0.9, 0.9), (1.25, 0.2, 0), P.RUST, bevel=0.04, rot_z=12)
    b.box((0.8, 0.7, 0.6), (0.1, 0.05, 0.9), P.METAL_DARK, bevel=0.03, rot_z=-8)
    b.box((0.2, 0.04, 0.1), (0.1, -0.32, 1.25), P.TECH_LIGHT)


@piece("Tech_Console", "Tech", collider="box", box=((0, 0, 0.55), (0.9, 0.7, 1.1)),
       notes="Waist-high control post with a slanted, lit panel.")
def tech_console(b):
    b.box((0.7, 0.5, 0.9), (0, 0, 0), P.METAL_DARK, taper=0.85, bevel=0.03)
    b.box((0.8, 0.5, 0.12), (0, -0.05, 0.92), P.METAL, rot_x=-25, bevel=0.02)
    for i in range(3):
        b.box((0.1, 0.06, 0.05), (-0.2 + i * 0.2, -0.12, 1.0), P.TECH_LIGHT, rot_x=-25)


@piece("Tech_Pole", "Tech", collider="box", box=((0, 0, 1.7), (0.4, 0.4, 3.4)),
       notes="3.4 m cable pole; string Tech_Cable_Span between two of them (8 m apart).")
def tech_pole(b):
    b.cylinder(0.09, 3.4, (0, 0, 0), P.METAL_DARK, segments=6)
    b.box((0.9, 0.1, 0.1), (0, 0, 3.15), P.METAL)
    for x in (-0.4, 0.4):
        b.cylinder(0.05, 0.15, (x, 0, 3.2), P.BONE, segments=5)
    b.box((0.35, 0.35, 0.25), (0, 0, 0), P.STONE_DARK)


@piece("Tech_Cable_Span", "Tech", collider="none",
       notes="Two sagging cables 8 m long along X; pivot = midpoint at the attachment height.")
def tech_cable_span(b):
    for x in (-0.4, 0.4):
        b.tube(sag_curve((-4.0, x, 0.0), (4.0, x, 0.0), 0.9, 10), 0.03, P.METAL_DARK, sides=4)


@piece("Tech_Wreck_Hull", "Tech",
       notes="Half-buried hull section of a crashed ship (10 m long) with exposed ribs.")
def tech_wreck_hull(b):
    tilt = Matrix.Translation((0, 0, -1.2)) @ Matrix.Rotation(math.radians(12), 4, "Y") \
        @ Matrix.Rotation(math.radians(90), 4, "X")
    # shell: a ring segment around Z, turned so the hull runs along Y
    for i, (z0, z1, mat) in enumerate(((-5, -1.5, P.METAL), (-1.5, 0.5, P.RUST), (0.5, 5, P.METAL))):
        b.ring_segment(3.6, 3.85, z0, z1, 15 + i * 6, 160 - i * 10, mat, segments=8, matrix=tilt)
    for z in (-4.0, -1.5, 1.0, 3.5):
        b.ring_segment(3.5, 3.95, z, z + 0.35, 5, 175, P.METAL_DARK, segments=10, matrix=tilt)
    b.box((1.2, 1.0, 0.8), (2.8, 3.5, 0), P.METAL_DARK, rot_z=20, bevel=0.03)
    b.box((0.8, 0.5, 0.5), (-3.2, -3.0, 0), P.RUST, rot_z=-30, bevel=0.03)


@piece("Tech_Wreck_Engine", "Tech", notes="Crashed engine pod (7 m) nose-down in the sand.")
def tech_wreck_engine(b):
    tilt = Matrix.Translation((0, 0, 0.9)) @ Matrix.Rotation(math.radians(-14), 4, "X")
    along_y = Matrix.Rotation(math.radians(-90), 4, "X")
    b.cone(1.9, 1.9, 5.5, tilt @ along_y, P.METAL)
    for y in (-2.0, 0.0, 2.0):
        b.cone(2.05, 2.05, 0.3, tilt @ Matrix.Translation((0, y, 0)) @ along_y, P.METAL_DARK)
    b.cone(1.6, 2.3, 1.4, tilt @ Matrix.Translation((0, 3.4, 0)) @ along_y, P.RUST)
    b.cone(1.5, 1.5, 0.1, tilt @ Matrix.Translation((0, 4.12, 0)) @ along_y, P.VOID)
    b.box((0.4, 2.5, 0.3), (0, -1.0, 2.7), P.METAL_DARK, rot_x=-14)


@piece("Ship_SkyBarge", "Backdrop", collider="none", hover=True,
       notes="Floating barge for the sky (about 30 m long). Bobs gently in Unity.")
def ship_sky_barge(b):
    hull = []
    for k in range(12):
        a = 2 * math.pi * k / 12
        hull.append((math.cos(a) * 15.0, math.sin(a) * 6.0 * (1.0 if math.cos(a) < 0.5 else 0.7)))
    b.prism(hull, -1.5, 1.2, P.METAL, top_scale=0.85)
    b.prism([(x * 0.85, y * 0.85) for x, y in hull], 1.2, 1.6, P.METAL_DARK, top_scale=0.92)
    b.box((8, 4.5, 2.2), (-2, 0, 1.5), P.STONE_LIGHT, taper=0.85, bevel=0.1)
    b.box((3.5, 3, 2.0), (-3, 0, 3.7), P.METAL, taper=0.7)
    b.cylinder(0.12, 3.5, (-3, 0, 5.7), P.METAL_DARK, segments=4)
    rng = b.rng
    for i in range(7):
        x = -9 + i * 3 + rng.uniform(-0.5, 0.5)
        y = rng.uniform(-2.0, 2.0)
        length = rng.uniform(3.0, 8.0)
        # hanging spires under the hull, as in the concept
        b.cylinder(0.05, length, (x, y, -1.5 - length), P.METAL_DARK, segments=5, radius_top=0.9 * length / 8 + 0.3)
    for y in (-3.5, 3.5):
        b.cylinder(0.9, 3.0, (10.5, y, 0.2), P.METAL_DARK, segments=8, rot_y=90, base=False)
        b.cylinder(0.6, 0.1, (12.05, y, 0.2), P.TECH_LIGHT, segments=8, rot_y=90, base=False)
