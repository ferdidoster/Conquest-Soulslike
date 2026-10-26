"""Lab machinery: pipes, cables, lights, consoles, the containment tubes and the hub reactor."""

import math

from mathutils import Matrix

from . import palette as P
from . import detail as D
from .geo import sag_curve
from .kit import piece

COLD = "#8FD0FF"
WARM = "#FF8A3A"
CYAN = "#3FC8FF"


# ---------------------------------------------------------------------------------- pipes and cables
def flange(b, x, y, z, r, axis="x"):
    rot = dict(rot_y=90) if axis == "x" else {}
    b.cylinder(r * 1.35, 0.12, (x, y, z), P.METAL_DARK, segments=10, base=False, **rot)


def octagon(r, c=None):
    c = r * 0.41 if c is None else c
    return [(r, -r + c), (r, r - c), (r - c, r), (-r + c, r), (-r, r - c), (-r, -r + c), (-r + c, -r), (r - c, -r)]


@piece("Pipe_Large_4m", "Pipes", collider="box", box=((0, 0, 0.55), (4.0, 1.0, 1.1)),
       notes="4 m heavy pipe (0.45 m) along X on two floor saddles. Chain them end to end.")
def pipe_large(b):
    # the concept's large pipe: dark gunmetal barrel, heavy bolted collars, an open mouth, floor saddles
    b.cylinder(0.45, 4.0, (0, 0, 0.62), P.GUNMETAL, segments=16, rot_y=90, base=False)
    b.cylinder(0.36, 0.03, (-1.99, 0, 0.62), P.VOID, segments=16, rot_y=90, base=False)
    for x, w in ((-1.82, 0.36), (1.82, 0.36), (0.0, 0.18)):
        b.cylinder(0.53, w, (x, 0, 0.62), P.METAL_DARK, segments=16, rot_y=90, base=False, bevel=0.015)
        for k in range(8):
            a = math.radians(k * 45 + 22.5)
            b.box((w + 0.04, 0.06, 0.06), (x, math.cos(a) * 0.53, 0.62 + math.sin(a) * 0.53), P.METAL,
                  base=False)
    for x in (-1.1, 1.1):
        b.box((0.34, 1.05, 0.14), (x, 0, 0), P.METAL_DARK, bevel=0.01)
        for s in (-1, 1):
            b.box((0.24, 0.12, 0.5), (x, s * 0.42, 0.12), P.METAL_DARK)
        b.box((0.26, 0.98, 0.07), (x, 0, 0.14), P.GUNMETAL)
    b.box((1.2, 0.03, 0.04), (0.9, -0.452, 0.9), P.STEEL, base=False)


@piece("Pipe_Wall_Run_4m", "Pipes", collider="none",
       notes="Three pipes and a cable tray along X, mounted on a wall face (back at 0, sticks out to -Z in Unity).")
def pipe_wall_run(b):
    runs = ((3.2, 0.16, P.METAL), (3.62, 0.11, P.RUST), (2.9, 0.07, P.METAL_DARK))
    for z, r, mat in runs:
        y = -(0.12 + r)
        b.cylinder(r, 4.0, (-2.0, y, z), mat, segments=8, rot_y=90, base=False)
    for x in (-1.5, 0.5):
        b.box((0.12, 0.45, 1.0), (x, -0.22, 2.75), P.METAL_DARK)
    b.box((4.0, 0.3, 0.05), (0, -0.2, 2.55), P.METAL_DARK)
    b.tube(sag_curve((-2.0, -0.25, 2.55), (2.0, -0.25, 2.55), 0.18, 8), 0.035, P.PLASTIC, sides=4)
    flange(b, -0.3, -0.28, 3.2, 0.16)


@piece("Pipe_Vertical_5m", "Pipes", fade=True, collider="box", box=((0, -0.3, 2.5), (0.6, 0.6, 5.0)),
       notes="Vertical 5 m pipe with brackets, for wall corners and machine rooms.")
def pipe_vertical(b):
    b.cylinder(0.22, 5.0, (0, -0.3, 0), P.METAL, segments=10)
    for z in (0.6, 2.4, 4.3):
        b.cylinder(0.3, 0.1, (0, -0.3, z), P.METAL_DARK, segments=10)
        b.box((0.12, 0.3, 0.12), (0, -0.1, z), P.METAL_DARK)
    b.cylinder(0.3, 0.25, (0, -0.3, 0), P.METAL_DARK, segments=10)


@piece("Pipe_Elbow", "Pipes", collider="box", box=((0, 0, 1.0), (1.4, 1.4, 2.0)),
       notes="Large pipe rising out of the floor and bending 90 degrees toward -Z (Unity).")
def pipe_elbow(b):
    pts = [(0, 0, -0.1)]
    for i in range(7):
        t = math.radians(i * 15)
        pts.append((0, -1.0 + math.cos(t), 0.6 + math.sin(t)))
    pts.append((0, -1.6, 1.6))
    b.tube(pts, 0.4, P.METAL, sides=10)
    b.cylinder(0.55, 0.15, (0, 0, 0), P.METAL_DARK, segments=10)
    b.cylinder(0.52, 0.12, (0, -1.6, 1.6), P.METAL_DARK, segments=10, rot_x=90, base=False)


@piece("Pipe_U_Bend", "Pipes", fade=True, collider="box", box=((0, 0, 1.4), (1.8, 0.7, 2.8)),
       notes="Two upright pipes joined by a U-bend at the floor, with a cable wrapped around (concept 'Pipe & Cable').")
def pipe_u(b):
    # the concept's 'Pipe & Cable': two dark uprights with capped tops and bands, joined by a
    # square duct along the floor
    for x in (-0.6, 0.6):
        b.box((0.46, 0.46, 0.36), (x, 0, 0), P.METAL_DARK, bevel=0.03)
        b.cylinder(0.21, 2.1, (x, 0, 0.36), P.GUNMETAL, segments=14)
        b.box((0.04, 0.02, 1.9), (x - 0.08, -0.205, 0.45), P.STEEL)
        for z, h in ((0.36, 0.12), (1.2, 0.08), (2.0, 0.08)):
            b.cylinder(0.245, h, (x, 0, z), P.METAL_DARK, segments=14)
        b.cylinder(0.26, 0.32, (x, 0, 2.42), P.METAL_DARK, segments=14, bevel=0.02)
        b.cylinder(0.2, 0.04, (x, 0, 2.74), P.VOID, segments=14)
    b.box((0.9, 0.3, 0.24), (0, 0, 0.04), P.GUNMETAL, bevel=0.02)
    b.box((0.94, 0.34, 0.05), (0, 0, 0.0), P.METAL_DARK)
    b.tube([(-0.84, -0.05, 1.9), (-0.9, -0.3, 1.0), (-0.5, -0.4, 0.06), (0.3, -0.45, 0.03), (0.9, -0.6, 0.02)],
           0.04, P.PLASTIC, sides=5)


