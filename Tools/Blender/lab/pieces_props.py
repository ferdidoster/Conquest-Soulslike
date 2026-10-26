"""Furniture and storage: crates, lockers, beds, kitchen, bathroom, tables, shelves, workbench."""

import math

from . import palette as P
from . import detail as D
from .kit import piece


def legs(b, w, d, h, t=0.06, mat=P.METAL_DARK, inset=0.05):
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((t, t, h), (sx * (w / 2 - inset), sy * (d / 2 - inset), 0), mat)


# ---------------------------------------------------------------------------------- storage
@piece("Crate_Storage", "Storage", collider="box", box=((0, 0, 0.4), (1.2, 0.9, 0.8)),
       notes="Steel supply crate with ribs, latches and a label.")
def crate_storage(b):
    # the concept's steel case: thick chamfered body, raised lid, corner guards, side handles,
    # orange latches and a small label
    b.box((1.2, 0.9, 0.62), (0, 0, 0.04), P.GUNMETAL, bevel=0.05)
    b.box((1.24, 0.94, 0.06), (0, 0, 0.0), P.METAL_DARK, bevel=0.015)
    b.box((1.26, 0.96, 0.08), (0, 0, 0.6), P.METAL_DARK, bevel=0.02)
    b.box((1.14, 0.84, 0.1), (0, 0, 0.66), P.WALL, bevel=0.04)
    b.box((0.9, 0.6, 0.03), (0, 0, 0.76), P.GUNMETAL, bevel=0.01)
    D.corner_guards(b, 1.24, 0.94, 0.78, size=0.14)
    for x in (-0.3, 0.3):
        b.box((0.1, 0.05, 0.16), (x, -0.475, 0.5), P.HAZARD, bevel=0.01)
        b.box((0.05, 0.03, 0.08), (x, -0.5, 0.46), P.METAL_DARK)
    for s in (-1, 1):
        b.box((0.05, 0.3, 0.06), (s * 0.63, 0, 0.42), P.METAL_DARK)
        b.box((0.03, 0.3, 0.12), (s * 0.66, 0, 0.36), P.METAL)
    b.box((0.28, 0.02, 0.16), (0.05, -0.46, 0.22), P.MARKING)
    b.box((0.08, 0.02, 0.16), (-0.12, -0.461, 0.22), P.HAZARD)


@piece("Crate_Large", "Storage", collider="box", box=((0, 0, 0.7), (2.0, 1.4, 1.4)),
       notes="Large cargo container with hazard band and corner guards.")
def crate_large(b):
    # the concept's supply crate: weathered planks in a steel-strapped frame
    b.box((1.96, 1.36, 1.3), (0, 0, 0.04), P.WOOD_GREY, bevel=0.02)
    for i in range(5):
        z = 0.06 + i * 0.26
        b.box((1.98, 1.38, 0.02), (0, 0, z), P.METAL_DARK)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.16, 0.16, 1.42), (sx * 0.93, sy * 0.63, 0.0), P.WOOD_GREY, bevel=0.02)
            b.box((0.18, 0.18, 0.12), (sx * 0.93, sy * 0.63, 0.0), P.METAL_DARK, bevel=0.01)
            b.box((0.18, 0.18, 0.12), (sx * 0.93, sy * 0.63, 1.3), P.METAL_DARK, bevel=0.01)
    for z in (0.0, 1.3):
        for sy in (-1, 1):
            b.box((1.7, 0.12, 0.12), (0, sy * 0.66, z), P.WOOD_GREY, bevel=0.015)
        for sx in (-1, 1):
            b.box((0.12, 1.1, 0.12), (sx * 0.95, 0, z), P.WOOD_GREY, bevel=0.015)
    for x in (-0.45, 0.45):
        b.box((0.08, 1.4, 1.36), (x, 0, 0.02), P.METAL_DARK)
    b.box((1.7, 1.1, 0.04), (0, 0, 1.34), P.WOOD_GREY)
    b.box((0.5, 0.02, 0.28), (0.0, -0.69, 0.55), P.MARKING)


@piece("Crate_Stack", "Storage", fade=True, collider="box", box=((0, 0, 0.8), (2.4, 1.4, 1.6)),
       notes="Two crates and a small case stacked together.")
