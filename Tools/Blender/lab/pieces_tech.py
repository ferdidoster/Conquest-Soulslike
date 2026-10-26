"""Lab machinery: pipes, cables, lights, consoles, the containment tubes and the hub reactor."""

import math

from mathutils import Matrix

from . import palette as P
from .geo import sag_curve
from .kit import piece

COLD = "#8FD0FF"
WARM = "#FF8A3A"
CYAN = "#3FC8FF"


# ---------------------------------------------------------------------------------- pipes and cables
def flange(b, x, y, z, r, axis="x"):
    rot = dict(rot_y=90) if axis == "x" else {}
    b.cylinder(r * 1.35, 0.12, (x, y, z), P.METAL_DARK, segments=10, base=False, **rot)


@piece("Pipe_Large_4m", "Pipes", collider="box", box=((0, 0, 0.55), (4.0, 1.0, 1.1)),
       notes="4 m heavy pipe (0.45 m) along X on two floor saddles. Chain them end to end.")
def pipe_large(b):
    b.cylinder(0.45, 4.0, (-2.0, 0, 0.6), P.METAL, segments=12, rot_y=90, base=False)
    for x in (-1.92, 1.92):
        flange(b, x, 0, 0.6, 0.45)
    for x in (-1.0, 1.0):
        b.box((0.3, 1.0, 0.25), (x, 0, 0), P.METAL_DARK)
        b.box((0.24, 0.2, 0.6), (x, 0, 0.0), P.METAL_DARK)
    b.box((0.9, 0.06, 0.25), (0.2, -0.46, 0.75), P.HAZARD)


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
    for x in (-0.6, 0.6):
        b.cylinder(0.25, 2.6, (x, 0, 0.6), P.METAL, segments=10)
        b.cylinder(0.32, 0.18, (x, 0, 2.6), P.METAL_DARK, segments=10)
        b.cylinder(0.32, 0.12, (x, 0, 1.4), P.METAL_DARK, segments=10)
    pts = [(-0.6 + 0.6 - 0.6 * math.cos(math.radians(a)), 0, 0.6 - 0.45 * math.sin(math.radians(a))) for a in range(0, 181, 30)]
    b.tube(pts, 0.25, P.METAL, sides=10)
    b.tube([(-0.85, -0.1, 2.2), (-0.3, -0.35, 1.7), (0.3, -0.35, 1.2), (0.85, -0.1, 0.8)], 0.05, P.PLASTIC, sides=5)


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


@piece("Console_Control", "Tech", collider="box", box=((0, 0, 0.6), (2.4, 1.1, 1.2)),
       lights=[{"offset": (0, -1.0, 1.4), "color": CYAN, "range": 4.5, "intensity": 1.6}],
       notes="Control desk with three monitors and button panels (front faces -Z in Unity).")
def console_control(b):
    b.box((2.4, 0.9, 0.85), (0, 0.05, 0), P.METAL_DARK, bevel=0.03)
    b.box((2.2, 0.06, 0.6), (0, -0.42, 0.12), P.WALL)
    tilted_box(b, (2.4, 0.6, 0.1), (0, -0.2, 0.92), -18, P.METAL)
    for i, x in enumerate((-0.75, -0.25, 0.25, 0.75)):
        tilted_box(b, (0.35, 0.3, 0.04), (x, -0.22, 0.98), -18, P.PLASTIC)
        b.box((0.06, 0.06, 0.04), (x - 0.1, -0.3, 0.95), P.LIGHT_RED if i % 2 else P.LIGHT_GREEN)
        b.box((0.06, 0.06, 0.04), (x + 0.08, -0.18, 0.99), P.SCREEN)
    for x, tilt in ((-0.78, 12), (0.0, 0), (0.78, -12)):
        b.box((0.08, 0.08, 0.35), (x, 0.25, 0.95), P.METAL_DARK)
        b.box((0.74, 0.08, 0.5), (x, 0.24, 1.25), P.PLASTIC, rot_z=tilt)
        b.box((0.66, 0.03, 0.42), (x, 0.19, 1.29), P.SCREEN, rot_z=tilt)


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
    b.box((0.9, 0.6, 0.75), (0, 0, 0), P.METAL_DARK)
    b.box((0.7, 0.45, 0.48), (0, 0.05, 0.75), P.PLASTIC, bevel=0.03)
    b.box((0.56, 0.03, 0.34), (0, -0.19, 0.81), P.VOID)
    for i in range(3):
        b.box((0.3 - i * 0.07, 0.02, 0.03), (-0.1, -0.21, 1.05 - i * 0.07), P.SCREEN)
    b.box((0.6, 0.22, 0.04), (0, -0.24, 0.75), P.METAL)


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
    b.box((2.4, 1.4, 1.4), (0, 0, 0), P.METAL_DARK, bevel=0.05)
    b.box((2.5, 1.5, 0.15), (0, 0, 1.4), P.METAL)
    for x in (-0.6, 0.6):
        b.cylinder(0.42, 0.1, (x, -0.72, 0.75), P.METAL, segments=12, rot_x=90, base=False)
        for k in range(3):
            b.box((0.78, 0.04, 0.06), (x, -0.78, 0.75), P.METAL_DARK, rot_y=k * 60)
    for i in range(3):
        b.box((0.12, 0.05, 0.12), (-0.3 + i * 0.3, -0.72, 1.28), P.LAMP_WARM if i != 1 else P.LIGHT_RED)
    b.tube([(1.2, 0.2, 0.4), (1.6, 0.3, 0.3), (1.9, 0.1, 0.05)], 0.09, P.PLASTIC)
    b.box((0.6, 0.04, 0.4), (0.0, -0.72, 0.2), P.HAZARD)