@piece("Cable_Hanging_4m", "Pipes", collider="none",
       notes="Bundle of sagging cables spanning 4 m along X at 4.5 m height (between pillars or across a corridor).")
def cable_hanging(b):
    for dy, sag, r, mat in ((0, 0.6, 0.06, P.PLASTIC), (0.12, 0.85, 0.045, P.METAL_DARK), (-0.1, 0.45, 0.035, P.RUST)):
        b.tube(sag_curve((-2, dy, 4.6), (2, dy, 4.6), sag, 10), r, mat, sides=5)
    for x in (-2, 2):
        b.box((0.2, 0.3, 0.2), (x, 0, 4.5), P.METAL_DARK)


# ---------------------------------------------------------------------------------- lights
@piece("Lamp_Wall_Cold", "Lights", collider="none",
       lights=[{"offset": (0, -0.6, 3.3), "color": "#9CC8F0", "range": 10.0, "intensity": 3.0}],
       notes="Wall-mounted caged lamp with a cold blue light; back on the wall face.")
def lamp_wall_cold(b):
    b.box((0.6, 0.12, 0.5), (0, -0.06, 3.15), P.METAL_DARK)
    b.box((0.5, 0.28, 0.2), (0, -0.25, 3.3), P.LAMP_COLD)
    for x in (-0.24, 0, 0.24):
        b.box((0.03, 0.34, 0.26), (x, -0.27, 3.27), P.METAL_DARK)
    b.box((0.56, 0.34, 0.04), (0, -0.25, 3.42), P.METAL_DARK)


@piece("Lamp_Emergency", "Lights", collider="none",
       lights=[{"offset": (0, -0.6, 3.6), "color": WARM, "range": 10.0, "intensity": 4.5, "flicker": 0.12}],
       notes="Wall-mounted orange emergency lamp that flickers; back on the wall face.")
def lamp_emergency(b):
    b.box((0.36, 0.1, 0.36), (0, -0.05, 3.45), P.METAL_DARK)
    b.cylinder(0.14, 0.28, (0, -0.24, 3.63), P.LAMP_WARM, segments=8, rot_x=90, base=False)
    for k in range(4):
        b.box((0.03, 0.3, 0.32), (0, -0.25, 3.63), P.METAL_DARK, rot_y=k * 45)


@piece("Lamp_Alarm_Red", "Lights", collider="none",
       lights=[{"offset": (0, -0.5, 4.2), "color": "#FF3030", "range": 6.0, "intensity": 2.5, "flicker": 0.6}],
       notes="Red rotating-style alarm beacon for wall faces near gates.")
def lamp_alarm(b):
    b.box((0.3, 0.1, 0.3), (0, -0.05, 4.05), P.METAL_DARK)
    b.cylinder(0.15, 0.25, (0, -0.25, 4.05), P.METAL_DARK, segments=8)
    b.cylinder(0.12, 0.2, (0, -0.25, 4.3), P.LIGHT_RED, segments=8, radius_top=0.08)


@piece("Light_Floor_Strip_2m", "Lights", collider="none",
       notes="2 m glowing guide strip on the floor along X (no light source, just glow).")
def light_floor_strip(b):
    b.box((2.0, 0.22, 0.03), (0, 0, 0.0), P.METAL_DARK)
    b.box((1.9, 0.08, 0.04), (0, 0, 0.0), P.GLOW_SOFT)


@piece("Light_Post", "Lights", fade=True, collider="box", box=((0, 0, 1.4), (0.5, 0.5, 2.8)),
       lights=[{"offset": (0, 0, 2.6), "color": "#BFE6FF", "range": 9.0, "intensity": 2.6}],
       notes="Free-standing work light on a weighted base.")
def light_post(b):
    b.cylinder(0.32, 0.18, (0, 0, 0), P.METAL_DARK, segments=8)
    b.cylinder(0.05, 2.4, (0, 0, 0.18), P.METAL, segments=6)
    b.box((0.6, 0.3, 0.25), (0, -0.05, 2.5), P.METAL_DARK)
    b.box((0.52, 0.04, 0.17), (0, -0.21, 2.54), P.LAMP_COLD)


# ---------------------------------------------------------------------------------- consoles and terminals
def tilted_box(b, size, center, tilt, mat):
    """Box rotated about X by `tilt` degrees around its own centre."""
    b.box(size, center, mat, rot_x=tilt, base=False)


@piece("Console_Control", "Tech", collider="box", box=((0, 0, 0.98), (2.4, 1.1, 1.96)),
       lights=[{"offset": (0, -1.0, 1.4), "color": CYAN, "range": 4.5, "intensity": 1.6}],
       notes="Control desk with three monitors and button panels (front faces -Z in Unity).")
def console_control(b):
    # the concept's control console: two cabinet pedestals, an angled control deck, and a tall
    # back panel with a 3 x 2 grid of screens between handle posts
    for x in (-0.82, 0.82):
        b.box((0.72, 0.85, 0.8), (x, 0.0, 0.0), P.GUNMETAL, bevel=0.02)
        b.box((0.62, 0.02, 0.66), (x, -0.43, 0.08), P.WALL_DARK, bevel=0.005)
        b.box((0.03, 0.03, 0.16), (x + 0.22, -0.45, 0.4), P.STEEL)
        b.box((0.5, 0.02, 0.04), (x, -0.44, 0.62), P.METAL_DARK)
    b.box((0.95, 0.75, 0.15), (0, 0.05, 0.65), P.GUNMETAL)
    b.box((0.9, 0.04, 0.5), (0, 0.36, 0.1), P.METAL_DARK)
    tilted_box(b, (2.44, 0.6, 0.1), (0, -0.12, 0.86), -14, P.METAL_DARK)
    for i, x in enumerate((-0.8, 0.0, 0.8)):
        tilted_box(b, (0.66, 0.42, 0.03), (x, -0.13, 0.925), -14, P.PLASTIC)
        tilted_box(b, (0.4, 0.22, 0.02), (x - 0.06, -0.12, 0.945), -14, P.SCREEN)
        for k in range(3):
            mat = (P.LIGHT_GREEN, P.LAMP_WARM, P.LIGHT_RED)[(i + k) % 3]
            tilted_box(b, (0.05, 0.05, 0.03), (x + 0.22, -0.24 + k * 0.08, 0.93 + k * 0.02), -14, mat)
    # back panel
    b.box((2.3, 0.16, 1.0), (0, 0.34, 0.92), P.METAL_DARK, bevel=0.02)
    b.box((2.2, 0.04, 0.92), (0, 0.25, 0.96), P.GUNMETAL)
    for row, (z, h) in enumerate(((1.44, 0.4), (1.02, 0.34))):
        for col, x in enumerate((-0.72, 0.0, 0.72)):
            b.box((0.66, 0.04, h + 0.06), (x, 0.22, z - 0.03), P.PLASTIC, bevel=0.01)
            b.box((0.58, 0.03, h - 0.04), (x, 0.2, z + 0.02), P.SCREEN)
            if (row + col) % 2 == 0:
                b.box((0.3, 0.02, 0.04), (x - 0.1, 0.185, z + h * 0.6), P.LAMP_COLD)
    for s in (-1, 1):
        b.cylinder(0.035, 1.05, (s * 1.2, 0.2, 0.9), P.METAL_DARK, segments=6)
        for z in (0.95, 1.9):
            b.box((0.12, 0.12, 0.05), (s * 1.17, 0.27, z), P.METAL_DARK)
    b.box((2.36, 0.22, 0.06), (0, 0.34, 1.92), P.METAL_DARK)
    for x in (-0.36, 0.36):
        D.status_light(b, x, 0.25, 1.86, w=0.06, h=0.03)