def crate_stack(b):
    crate_storage(b)
    b.box((1.0, 0.8, 0.7), (0.65, 0.15, 0.0), P.METAL_DARK, bevel=0.03)
    b.box((1.04, 0.84, 0.08), (0.65, 0.15, 0.62), P.HAZARD)
    b.box((0.8, 0.6, 0.45), (0.1, 0.05, 0.8), P.STEEL, rot_z=12, bevel=0.03)
    b.box((0.2, 0.04, 0.1), (0.1, -0.27, 1.0), P.METAL_DARK, rot_z=12)


@piece("Barrel", "Storage", collider="box", box=((0, 0, 0.5), (0.7, 0.7, 1.0)),
       notes="Chemical drum with rings and a warning band.")
def barrel(b):
    b.cylinder(0.33, 1.0, (0, 0, 0), P.FABRIC_BLUE, segments=10)
    for z in (0.15, 0.82):
        b.cylinder(0.35, 0.05, (0, 0, z), P.METAL_DARK, segments=10)
    b.cylinder(0.34, 0.16, (0, 0, 0.45), P.HAZARD, segments=10)
    b.cylinder(0.06, 0.04, (0.15, 0, 1.0), P.METAL_DARK, segments=6)


@piece("Shelf_Storage", "Storage", fade=True, collider="box", box=((0, 0, 1.2), (3.0, 0.7, 2.4)),
       notes="3 m industrial shelving with boxes and canisters (back against a wall).")
def shelf_storage(b):
    for x in (-1.45, 0.0, 1.45):
        for y in (-0.3, 0.3):
            b.box((0.07, 0.07, 2.4), (x, y, 0), P.METAL_DARK)
    rng = b.rng
    # packed like the concept's storage room: cardboard, steel cases and canisters on every level
    for z in (0.12, 0.9, 1.65, 2.35):
        b.box((3.0, 0.68, 0.05), (0, 0, z), P.METAL)
        x = -1.38
        while x < 1.25:
            w = rng.uniform(0.3, 0.65)
            h = rng.uniform(0.25, 0.6 if z < 2.3 else 0.4)
            kind = rng.random()
            if kind < 0.7:
                mat = rng.choice((P.CARDBOARD, P.CARDBOARD, P.METAL_DARK, P.WALL_LIGHT, P.FABRIC_BLUE))
                b.box((w, rng.uniform(0.4, 0.58), h), (x + w / 2, 0, z + 0.05), mat, bevel=0.01,
                      rot_z=rng.uniform(-6, 6))
            elif kind < 0.8:
                b.cylinder(0.12, h, (x + 0.15, 0, z + 0.05), rng.choice((P.HAZARD, P.STEEL, P.RUST)), segments=8)
                w = 0.3
            x += w + rng.uniform(0.05, 0.2)


@piece("Locker_Single", "Storage", fade=True, collider="box", box=((0, 0, 1.0), (0.6, 0.55, 2.0)),
       notes="Single 2 m steel locker with vents and a handle.")
def locker_single(b):
    locker(b, 0.0)


def locker(b, x, open_door=False, w=0.58):
    # the concept's locker: dark steel, recessed doors with louvres top and bottom, a handle
    b.box((w, 0.55, 1.98), (x, 0, 0.04), P.WALL_DARK, bevel=0.012)
    b.box((w + 0.02, 0.57, 0.06), (x, 0, 0), P.METAL_DARK)
    b.box((w + 0.02, 0.57, 0.05), (x, 0, 1.97), P.METAL_DARK)
    b.box((w - 0.08, 0.02, 1.8), (x, -0.28, 0.12), P.GUNMETAL, bevel=0.006)
    for z0 in (1.52, 0.2):
        b.box((w - 0.2, 0.02, 0.28), (x, -0.29, z0), P.METAL_DARK)
        for i in range(5):
            b.box((w - 0.24, 0.03, 0.025), (x, -0.3, z0 + 0.03 + i * 0.05), P.WALL_DARK, rot_x=-25)
    b.box((0.035, 0.05, 0.24), (x + w / 2 - 0.1, -0.31, 0.95), P.STEEL)
    b.box((0.14, 0.02, 0.07), (x - 0.08, -0.295, 1.88), P.MARKING)