@piece("Machine_Pump", "Tech", collider="box", box=((0, 0, 2.0), (3.0, 3.0, 4.0)), fade=True,
       lights=[{"offset": (0, -1.8, 1.6), "color": WARM, "range": 7.0, "intensity": 2.4, "flicker": 0.2}],
       notes="Big boiler / pump tank with gauges, pipes into the floor and orange work lights (technical room centrepiece).")
def machine_pump(b):
    b.box((3.0, 3.0, 0.4), (0, 0, 0), P.METAL_DARK, bevel=0.04)
    b.cylinder(1.25, 3.2, (0, 0, 0.4), P.METAL, segments=16)
    for z in (0.9, 2.0, 3.1):
        b.cylinder(1.33, 0.16, (0, 0, z), P.METAL_DARK, segments=16)
    b.cylinder(1.25, 0.5, (0, 0, 3.6), P.METAL_DARK, segments=16, radius_top=0.6)
    b.cylinder(0.3, 0.6, (0, 0, 4.1), P.RUST, segments=8)
    for k, a in enumerate((200, 250, 300)):
        r = math.radians(a)
        b.cylinder(0.16, 0.08, (math.cos(r) * 1.3, math.sin(r) * 1.3, 2.5 - k * 0.3), P.STEEL, segments=8,
                   rot_x=90, rot_z=a - 270, base=False)
    b.box((0.5, 0.06, 0.5), (0, -1.3, 1.4), P.METAL_DARK)
    b.box((0.3, 0.04, 0.15), (0, -1.34, 1.55), P.LAMP_WARM)
    for a in (30, 150):
        r = math.radians(a)
        p0 = (math.cos(r) * 1.25, math.sin(r) * 1.25, 2.6)
        p1 = (math.cos(r) * 2.0, math.sin(r) * 2.0, 2.4)
        p2 = (math.cos(r) * 2.1, math.sin(r) * 2.1, 0.0)
        b.tube([p0, p1, p2], 0.18, P.METAL, sides=8)
    hazard_band(b, 1.27, 0.45, 0.25)


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
    b.cylinder(0.78, 0.4, (0, 0, 0), P.METAL_DARK, segments=12)
    b.cylinder(0.7, 0.06, (0, 0, 0.4), P.GLOW, segments=12)
    b.cylinder(0.56, 2.1, (0, 0, 0.46), P.TANK, segments=12)
    for k in range(6):
        a = math.radians(k * 60 + 30)
        b.box((0.08, 0.08, 2.1), (math.cos(a) * 0.6, math.sin(a) * 0.6, 0.46), P.METAL)
    specimen_silhouette(b, -0.565, 0.6, 1.8)
    specimen_silhouette(b, 0.565, 0.6, 1.8)
    b.cylinder(0.78, 0.45, (0, 0, 2.56), P.METAL_DARK, segments=12, radius_top=0.55)
    b.cylinder(0.25, 0.25, (0, 0, 3.0), P.METAL, segments=8)
    b.tube([(0.3, 0.2, 3.1), (0.8, 0.5, 3.4), (1.2, 0.6, 3.6)], 0.06, P.PLASTIC, sides=4)
    b.box((0.5, 0.06, 0.25), (0, -0.78, 0.1), P.SCREEN)


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
               {"offset": (0, -3.0, 5.5), "color": CYAN, "range": 12.0, "intensity": 2.0}],
       notes="The laboratory's centrepiece: a 9 m containment cylinder with a huge dark specimen, "
             "cage bars, machinery crown and cables.")