@piece("Terminal_Wall", "Tech", collider="none",
       notes="Wall terminal with a glowing screen and keypad; back on the wall face.")
def terminal_wall(b):
    b.box((0.9, 0.16, 1.1), (0, -0.08, 1.2), P.METAL_DARK, bevel=0.02)
    b.box((0.7, 0.04, 0.5), (0, -0.17, 1.62), P.SCREEN)
    b.box((0.7, 0.3, 0.06), (0, -0.25, 1.25), P.METAL)
    for i in range(3):
        for j in range(4):
            b.box((0.1, 0.06, 0.03), (-0.24 + j * 0.16, -0.22 - i * 0.07, 1.3), P.PLASTIC)
    b.box((0.08, 0.03, 0.08), (0.3, -0.17, 1.38), P.LIGHT_GREEN)


@piece("Terminal_Offline", "Tech", collider="box", box=((0, 0, 0.6), (0.9, 0.7, 1.2)),
       notes="Dead monitor and keyboard on a small desk, a few lines still glowing.")
def terminal_offline(b):
    # the concept's offline terminal: a deep CRT monitor with green-cyan text over a keyboard, on a desk
    b.box((0.95, 0.65, 0.05), (0, 0, 0.7), P.METAL_DARK)
    b.box((0.9, 0.6, 0.7), (0, 0, 0), P.GUNMETAL, bevel=0.01)
    b.box((0.8, 0.02, 0.55), (0, -0.3, 0.08), P.WALL_DARK)
    b.box((0.66, 0.2, 0.52), (0, -0.02, 0.76), P.PLASTIC, bevel=0.03)
    b.box((0.52, 0.22, 0.4), (0, 0.18, 0.8), P.PLASTIC, bevel=0.04)
    b.box((0.34, 0.12, 0.26), (0, 0.32, 0.86), P.METAL_DARK, bevel=0.03)
    b.box((0.56, 0.02, 0.4), (0, -0.125, 0.82), P.VOID)
    for i in range(6):
        w = (0.4, 0.28, 0.34, 0.18, 0.3, 0.22)[i]
        b.box((w, 0.01, 0.022), (-0.22 + w / 2, -0.137, 1.14 - i * 0.055), P.SCREEN)
    b.box((0.08, 0.01, 0.1), (0.18, -0.137, 0.9), P.SCREEN)
    b.box((0.04, 0.02, 0.03), (0.27, -0.13, 0.78), P.LIGHT_GREEN)
    tilted_box(b, (0.6, 0.2, 0.04), (0, -0.2, 0.77), -8, P.METAL_DARK)
    for r in range(3):
        tilted_box(b, (0.54, 0.035, 0.012), (0, -0.26 + r * 0.05, 0.795 + r * 0.007), -8, P.METAL)
    b.tube([(0.2, 0.3, 0.8), (0.3, 0.42, 0.72), (0.38, 0.34, 0.7)], 0.02, P.PLASTIC, sides=4)


@piece("Server_Rack", "Tech", fade=True, collider="box", box=((0, 0, 1.1), (0.8, 1.0, 2.2)),
       notes="2.2 m server cabinet with rows of status lights.")
def server_rack(b):
    b.box((0.8, 1.0, 2.2), (0, 0, 0), P.METAL_DARK, bevel=0.02)
    for i in range(8):
        z = 0.2 + i * 0.25
        b.box((0.68, 0.04, 0.18), (0, -0.5, z), P.PLASTIC)
        for k in range(4):
            mat = (P.SCREEN, P.LIGHT_GREEN, P.SCREEN, P.LIGHT_RED)[(i + k) % 4]
            b.box((0.04, 0.03, 0.03), (-0.25 + k * 0.08, -0.53, z + 0.08), mat)
    b.box((0.82, 1.02, 0.08), (0, 0, 2.2), P.METAL)


@piece("Generator_Unit", "Tech", collider="box", box=((0, 0, 0.8), (2.6, 1.6, 1.6)),
       lights=[{"offset": (0, -1.1, 1.0), "color": WARM, "range": 4.5, "intensity": 1.6, "flicker": 0.15}],
       notes="Boxy power unit with fan housings, gauges and warm status lamps.")
def generator_unit(b):
    # the concept's generator: a dark gunmetal cabinet with a round fan grille, side vents, a
    # control strip with orange lights and a cable off the back
    b.box((2.5, 1.5, 0.14), (0, 0, 0), P.METAL_DARK, bevel=0.02)
    b.box((2.4, 1.4, 1.3), (0, 0, 0.14), P.GUNMETAL, bevel=0.04)
    b.box((2.46, 1.46, 0.12), (0, 0, 1.44), P.METAL_DARK, bevel=0.02)
    D.corner_guards(b, 2.42, 1.42, 1.3, z0=0.14, size=0.12)
    for x in (-0.6, 0.6):
        b.cylinder(0.46, 0.08, (x, -0.72, 0.8), P.METAL_DARK, segments=16, rot_x=90, base=False)
        b.cylinder(0.38, 0.04, (x, -0.77, 0.8), P.VOID, segments=16, rot_x=90, base=False)
        for k in range(4):
            b.box((0.74, 0.03, 0.05), (x, -0.79, 0.8), P.METAL, rot_y=k * 45, base=False)
        b.cylinder(0.09, 0.05, (x, -0.8, 0.8), P.METAL, segments=8, rot_x=90, base=False)
    for s in (-1, 1):
        for k in range(5):
            b.box((0.03, 0.9, 0.05), (s * 1.21, 0, 0.45 + k * 0.16), P.METAL_DARK, rot_y=0)
    b.box((1.6, 0.05, 0.16), (0, -0.72, 1.24), P.METAL_DARK)
    for i in range(4):
        b.box((0.1, 0.05, 0.07), (-0.45 + i * 0.3, -0.75, 1.28), P.LAMP_WARM if i != 2 else P.LIGHT_RED)
    b.tube([(0.9, 0.7, 0.5), (1.4, 0.9, 0.2), (1.9, 0.6, 0.03)], 0.07, P.PLASTIC, sides=5)
    b.box((0.5, 0.03, 0.2), (0.0, -0.72, 0.22), P.HAZARD)