@piece("Locker_Row", "Storage", fade=True, collider="box", box=((0, 0, 1.0), (1.85, 0.55, 2.0)),
       notes="Three lockers side by side, 1.8 m wide.")
def locker_row(b):
    for x in (-0.61, 0.0, 0.61):
        locker(b, x)
    b.box((1.86, 0.58, 0.06), (0, 0, 2.0), P.METAL_DARK)


# ---------------------------------------------------------------------------------- living quarters
@piece("Bed_Bunk", "Furniture", fade=True, collider="box", box=((0, 0, 0.95), (2.1, 1.0, 1.9)),
       notes="Steel bunk bed with two mattresses, pillows, blankets and a ladder.")
def bed_bunk(b):
    # the concept's bunk: dark tube frame, ladder at one end, grey mattresses, rolled blankets
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.cylinder(0.045, 1.95, (sx * 1.0, sy * 0.45, 0), P.METAL_DARK, segments=8)
            b.cylinder(0.06, 0.04, (sx * 1.0, sy * 0.45, 0), P.METAL_DARK, segments=8)
        for z in (0.32, 1.32, 1.9):
            b.cylinder(0.03, 0.9, (sx * 1.0, 0, z), P.METAL_DARK, segments=6, rot_x=90, base=False)
    for z in (0.32, 1.32):
        for sy in (-1, 1):
            b.cylinder(0.035, 2.0, (0, sy * 0.45, z), P.METAL_DARK, segments=6, rot_y=90, base=False)
        b.box((1.96, 0.86, 0.04), (0, 0, z), P.METAL)
        b.box((1.9, 0.8, 0.14), (0, 0, z + 0.04), P.MATTRESS, bevel=0.035)
        b.box((1.05, 0.84, 0.05), (0.4, 0, z + 0.17), P.FABRIC_BLUE, bevel=0.02)
        b.cylinder(0.09, 0.8, (-0.15, 0, z + 0.25), P.FABRIC_BLUE, segments=8, rot_x=90, base=False)
        b.box((0.4, 0.55, 0.1), (-0.7, 0, z + 0.18), P.PORCELAIN, bevel=0.04)
    for z in (1.9,):
        b.cylinder(0.03, 2.0, (0, 0.45, z), P.METAL_DARK, segments=6, rot_y=90, base=False)
    # ladder
    for y in (-0.42, -0.12):
        b.cylinder(0.025, 1.5, (-1.08, y, 0.25), P.METAL_DARK, segments=6)
    for z in (0.55, 0.85, 1.15, 1.45):
        b.cylinder(0.02, 0.32, (-1.08, -0.27, z), P.STEEL, segments=6, rot_x=90, base=False)


@piece("Bed_Medical", "Furniture", collider="box", box=((0, 0, 0.5), (2.1, 0.9, 1.0)),
       notes="Medical / examination bed with straps and a monitor arm.")
def bed_medical(b):
    legs(b, 1.9, 0.7, 0.7)
    b.box((2.0, 0.8, 0.12), (0, 0, 0.7), P.METAL)
    b.box((1.9, 0.72, 0.1), (0, 0, 0.82), P.PORCELAIN, bevel=0.03)
    for x in (-0.4, 0.5):
        b.box((0.12, 0.76, 0.03), (x, 0, 0.92), P.PLASTIC)
    b.box((0.06, 0.06, 1.1), (-1.0, 0.45, 0.7), P.METAL_DARK)
    b.box((0.4, 0.06, 0.3), (-0.85, 0.42, 1.75), P.PLASTIC)
    b.box((0.34, 0.03, 0.24), (-0.85, 0.39, 1.78), P.SCREEN)


@piece("Table_Dining", "Furniture", collider="box", box=((0, 0, 0.4), (3.0, 1.2, 0.8)),
       notes="3 m mess table with a steel frame.")