def containment_tube_large(b):
    b.cylinder(2.6, 0.5, (0, 0, 0), P.METAL_DARK, segments=20)
    b.cylinder(2.35, 0.3, (0, 0, 0.5), P.METAL, segments=20)
    b.cylinder(2.2, 0.08, (0, 0, 0.8), P.GLOW, segments=20)
    b.cylinder(1.9, 5.6, (0, 0, 0.88), P.TANK, segments=20)
    for k in range(10):
        a = math.radians(k * 36)
        b.box((0.18, 0.18, 5.6), (math.cos(a) * 2.0, math.sin(a) * 2.0, 0.88), P.METAL_DARK)
    for z in (2.4, 4.4):
        b.ring_segment(1.95, 2.15, z, z + 0.18, 0, 360, P.METAL, segments=20)
    for side in (-1, 1):
        specimen_silhouette(b, side * 1.905, 1.3, 4.6)
    b.cylinder(2.6, 0.7, (0, 0, 6.48), P.METAL_DARK, segments=20, radius_top=2.2)
    b.cylinder(1.4, 1.0, (0, 0, 7.18), P.METAL, segments=12, radius_top=0.9)
    b.cylinder(0.5, 0.8, (0, 0, 8.18), P.METAL_DARK, segments=8)
    for k in range(6):
        a = k * 60 + 15
        r = math.radians(a)
        p0 = (math.cos(r) * 2.2, math.sin(r) * 2.2, 6.9)
        p1 = (math.cos(r) * 3.4, math.sin(r) * 3.4, 6.2)
        p2 = (math.cos(r) * 3.9, math.sin(r) * 3.9, 0.0)
        b.tube([p0, p1, p2], 0.12 if k % 2 else 0.08, P.PLASTIC if k % 2 else P.METAL, sides=5)
    for k in range(4):
        a = math.radians(k * 90 + 45)
        b.box((0.7, 0.7, 0.8), (math.cos(a) * 2.5, math.sin(a) * 2.5, 0), P.METAL, bevel=0.03)
        b.box((0.3, 0.05, 0.15), (math.cos(a) * 2.5, math.sin(a) * 2.5 - 0.36, 0.55), P.GLOW)


@piece("Reactor_Column", "Containment", collider="box", box=((0, 0, 4.0), (5.4, 5.4, 8.0)), fade=True,
       lights=[{"offset": (0, 0, 2.6), "color": CYAN, "range": 20.0, "intensity": 7.0},
               {"offset": (0, 0, 10.5), "color": CYAN, "range": 14.0, "intensity": 3.0}],
       notes="The hub's 15 m energy column from the concept: a machinery drum, a bright core in a caged "
             "lower chamber, a ribbed dark housing with glowing slots, a narrow upper core between girders, "
             "crown and feeder pipes. Stands on Platform_Dais_6m.")