@piece("Machine_Pump", "Tech", collider="box", box=((0, 0, 3.1), (3.4, 3.4, 6.2)), fade=True,
       lights=[{"offset": (0, -1.8, 1.6), "color": WARM, "range": 7.0, "intensity": 2.4, "flicker": 0.2}],
       notes="Big boiler / pump tank with gauges, pipes into the floor and orange work lights (technical room centrepiece).")
def machine_pump(b):
    # the concept's technical-room centrepiece: a tall dark machine of stacked drums on a heavy
    # plinth, ringed by machinery boxes, gauges and pipes, with orange work lights
    b.prism(octagon(1.75), 0.0, 0.45, P.METAL_DARK)
    b.prism(octagon(1.55), 0.45, 0.55, P.RUST)
    for k in range(4):
        a = k * 90 + 45
        r = math.radians(a)
        b.box((0.9, 0.7, 0.9), (math.cos(r) * 1.35, math.sin(r) * 1.35, 0.45), P.GUNMETAL, rot_z=a, bevel=0.03)
        b.box((0.5, 0.05, 0.08), (math.cos(r) * 1.72, math.sin(r) * 1.72, 1.1), P.LAMP_WARM, rot_z=a + 90)
    sections = ((1.2, 0.55, 1.9), (1.0, 2.45, 1.5), (1.15, 3.95, 0.5), (0.8, 4.45, 1.2), (0.5, 5.65, 0.6))
    for i, (rad, z, h) in enumerate(sections):
        b.cylinder(rad, h, (0, 0, z), P.GUNMETAL if i % 2 == 0 else P.METAL_DARK, segments=16)
        b.cylinder(rad + 0.08, 0.14, (0, 0, z), P.METAL_DARK, segments=16)
        for k in range(8):
            a = math.radians(k * 45 + 22.5)
            b.box((0.08, 0.06, h - 0.2), (math.cos(a) * rad, math.sin(a) * rad, z + 0.14), P.METAL_DARK,
                  rot_z=k * 45 + 22.5 + 90)
    b.box((0.6, 0.05, 0.08), (0, -1.21, 1.6), P.STEEL)
    b.cylinder(0.08, 3.4, (0.55, -0.98, 0.55), P.STEEL, segments=6)
    for k, a in enumerate((240, 270, 300)):
        r = math.radians(a)
        b.cylinder(0.15, 0.06, (math.cos(r) * 1.02, math.sin(r) * 1.02, 3.1 - k * 0.25), P.PORCELAIN,
                   segments=10, rot_x=90, rot_z=a - 270, base=False)
    b.box((0.7, 0.08, 0.5), (-0.3, -1.22, 1.1), P.METAL_DARK)
    b.box((0.5, 0.04, 0.2), (-0.3, -1.27, 1.25), P.LAMP_WARM)
    for a in (20, 160, 200):
        r = math.radians(a)
        p0 = (math.cos(r) * 1.0, math.sin(r) * 1.0, 3.2)
        p1 = (math.cos(r) * 2.0, math.sin(r) * 2.0, 3.0)
        p2 = (math.cos(r) * 2.1, math.sin(r) * 2.1, 0.0)
        b.tube([p0, p1, p2], 0.16, P.GUNMETAL, sides=8)
        b.cylinder(0.24, 0.12, (math.cos(r) * 2.1, math.sin(r) * 2.1, 0.0), P.METAL_DARK, segments=8)
    hazard_band(b, 1.21, 0.75, 0.2)


def hazard_band(b, r, z, h):
    for k in range(16):
        a = k * 22.5
        mat = P.HAZARD if k % 2 else P.METAL_DARK
        b.ring_segment(r, r + 0.03, z, z + h, a, a + 22.5, mat, segments=1)


# ---------------------------------------------------------------------------------- containment
def specimen_silhouette(b, y, z0, height, side=-1):
    """A dark figure painted on the front of a glowing tube: head, torso, arms, legs."""
    s = height / 1.8
    parts = (((0, 1.62), (0.2, 0.24)), ((0, 1.15), (0.38, 0.6)), ((-0.27, 1.12), (0.1, 0.62)),
             ((0.27, 1.12), (0.1, 0.62)), ((-0.1, 0.45), (0.13, 0.75)), ((0.1, 0.45), (0.13, 0.75)))
    for (x, zc), (w, h) in parts:
        b.box((w * s, 0.02, h * s), (x * s, y, z0 + zc * s), P.SPECIMEN, base=False)


@piece("Containment_Tube", "Containment", fade=True, collider="box", box=((0, 0, 1.5), (1.6, 1.6, 3.0)),
       lights=[{"offset": (0, -0.9, 1.5), "color": CYAN, "range": 5.5, "intensity": 1.8}],
       notes="3 m specimen tube: steel base and cap, glowing liquid with a dark figure inside.")