def table_dining(b):
    legs(b, 2.8, 1.0, 0.74, t=0.08, inset=0.1)
    b.box((3.0, 1.2, 0.06), (0, 0, 0.74), P.WOOD, bevel=0.01)
    b.box((2.6, 0.06, 0.08), (0, 0, 0.3), P.METAL_DARK)
    b.cylinder(0.05, 0.12, (0.4, 0.1, 0.8), P.STEEL, segments=6)
    b.box((0.3, 0.22, 0.02), (-0.6, -0.2, 0.8), P.PORCELAIN)


@piece("Chair", "Furniture", collider="box", box=((0, 0, 0.45), (0.5, 0.5, 0.9)),
       notes="Simple steel chair, seat facing -Z (Unity).")
def chair(b):
    legs(b, 0.44, 0.44, 0.45, t=0.04, inset=0.03)
    b.box((0.48, 0.46, 0.05), (0, 0, 0.45), P.FABRIC_BLUE)
    b.box((0.46, 0.05, 0.45), (0, 0.21, 0.5), P.METAL_DARK)


@piece("Sofa", "Furniture", collider="box", box=((0, 0, 0.45), (2.2, 0.95, 0.9)),
       notes="Worn red three-seat sofa (from the kitchen / living concept).")
def sofa(b):
    b.box((2.2, 0.9, 0.42), (0, 0, 0.06), P.FABRIC, bevel=0.04)
    b.box((2.2, 0.25, 0.85), (0, 0.33, 0.06), P.FABRIC, bevel=0.05)
    for s in (-1, 1):
        b.box((0.22, 0.9, 0.65), (s * 1.0, 0, 0.06), P.FABRIC, bevel=0.04)
    for x in (-0.6, 0.0, 0.6):
        b.box((0.58, 0.62, 0.12), (x, -0.08, 0.48), P.FABRIC, bevel=0.03)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.06, 0.06, 0.06), (sx * 1.0, sy * 0.38, 0), P.METAL_DARK)
    b.box((0.4, 0.15, 0.32), (0.75, 0.15, 0.6), P.FABRIC_BLUE, rot_x=-15, rot_z=-10)


@piece("Kitchen_Counter", "Furniture", fade=True, collider="box", box=((0, 0, 1.0), (3.0, 0.7, 2.0)),
       lights=[{"offset": (0.2, -0.6, 1.6), "color": "#FFB070", "range": 4.0, "intensity": 1.4, "flicker": 0.08}],
       notes="3 m kitchen run: cabinets, a sink, a stove, wall cupboards (back against a wall).")
def kitchen_counter(b):
    b.box((3.0, 0.65, 0.86), (0, 0, 0), P.WALL_LIGHT, bevel=0.01)
    b.box((3.04, 0.7, 0.05), (0, -0.02, 0.86), P.STEEL)
    for i in range(5):
        x = -1.2 + i * 0.6
        b.box((0.55, 0.02, 0.7), (x, -0.33, 0.08), P.WALL)
        b.box((0.16, 0.03, 0.03), (x, -0.35, 0.68), P.METAL_DARK)
    b.box((0.6, 0.42, 0.04), (-0.7, -0.03, 0.88), P.METAL_DARK)
    b.box((0.04, 0.04, 0.3), (-0.7, 0.2, 0.9), P.STEEL)
    for x in (0.55, 0.85):
        for y in (-0.15, 0.12):
            b.cylinder(0.1, 0.02, (x, y, 0.91), P.PLASTIC, segments=8)
    b.box((3.0, 0.38, 0.7), (0, 0.13, 1.6), P.WALL_LIGHT)
    for i in range(5):
        b.box((0.55, 0.02, 0.6), (-1.2 + i * 0.6, -0.07, 1.65), P.WALL)
    b.box((0.8, 0.05, 0.06), (0.2, -0.08, 1.56), P.LAMP_WARM)


@piece("Fridge", "Furniture", fade=True, collider="box", box=((0, 0, 1.0), (0.8, 0.75, 2.0)),
       notes="Tall steel fridge with two doors.")