def reactor_column(b):
    # machinery drum the column stands in
    b.cylinder(3.2, 0.7, (0, 0, 0), P.METAL_DARK, segments=16)
    b.cylinder(2.95, 0.3, (0, 0, 0.7), P.METAL, segments=16, radius_top=2.7)
    for k in range(8):
        a = math.radians(k * 45)
        b.box((0.9, 0.55, 0.85), (math.cos(a) * 2.75, math.sin(a) * 2.75, 0), P.METAL, rot_z=k * 45, bevel=0.03)
        b.box((0.5, 0.06, 0.22), (math.cos(a) * 3.04, math.sin(a) * 3.04, 0.35), P.HAZARD, rot_z=k * 45 + 90)
    b.cylinder(2.3, 0.12, (0, 0, 1.0), P.GLOW, segments=16)
    # lower chamber: the bright core behind a cage, what the concept's light comes from
    b.cylinder(1.0, 3.3, (0, 0, 1.1), P.GLOW, segments=12)
    for k in range(10):
        a = math.radians(k * 36)
        b.box((0.12, 0.12, 3.3), (math.cos(a) * 1.3, math.sin(a) * 1.3, 1.1), P.METAL_DARK)
    for k in range(4):
        a = math.radians(k * 90 + 45)
        b.box((0.45, 0.45, 3.4), (math.cos(a) * 1.65, math.sin(a) * 1.65, 1.0), P.METAL_DARK, rot_z=k * 90 + 45)
        b.box((0.2, 0.5, 3.4), (math.cos(a) * 1.9, math.sin(a) * 1.9, 1.0), P.METAL, rot_z=k * 90 + 45)
    b.ring_segment(1.2, 1.75, 2.7, 2.85, 0, 360, P.METAL, segments=16)
    # heavy housing: ribbed dark steel with thin glowing slots
    b.cylinder(2.0, 0.6, (0, 0, 4.4), P.METAL, segments=16, radius_top=1.9)
    b.cylinder(1.85, 3.8, (0, 0, 5.0), P.METAL_DARK, segments=16)
    for k in range(16):
        a = math.radians(k * 22.5)
        if k % 4 == 0:
            b.box((0.16, 0.08, 2.8), (math.cos(a) * 1.86, math.sin(a) * 1.86, 5.5), P.GLOW, rot_z=k * 22.5 + 90)
        else:
            b.box((0.18, 0.22, 3.8), (math.cos(a) * 1.9, math.sin(a) * 1.9, 5.0), P.METAL, rot_z=k * 22.5)
    for z in (5.0, 6.9, 8.6):
        b.ring_segment(1.85, 2.1, z, z + 0.25, 0, 360, P.METAL, segments=16)
    for k in range(3):
        a = math.radians(k * 120 + 20)
        b.tube([(math.cos(a) * 2.0, math.sin(a) * 2.0, 8.4), (math.cos(a) * 2.35, math.sin(a) * 2.35, 6.8),
                (math.cos(a) * 2.25, math.sin(a) * 2.25, 4.9)], 0.13, P.RUST, sides=6)
    # upper column: a narrow core between girders up to the crown
    b.cylinder(0.55, 3.8, (0, 0, 8.8), P.GLOW, segments=10)
    for k in range(4):
        a = math.radians(k * 90)
        b.box((0.3, 0.3, 3.9), (math.cos(a) * 1.15, math.sin(a) * 1.15, 8.8), P.METAL_DARK)
    for z in (9.9, 11.2):
        b.ring_segment(0.95, 1.35, z, z + 0.2, 0, 360, P.METAL_DARK, segments=12)
    # crown and feeder pipes down to the drum
    b.cylinder(2.5, 1.1, (0, 0, 12.6), P.METAL_DARK, segments=16, radius_top=1.9)
    b.ring_segment(2.4, 2.6, 12.6, 12.85, 0, 360, P.METAL, segments=16)
    b.cylinder(1.3, 1.2, (0, 0, 13.7), P.METAL, segments=12, radius_top=0.9)
    b.cylinder(0.45, 0.5, (0, 0, 14.9), P.GLOW, segments=8)
    for k in range(4):
        a = k * 90 + 45
        r = math.radians(a)
        p0 = (math.cos(r) * 2.3, math.sin(r) * 2.3, 13.0)
        p1 = (math.cos(r) * 4.2, math.sin(r) * 4.2, 12.0)
        p2 = (math.cos(r) * 4.6, math.sin(r) * 4.6, 0.9)
        b.tube([p0, p1, p2], 0.26, P.METAL_DARK, sides=8)
        b.box((0.9, 0.9, 0.9), (math.cos(r) * 4.6, math.sin(r) * 4.6, 0), P.METAL_DARK, rot_z=a, bevel=0.03)
        b.box((0.4, 0.06, 0.12), (math.cos(r) * 4.6, math.sin(r) * 4.6, 0.6), P.LAMP_WARM, rot_z=a + 90)


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
    b.box((1.0, 0.25, 1.6), (0, -0.12, 1.6), P.METAL_DARK, bevel=0.02)
    for i in range(9):
        b.box((0.86, 0.1, 0.06), (0, -0.27, 1.72 + i * 0.15), P.METAL, rot_x=-25)
    b.box((1.04, 0.28, 0.08), (0, -0.13, 3.2), P.METAL)


@piece("Broken_Robot", "Props", collider="box", box=((0, 0, 0.4), (1.6, 1.4, 0.8)),
       notes="Wrecked droid: cracked skull-like head, torso plates and a torn arm with cables.")
def broken_robot(b):
    b.box((0.8, 0.55, 0.45), (0.1, 0.1, 0.0), P.STEEL, rot_z=20, bevel=0.04)
    b.box((0.6, 0.5, 0.3), (0.15, 0.15, 0.45), P.METAL, rot_z=14, rot_y=-12, bevel=0.03)
    # head
    b.box((0.42, 0.42, 0.38), (-0.55, -0.25, 0.0), P.STEEL, rot_z=-25, rot_x=-12, bevel=0.06)
    for x in (-0.64, -0.47):
        b.box((0.1, 0.04, 0.08), (x, -0.47, 0.22), P.VOID, rot_z=-25)
    b.box((0.05, 0.04, 0.05), (-0.62, -0.48, 0.24), P.LIGHT_RED, rot_z=-25)
    b.box((0.5, 0.12, 0.12), (0.75, -0.3, 0.05), P.METAL_DARK, rot_z=-35)
    b.box((0.4, 0.1, 0.1), (1.05, -0.55, 0.02), P.STEEL, rot_z=-60)
    b.tube([(0.5, -0.1, 0.2), (0.7, -0.45, 0.1), (0.95, -0.4, 0.02)], 0.03, P.PLASTIC, sides=4)
    b.tube([(0.45, 0.0, 0.25), (0.8, 0.2, 0.08), (1.0, 0.4, 0.02)], 0.025, P.RUST, sides=4)
    b.box((0.18, 0.5, 0.12), (-0.1, 0.55, 0.0), P.METAL_DARK, rot_z=80)


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