def containment_tube(b):
    # the concept's containment tube: a heavy black base and cap, bright cyan glass with a dark
    # figure, two black side columns, thin cage bars and cables trailing off the base
    b.box((1.5, 1.5, 0.12), (0, 0, 0), P.METAL_DARK, bevel=0.02)
    b.cylinder(0.76, 0.38, (0, 0, 0.12), P.PLASTIC, segments=16, bevel=0.02)
    b.cylinder(0.79, 0.07, (0, 0, 0.3), P.METAL_DARK, segments=16)
    b.cylinder(0.62, 0.05, (0, 0, 0.5), P.GLOW, segments=16)
    b.cylinder(0.56, 2.0, (0, 0, 0.55), P.GLOW_SOFT, segments=16)
    for a in (-118, -62, 62, 118):
        b.box((0.07, 0.02, 1.9), (math.cos(math.radians(a - 90)) * 0.555, math.sin(math.radians(a - 90)) * 0.555, 0.6),
              P.GLOW, rot_z=a)
    specimen_silhouette(b, -0.565, 0.75, 1.65)
    specimen_silhouette(b, 0.565, 0.75, 1.65)
    for k in range(4):
        a = math.radians(k * 90 + 45)
        b.box((0.045, 0.045, 2.0), (math.cos(a) * 0.59, math.sin(a) * 0.59, 0.55), P.METAL_DARK)
    for sx in (-1, 1):
        b.box((0.2, 0.26, 2.5), (sx * 0.7, 0.05, 0.3), P.PLASTIC, bevel=0.025)
        b.box((0.08, 0.05, 1.8), (sx * 0.7, -0.1, 0.6), P.METAL_DARK)
        for z in (0.7, 1.5, 2.3):
            b.box((0.24, 0.3, 0.07), (sx * 0.7, 0.05, z), P.METAL_DARK)
        D.status_light(b, sx * 0.7, -0.09, 0.42, w=0.05, h=0.04)
    b.cylinder(0.79, 0.08, (0, 0, 2.52), P.METAL_DARK, segments=16)
    b.cylinder(0.76, 0.38, (0, 0, 2.6), P.PLASTIC, segments=16, radius_top=0.66, bevel=0.02)
    b.cylinder(0.42, 0.16, (0, 0, 2.98), P.METAL_DARK, segments=12)
    for k in range(8):
        a = math.radians(k * 45)
        b.box((0.1, 0.1, 0.12), (math.cos(a) * 0.7, math.sin(a) * 0.7, 2.98), P.METAL_DARK)
    b.tube([(0.5, -0.5, 0.15), (0.75, -0.75, 0.03), (1.0, -0.6, 0.02), (1.3, -0.9, 0.02)], 0.035, P.PLASTIC, sides=5)
    b.tube([(-0.5, -0.55, 0.12), (-0.8, -0.7, 0.02), (-1.2, -0.5, 0.02)], 0.03, P.PLASTIC, sides=5)


@piece("Containment_Tube_Broken", "Containment", collider="box", box=((0, 0, 1.0), (1.6, 1.6, 2.0)),
       notes="Smashed specimen tube: empty, glass gone, a few bent bars and a drained base.")
def containment_tube_broken(b):
    b.cylinder(0.78, 0.4, (0, 0, 0), P.METAL_DARK, segments=12)
    b.cylinder(0.6, 0.05, (0, 0, 0.4), P.SPECIMEN, segments=12)
    for k, h in enumerate((1.9, 0.8, 1.4, 0.5, 2.1, 1.1)):
        a = math.radians(k * 60 + 30)
        lean = 6 if k % 2 else -8
        b.box((0.08, 0.08, h), (math.cos(a) * 0.6, math.sin(a) * 0.6, 0.4), P.METAL, rot_x=lean)
    b.cylinder(0.78, 0.3, (0, 0, 2.5), P.METAL_DARK, segments=12, radius_top=0.55)
    for k in range(3):
        a = math.radians(k * 120)
        b.box((0.04, 0.04, 2.1), (math.cos(a) * 0.62, math.sin(a) * 0.62, 0.4), P.METAL_DARK)
    for x, y in ((0.9, -0.4), (-0.7, -0.9), (0.2, -1.1)):
        b.box((0.25, 0.18, 0.02), (x, y, 0.0), P.TANK, rot_z=x * 70)


@piece("Containment_Tube_Large", "Containment", collider="box", box=((0, 0, 3.5), (5.0, 5.0, 7.0)), fade=True,
       lights=[{"offset": (0, 0, 2.0), "color": CYAN, "range": 16.0, "intensity": 3.2},
               {"offset": (0, -3.0, 5.5), "color": CYAN, "range": 12.0, "intensity": 2.0},
               {"offset": (0, -1.0, 4.0), "color": WARM, "range": 5.0, "intensity": 1.4}],
       notes="The laboratory's centrepiece: a 9 m containment cylinder with a huge dark specimen, "
             "cage bars, machinery crown and cables.")
def containment_tube_large(b):
    # the concept's laboratory centrepiece: a round pedestal under a column of light bars, a ring of
    # lamps overhead and a dark lantern-like capsule with a warm core hanging in the middle
    b.cylinder(2.6, 0.35, (0, 0, 0), P.METAL_DARK, segments=28, bevel=0.03)
    b.ring_segment(2.45, 2.62, 0.3, 0.37, 0, 360, P.GLOW_SOFT, segments=28)
    b.cylinder(2.05, 0.3, (0, 0, 0.35), P.GUNMETAL, segments=24)
    b.cylinder(1.2, 0.12, (0, 0, 0.65), P.METAL_DARK, segments=16)
    b.cylinder(0.9, 0.04, (0, 0, 0.77), P.GLOW_SOFT, segments=16)
    for k in range(8):
        a = math.radians(k * 45 + 22.5)
        b.box((0.5, 0.3, 0.2), (math.cos(a) * 2.3, math.sin(a) * 2.3, 0.35), P.METAL_DARK, rot_z=k * 45 + 22.5)
        b.box((0.06, 0.2, 0.06), (math.cos(a) * 2.56, math.sin(a) * 2.56, 0.42), P.LAMP_WARM, rot_z=k * 45 + 22.5)
    # the column of light: thin glowing bars, a few dark guide posts
    for k in range(24):
        a = math.radians(k * 15)
        if k % 6 == 0:
            b.box((0.14, 0.14, 7.0), (math.cos(a) * 2.05, math.sin(a) * 2.05, 0.65), P.METAL_DARK)
        else:
            b.box((0.06, 0.06, 6.6 - (k % 3) * 0.8), (math.cos(a) * 1.95, math.sin(a) * 1.95, 0.9 + (k % 3) * 0.4),
                  P.GLOW_SOFT if k % 3 else P.GLOW)
    b.ring_segment(1.85, 2.35, 7.6, 7.95, 0, 360, P.METAL_DARK, segments=28)
    for k in range(16):
        a = math.radians(k * 22.5 + 11.25)
        b.box((0.22, 0.12, 0.06), (math.cos(a) * 2.1, math.sin(a) * 2.1, 7.54), P.LAMP_COLD, rot_z=k * 22.5 + 101.25)
    # hanging capsule
    b.cylinder(0.05, 2.2, (0, 0, 5.4), P.METAL_DARK, segments=6)
    b.cylinder(0.25, 0.25, (0, 0, 5.3), P.METAL_DARK, segments=8)
    b.cylinder(0.72, 0.5, (0, 0, 4.8), P.METAL_DARK, segments=8, radius_top=0.25)
    b.prism(octagon(0.62), 3.1, 4.8, P.GUNMETAL)
    for k in range(8):
        a = math.radians(k * 45)
        b.box((0.26, 0.04, 0.75), (math.cos(a) * 0.62, math.sin(a) * 0.62, 3.55), P.LAMP_WARM, rot_z=k * 45 + 90)
        b.box((0.14, 0.14, 1.8), (math.cos(a + 0.39) * 0.66, math.sin(a + 0.39) * 0.66, 3.05), P.STEEL)
    for z in (3.5, 4.3):
        b.prism(octagon(0.7), z, z + 0.1, P.METAL_DARK)
    b.cylinder(0.72, 0.5, (0, 0, 2.6), P.METAL_DARK, segments=8, radius_top=0.72)
    b.cylinder(0.25, 0.45, (0, 0, 2.15), P.METAL_DARK, segments=8, radius_top=0.72)
    b.cylinder(0.04, 1.4, (0, 0, 0.77), P.METAL_DARK, segments=6)
    for k in range(4):
        a = math.radians(k * 90 + 45)
        b.tube([(math.cos(a) * 0.3, math.sin(a) * 0.3, 5.6), (math.cos(a) * 1.0, math.sin(a) * 1.0, 6.6),
                (math.cos(a) * 1.9, math.sin(a) * 1.9, 7.6)], 0.04, P.PLASTIC, sides=4)