def fridge(b):
    b.box((0.8, 0.72, 1.95), (0, 0, 0.05), P.STEEL, bevel=0.03)
    b.box((0.82, 0.04, 0.03), (0, -0.37, 1.35), P.METAL_DARK)
    b.box((0.04, 0.05, 0.5), (0.32, -0.39, 0.7), P.METAL_DARK)
    b.box((0.04, 0.05, 0.3), (0.32, -0.39, 1.5), P.METAL_DARK)
    b.box((0.06, 0.02, 0.06), (-0.3, -0.37, 1.85), P.LIGHT_GREEN)


# ---------------------------------------------------------------------------------- bathroom
@piece("Shower_Stall", "Furniture", fade=True, collider="mesh",
       notes="1.4 m tiled shower cubicle with partitions, shower head and drain (open side faces -Z in Unity).")
def shower_stall(b):
    b.box((1.4, 0.1, 2.3), (0, 0.65, 0), P.TILE)
    for s in (-1, 1):
        b.box((0.06, 1.4, 2.1), (s * 0.7, 0, 0.1), P.PORCELAIN)
        b.box((0.08, 0.08, 2.3), (s * 0.7, -0.7, 0), P.METAL_DARK)
    b.box((1.36, 1.3, 0.04), (0, 0, 0), P.TILE)
    b.cylinder(0.08, 0.02, (0, 0, 0.04), P.METAL_DARK, segments=8)
    b.box((0.04, 0.3, 0.04), (0, 0.48, 2.0), P.STEEL)
    b.cylinder(0.1, 0.05, (0, 0.33, 1.95), P.STEEL, segments=8)
    b.box((0.08, 0.04, 0.12), (0.2, 0.58, 1.2), P.STEEL)
    b.box((1.4, 0.05, 0.04), (0, -0.7, 2.0), P.STEEL)


@piece("Sink_Row", "Furniture", collider="box", box=((0, 0, 0.5), (2.6, 0.6, 1.0)),
       notes="Three wash basins on a counter with a long mirror (back against a wall).")
def sink_row(b):
    b.box((2.6, 0.55, 0.12), (0, 0, 0.8), P.PORCELAIN)
    for x in (-1.2, 1.2):
        b.box((0.08, 0.5, 0.8), (x, 0, 0), P.METAL_DARK)
    for x in (-0.85, 0.0, 0.85):
        b.box((0.5, 0.36, 0.06), (x, -0.04, 0.88), P.TILE)
        b.box((0.42, 0.28, 0.02), (x, -0.04, 0.93), P.METAL_DARK)
        b.box((0.04, 0.12, 0.2), (x, 0.17, 0.92), P.STEEL)
    b.box((2.5, 0.04, 0.8), (0, 0.26, 1.3), P.GLOW_SOFT)
    b.box((2.6, 0.03, 0.9), (0, 0.29, 1.25), P.METAL_DARK)


@piece("Toilet_Stall", "Furniture", fade=True, collider="mesh",
       notes="Toilet cubicle with partitions and a half-open door (open side faces -Z in Unity).")
def toilet_stall(b):
    for s in (-1, 1):
        b.box((0.05, 1.5, 2.0), (s * 0.55, 0, 0.15), P.WALL_LIGHT)
    b.box((1.1, 0.05, 2.15), (0, 0.75, 0), P.TILE)
    b.box((0.38, 0.5, 0.42), (0, 0.4, 0), P.PORCELAIN, bevel=0.04)
    b.box((0.4, 0.2, 0.4), (0, 0.65, 0.4), P.PORCELAIN)
    b.box((0.6, 0.04, 1.8), (-0.3, -0.85, 0.15), P.WALL, rot_z=-35)


# ---------------------------------------------------------------------------------- lab and workshop
@piece("Lab_Table", "Furniture", collider="box", box=((0, 0, 0.5), (2.0, 0.9, 1.0)),
       notes="Lab bench with drawers, sample jars, a microscope and a tablet.")