@piece("Machine_Column", "Containment", collider="box", box=((0, 0, 4.5), (1.4, 1.4, 9.0)), fade=True,
       lights=[{"offset": (0, -0.9, 1.2), "color": WARM, "range": 4.0, "intensity": 1.2, "flicker": 0.08}],
       notes="9 m dark machinery column from the lab concept: a plated core with brackets, a pipe bundle, "
             "cyan and orange status lights and a heavy foot. Ring them around the lab's centre.")
def machine_column(b):
    b.prism(octagon(0.75), 0.0, 0.6, P.METAL_DARK)
    b.prism(octagon(0.62), 0.6, 0.7, P.RUST)
    b.box((0.8, 0.8, 8.0), (0, 0, 0.7), P.METAL_DARK, bevel=0.03)
    b.box((0.5, 0.86, 7.4), (0, 0, 1.0), P.GUNMETAL)
    b.box((0.86, 0.5, 7.4), (0, 0, 1.0), P.GUNMETAL)
    for z in (1.6, 3.2, 4.8, 6.4, 8.0):
        b.box((1.0, 1.0, 0.22), (0, 0, z), P.METAL_DARK, bevel=0.02)
    for sx in (-1, 1):
        b.cylinder(0.1, 7.6, (sx * 0.28, -0.56, 0.7), P.GUNMETAL, segments=8)
        b.cylinder(0.07, 7.2, (sx * 0.56, 0.3, 0.9), P.METAL_DARK, segments=6)
    for z, mat in ((1.25, P.LAMP_WARM), (2.6, P.GLOW), (5.6, P.GLOW), (7.1, P.LAMP_WARM)):
        b.box((0.12, 0.05, 0.2), (0, -0.46, z), mat)
    b.box((1.1, 1.1, 0.5), (0, 0, 8.6), P.METAL_DARK, bevel=0.03)
    b.tube(sag_curve((0.4, -0.5, 8.6), (1.6, -1.2, 9.0), 0.5, 6), 0.05, P.PLASTIC, sides=4)


@piece("Reactor_Column", "Containment", collider="box", box=((0, 0, 4.0), (5.4, 5.4, 8.0)), fade=True,
       lights=[{"offset": (0, 0, 2.6), "color": CYAN, "range": 20.0, "intensity": 7.0},
               {"offset": (0, 0, 10.5), "color": CYAN, "range": 14.0, "intensity": 3.0}],
       notes="The hub's 15 m energy column from the concept: a machinery drum, a bright core in a caged "
             "lower chamber, a ribbed dark housing with glowing slots, a narrow upper core between girders, "
             "crown and feeder pipes. Stands on Platform_Dais_6m.")
def reactor_column(b):
    # the concept's hub reactor: a heavy drum on the dais, a lower housing, a tall column of glass
    # slats around a bright beam, four structural columns with brackets and pipes up to a
    # machinery crown, cables hanging off it
    b.cylinder(3.1, 0.9, (0, 0, 0), P.METAL_DARK, segments=24, bevel=0.03)
    b.ring_segment(3.0, 3.15, 0.75, 0.85, 0, 360, P.GLOW_SOFT, segments=24)
    for k in range(12):
        a = math.radians(k * 30 + 15)
        b.box((1.2, 0.08, 0.55), (math.cos(a) * 3.1, math.sin(a) * 3.1, 0.15), P.GUNMETAL, rot_z=k * 30 + 105)
        b.box((0.06, 0.06, 0.1), (math.cos(a) * 3.16, math.sin(a) * 3.16, 0.5), P.LAMP_WARM, rot_z=k * 30 + 105)
    b.cylinder(2.6, 0.25, (0, 0, 0.9), P.GUNMETAL, segments=24)
    # lower housing: plated octagon the beam comes out of
    b.prism(octagon(1.75), 1.15, 3.3, P.METAL_DARK)
    for k in range(8):
        a = math.radians(k * 45)
        b.box((0.95, 0.06, 1.6), (math.cos(a) * 1.76, math.sin(a) * 1.76, 1.4), P.GUNMETAL, rot_z=k * 45 + 90)
        b.box((0.16, 0.05, 0.1), (math.cos(a) * 1.8, math.sin(a) * 1.8, 2.75), P.LAMP_WARM if k % 2 else P.GLOW,
              rot_z=k * 45 + 90)
    b.prism(octagon(1.85), 3.3, 3.55, P.GUNMETAL)
    b.cylinder(1.35, 0.1, (0, 0, 3.55), P.GLOW, segments=16)
    # the glass column: blue slats around the white-hot beam
    b.cylinder(0.5, 6.8, (0, 0, 3.6), P.GLOW, segments=10)
    for k in range(16):
        a = math.radians(k * 22.5)
        b.box((0.5, 0.06, 6.6), (math.cos(a) * 1.5, math.sin(a) * 1.5, 3.65), P.GLOW_SOFT, rot_z=k * 22.5 + 90)
    for z in (3.6, 10.1):
        b.ring_segment(1.45, 1.75, z, z + 0.25, 0, 360, P.METAL_DARK, segments=20)
    # four structural columns with brackets, small pipes and lights
    for k in range(4):
        a = k * 90 + 45
        r = math.radians(a)
        cx, cy = math.cos(r) * 2.35, math.sin(r) * 2.35
        b.box((0.42, 0.5, 11.2), (cx, cy, 1.0), P.METAL_DARK, rot_z=a, bevel=0.03)
        b.box((0.16, 0.54, 10.6), (cx, cy, 1.3), P.GUNMETAL, rot_z=a)
        ox, oy = math.cos(r) * 2.62, math.sin(r) * 2.62
        for z in (2.2, 4.0, 5.8, 7.6, 9.4):
            b.box((0.56, 0.66, 0.22), (cx, cy, z), P.METAL_DARK, rot_z=a, bevel=0.02)
            b.box((0.9, 0.14, 0.12), ((cx + math.cos(r) * -0.55), (cy + math.sin(r) * -0.55), z + 0.05), P.METAL_DARK,
                  rot_z=a)
        b.cylinder(0.09, 10.0, (ox + math.cos(r + 1.3) * 0.3, oy + math.sin(r + 1.3) * 0.3, 1.2), P.GUNMETAL,
                   segments=6)
        b.box((0.06, 0.08, 0.16), (ox, oy, 3.0), P.LAMP_WARM, rot_z=a)
        b.box((0.06, 0.08, 0.16), (ox, oy, 8.2), P.GLOW, rot_z=a)
    # machinery crown
    b.cylinder(1.7, 0.3, (0, 0, 10.3), P.METAL_DARK, segments=16)
    b.prism(octagon(1.45), 10.6, 12.2, P.GUNMETAL)
    for k in range(8):
        a = math.radians(k * 45 + 22.5)
        b.box((0.3, 0.3, 1.7), (math.cos(a) * 1.45, math.sin(a) * 1.45, 10.55), P.METAL_DARK, rot_z=k * 45 + 22.5)
    b.cylinder(2.4, 0.45, (0, 0, 12.2), P.METAL_DARK, segments=20, bevel=0.03)
    b.cylinder(0.9, 1.6, (0, 0, 12.65), P.GUNMETAL, segments=12, radius_top=0.7)
    b.cylinder(0.5, 1.2, (0, 0, 14.25), P.METAL_DARK, segments=8)
    for k in range(6):
        a = math.radians(k * 60 + 10)
        p0 = (math.cos(a) * 2.2, math.sin(a) * 2.2, 12.3)
        b.tube(sag_curve(p0, (math.cos(a) * 3.2, math.sin(a) * 3.2, 9.0 - (k % 3)), 0.3, 5), 0.05,
               P.PLASTIC if k % 2 else P.METAL_DARK, sides=4)
    for k in range(4):
        a = k * 90
        r = math.radians(a)
        b.tube([(math.cos(r) * 2.2, math.sin(r) * 2.2, 12.4), (math.cos(r) * 3.4, math.sin(r) * 3.4, 11.4),
                (math.cos(r) * 3.4, math.sin(r) * 3.4, 0.9)], 0.18, P.GUNMETAL, sides=8)
        b.box((0.6, 0.6, 0.5), (math.cos(r) * 3.4, math.sin(r) * 3.4, 0.9), P.METAL_DARK, rot_z=a)


# ---------------------------------------------------------------------------------- elevator
@piece("Elevator_Platform", "Tech", parts=("COL_Ramp",),
       lights=[{"offset": (0, 0, 4.5), "color": "#BFE6FF", "range": 9.0, "intensity": 2.6}],
       notes="6 x 6 m freight elevator: raised platform, corner guide rails up into the dark, call console. "
             "Its trigger and lift motion are left for your own script.")
def elevator_platform(b, parts):
    b.box((6.0, 6.0, 0.3), (0, 0, 0), P.METAL_DARK)
    b.box((5.6, 5.6, 0.04), (0, 0, 0.3), P.GRATE)
    for s in (-1, 1):
        b.box((5.8, 0.25, 0.05), (0, s * 2.7, 0.3), P.HAZARD)
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        b.box((0.4, 0.4, 12.0), (sx * 3.0, sy * 3.0, 0), P.METAL_DARK)
        b.box((0.12, 0.12, 12.0), (sx * 2.75, sy * 2.75, 0), P.METAL)
    for z in (4.5, 9.0):
        for s in (-1, 1):
            b.box((6.4, 0.2, 0.3), (0, s * 3.0, z), P.METAL_DARK)
            b.box((0.2, 6.4, 0.3), (s * 3.0, 0, z), P.METAL_DARK)
    for x in (-1.6, 1.6):
        b.tube(sag_curve((x, 0.4, 12.0), (x, 0.4, 2.0), 0.0, 2), 0.05, P.METAL, sides=4)
    b.box((1.6, 1.2, 0.2), (0, 0.4, 2.0), P.METAL_DARK)
    b.box((0.5, 0.4, 1.1), (2.2, -2.2, 0.3), P.METAL_DARK)
    b.box((0.36, 0.04, 0.3), (2.2, -2.42, 1.1), P.LIGHT_GREEN)
    ramp = parts["COL_Ramp"]
    ramp.prism([(-3.6, 0), (-3.0, 0), (-3.0, 0.3)], -3.0, 3.0, P.VOID, matrix=_side_matrix())


def _side_matrix():
    return Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


# ---------------------------------------------------------------------------------- vents and debris
@piece("Vent_Wall", "Tech", collider="none",
       notes="Ventilation unit with louvres for wall faces (concept 'Ventilation').")
def vent_wall(b):
    # the concept's ventilation unit: a heavy frame with deep louvres and corner bolts
    b.box((1.12, 0.32, 1.72), (0, -0.16, 1.4), P.METAL_DARK, bevel=0.03, base=True)
    b.box((0.88, 0.05, 1.44), (0, -0.3, 1.54), P.VOID)
    for i in range(10):
        b.box((0.86, 0.2, 0.045), (0, -0.33, 1.6 + i * 0.135), P.GUNMETAL, rot_x=-32)
    b.box((1.16, 0.36, 0.1), (0, -0.17, 3.1), P.GUNMETAL, bevel=0.01)
    b.box((1.16, 0.36, 0.1), (0, -0.17, 1.36), P.GUNMETAL, bevel=0.01)
    for sx in (-1, 1):
        for z in (1.5, 3.0):
            b.box((0.05, 0.03, 0.05), (sx * 0.5, -0.335, z), P.METAL)
        b.box((0.08, 0.03, 1.4), (sx * 0.5, -0.33, 1.55), P.METAL_DARK)
    D.status_light(b, 0.36, -0.36, 3.13, w=0.06, h=0.03)


@piece("Broken_Robot", "Props", collider="box", box=((0, 0, 0.4), (1.6, 1.4, 0.8)),
       notes="Wrecked droid: cracked skull-like head, torso plates and a torn arm with cables.")