def lab_table(b):
    # the concept's lab table: pale worn top, drawer pedestal left, cabinet right, legs on casters
    b.box((2.0, 0.85, 0.06), (0, 0, 0.86), P.MATTRESS, bevel=0.015)
    b.box((2.04, 0.89, 0.03), (0, 0, 0.84), P.METAL_DARK)
    b.box((0.55, 0.75, 0.74), (-0.68, 0, 0.1), P.WALL_DARK, bevel=0.01)
    for i in range(3):
        b.box((0.49, 0.02, 0.21), (-0.68, -0.38, 0.15 + i * 0.23), P.GUNMETAL, bevel=0.005)
        b.box((0.14, 0.03, 0.025), (-0.68, -0.4, 0.3 + i * 0.23), P.STEEL)
    b.box((0.6, 0.75, 0.74), (0.66, 0, 0.1), P.WALL_DARK, bevel=0.01)
    for x in (0.52, 0.8):
        b.box((0.26, 0.02, 0.62), (x, -0.38, 0.15), P.GUNMETAL, bevel=0.005)
    for x in (0.6, 0.72):
        b.box((0.025, 0.03, 0.14), (x, -0.4, 0.5), P.STEEL)
    b.box((0.7, 0.7, 0.12), (0, 0, 0.7), P.WALL_DARK, bevel=0.01)
    b.box((0.2, 0.03, 0.025), (0, -0.36, 0.76), P.STEEL)
    for x in (-0.68, 0.66):
        for sy in (-1, 1):
            for sx in (-1, 1):
                b.box((0.05, 0.05, 0.08), (x + sx * 0.22, sy * 0.3, 0.02), P.METAL_DARK)
                b.cylinder(0.04, 0.04, (x + sx * 0.22, sy * 0.3 - 0.02, 0.0), P.PLASTIC, segments=8,
                           rot_y=90, base=False)
    for i, x in enumerate((0.2, 0.32, 0.44)):
        b.cylinder(0.05, 0.16 + i * 0.03, (x, 0.22, 0.92), P.GLOW_SOFT if i != 1 else P.LIGHT_GREEN, segments=8)
    b.box((0.4, 0.28, 0.02), (-0.3, -0.1, 0.92), P.PLASTIC)
    b.box((0.34, 0.22, 0.01), (-0.3, -0.1, 0.94), P.SCREEN)


@piece("Workbench", "Furniture", collider="box", box=((0, 0, 0.5), (2.4, 0.9, 1.0)),
       notes="Heavy workbench with a vice, tools, a pegboard and a work lamp (back against a wall).")
def workbench(b):
    b.box((2.4, 0.85, 0.1), (0, 0, 0.85), P.WOOD)
    legs(b, 2.3, 0.75, 0.85, t=0.1, mat=P.METAL_DARK)
    b.box((2.2, 0.7, 0.05), (0, 0, 0.25), P.METAL_DARK)
    b.box((2.4, 0.05, 1.2), (0, 0.43, 0.95), P.WALL_DARK)
    for i in range(6):
        b.box((0.06, 0.03, 0.35), (-0.9 + i * 0.35, 0.39, 1.35), P.STEEL, rot_y=(i * 25) % 40 - 20)
    b.box((0.25, 0.3, 0.2), (-0.9, -0.15, 0.95), P.METAL_DARK)
    b.box((0.35, 0.25, 0.12), (0.3, 0.0, 0.95), P.RUST)
    b.box((0.5, 0.3, 0.25), (0.75, 0.05, 0.95), P.HAZARD, bevel=0.02)
    b.cylinder(0.12, 0.08, (-0.3, -0.1, 0.95), P.METAL, segments=8)


@piece("Med_Cabinet", "Furniture", fade=True, collider="box", box=((0, 0, 1.0), (1.2, 0.5, 2.0)),
       notes="Tall glass-front cabinet with glowing sample vials (back against a wall).")
def med_cabinet(b):
    b.box((1.2, 0.48, 2.0), (0, 0, 0), P.WALL_LIGHT, bevel=0.02)
    b.box((1.04, 0.04, 1.2), (0, -0.23, 0.7), P.WALL_DARK)
    for z in (0.95, 1.35, 1.75):
        b.box((1.0, 0.36, 0.03), (0, -0.04, z - 0.05), P.METAL)
        for k in range(6):
            b.cylinder(0.04, 0.16, (-0.4 + k * 0.16, -0.15, z), P.GLOW_SOFT if k % 3 else P.LIGHT_GREEN, segments=6)
    b.box((1.0, 0.04, 0.5), (0, -0.24, 0.1), P.WALL)