def broken_robot(b):
    # the concept's broken robot: a big cracked skull-like head with deep eye sockets, lying in
    # torn plates, cables and limb parts
    hz = 0.0
    b.box((0.62, 0.6, 0.5), (0, 0, hz + 0.12), P.STEEL, rot_z=-18, rot_x=-14, bevel=0.17)
    b.box((0.56, 0.54, 0.22), (0.0, 0.03, hz + 0.5), P.STEEL, rot_z=-18, rot_x=-14, bevel=0.1, taper=0.75)
    b.box((0.42, 0.34, 0.18), (0.04, -0.06, hz), P.METAL, rot_z=-18, rot_x=-6, bevel=0.06, taper=1.15)
    for sx in (-1, 1):
        ex = sx * 0.14
        b.box((0.17, 0.16, 0.15), (ex * 0.95 - 0.04, -0.25 + ex * 0.33, hz + 0.3), P.VOID, rot_z=-18,
              rot_x=-14, base=False, bevel=0.05)
    b.box((0.06, 0.05, 0.08), (-0.03, -0.31, hz + 0.18), P.VOID, rot_z=-18, base=False)
    for i in range(4):
        b.box((0.035, 0.05, 0.07), (-0.11 + i * 0.07, -0.25 + i * 0.022, hz + 0.06), P.METAL_DARK, rot_z=-18)
    b.box((0.02, 0.62, 0.02), (0.1, 0.0, hz + 0.62), P.METAL_DARK, rot_z=-18, rot_x=-14)
    b.box((0.03, 0.03, 0.03), (0.12, -0.24, hz + 0.32), P.LIGHT_RED, rot_z=-18)
    # wreckage around it
    b.box((0.55, 0.38, 0.12), (0.62, 0.32, 0.0), P.METAL, rot_z=35, rot_y=-10, bevel=0.03)
    b.box((0.4, 0.1, 0.1), (0.72, -0.4, 0.0), P.METAL_DARK, rot_z=-40)
    b.box((0.3, 0.08, 0.08), (-0.6, 0.42, 0.0), P.STEEL, rot_z=70)
    b.box((0.22, 0.2, 0.05), (-0.62, -0.35, 0.0), P.GUNMETAL, rot_z=20, rot_x=8)
    b.box((0.16, 0.14, 0.04), (0.35, -0.6, 0.0), P.STEEL, rot_z=-12)
    b.tube([(0.2, 0.15, 0.08), (0.5, -0.1, 0.03), (0.85, -0.2, 0.02)], 0.025, P.PLASTIC, sides=4)
    b.tube([(-0.15, 0.25, 0.06), (-0.45, 0.4, 0.02), (-0.75, 0.2, 0.02)], 0.02, P.RUST, sides=4)
    b.tube([(0.1, 0.28, 0.05), (0.3, 0.6, 0.02), (0.1, 0.8, 0.02)], 0.018, P.METAL_DARK, sides=4)


@piece("Debris_Scrap", "Props", collider="none",
       notes="Scattered panels, bolts and a bent pipe for the floor.")
def debris_scrap(b):
    rng = b.rng
    for i in range(9):
        x, y = rng.uniform(-1.2, 1.2), rng.uniform(-1.0, 1.0)
        b.box((rng.uniform(0.2, 0.6), rng.uniform(0.15, 0.4), 0.04), (x, y, 0.0),
              rng.choice((P.METAL, P.WALL, P.RUST, P.METAL_DARK)), rot_z=rng.uniform(0, 180), rot_x=rng.uniform(-6, 6))
    b.tube([(-0.9, 0.6, 0.06), (-0.2, 0.8, 0.07), (0.3, 0.5, 0.25)], 0.06, P.METAL, sides=6)
    for i in range(4):
        b.cylinder(0.04, 0.05, (rng.uniform(-1, 1), rng.uniform(-1, 1), 0), P.METAL_DARK, segments=6)


# ---------------------------------------------------------------------------------- set dressing
@piece("Floor_Grime", "Props", collider="none",
       notes="Flat dirt: oil stains, a dark water puddle and a few scattered papers, about 3 m across. "
             "Scatter over any floor to break up the tiling (the concept's floors are never clean).")
def floor_grime(b):
    def blob(x, y, r, z, mat, squash=0.7):
        pts = []
        for k in range(10):
            a = math.radians(k * 36 + b.rng.uniform(-10, 10))
            rr = r * b.rng.uniform(0.7, 1.0)
            pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr * squash))
        b.prism(pts, 0.004, z, mat)

    # layered stains, lighter at the rim, so they read as soft dirt rather than holes
    for x, y, r in ((0.0, 0.0, 1.4), (1.0, 0.6, 0.8), (-0.9, -0.3, 0.7)):
        blob(x, y, r, 0.008, P.GRIME)
        blob(x + 0.1, y, r * 0.6, 0.012, P.GRIME_DARK)
    blob(0.4, -0.9, 0.6, 0.016, P.PUDDLE, squash=0.55)
    for k in range(3):
        b.box((0.21, 0.3, 0.008), (b.rng.uniform(-1.2, 1.2), b.rng.uniform(-1.0, 1.0), 0.016), P.MARKING,
              rot_z=b.rng.uniform(0, 180))


@piece("Cable_Floor_Run", "Pipes", collider="none",
       notes="Bundle of three loose cables snaking 4 m along X across the floor, for clutter between machines.")
def cable_floor_run(b):
    for k, (mat, r) in enumerate(((P.PLASTIC, 0.05), (P.RUST, 0.035), (P.METAL_DARK, 0.04))):
        pts = []
        for i in range(7):
            x = -2.0 + i * 4.0 / 6
            y = math.sin(i * 1.1 + k * 0.8) * 0.35 + (k - 1) * 0.12
            pts.append((x, y, r))
        b.tube(pts, r, mat, sides=5)


# ---------------------------------------------------------------------------------- room fill light
def _fill_marker(b):
    # no visible fixture (the ceiling is never seen from the game camera): a small block under the floor
    b.box((0.1, 0.1, 0.05), (0, 0, -0.3), P.METAL_DARK)


@piece("Light_Fill_Cold", "Lights", collider="none",
       lights=[{"offset": (0, 0, 4.2), "color": "#A9C8EE", "range": 13.0, "intensity": 1.6}],
       notes="Invisible cold ceiling light that lifts a room's mid-tones (the concept's overhead panels).")
def light_fill_cold(b):
    _fill_marker(b)


@piece("Light_Fill_Warm", "Lights", collider="none",
       lights=[{"offset": (0, 0, 4.2), "color": "#FF9A50", "range": 13.0, "intensity": 1.8}],
       notes="Invisible warm ceiling light for workshops and living areas.")
def light_fill_warm(b):
    _fill_marker(b)
