"""
Sections of the demo level: the area the player stands in is the only one Unity shows.

Every placed piece is tagged with the sections it belongs to. A wall on the line between two
sections belongs to both, so from either side it stays visible. Unity also gets a 0.25 m raster
of the level (the section map), which tells it in which section the player stands.
"""

import math

from . import layout as L

SECTIONS = ["Hub", "Corridor", "InnerRing", "OuterRing", "Technical", "Sleeping", "Bathroom",
            "Kitchen", "Storage", "Lab", "Elevator"]

# section map bounds in Blender level space (metres)
MAP_X0, MAP_X1 = -50, 82
MAP_Y0, MAP_Y1 = -50, 50
CELL = 0.25

ALWAYS = {"Floor_Void_Slab"}
CENTRE_ONLY = {"Floor_Hub_Disc", "Floor_Arena_Disc"}


def _elevator_y0():
    return L.LAB_C[1] + L.R_LAB * math.cos(math.radians(L.half_angle(L.GATE, L.R_LAB)))


def _corridor_x1():
    return L.LAB_C[0] - L.R_LAB * math.cos(math.radians(L.half_angle(L.GATE, L.R_LAB)))


def section_of(x, y):
    """Section at a floor point (Blender level space), or None outside the level."""
    r = math.hypot(x, y)
    if L.LAB_C[0] - 4 <= x <= L.LAB_C[0] + 4 and _elevator_y0() <= y <= 46:
        return "Elevator"
    if math.hypot(x - L.LAB_C[0], y - L.LAB_C[1]) < L.R_LAB:
        return "Lab"
    if r < L.R_HUB:
        return "Hub"
    if 8.0 <= x <= _corridor_x1() and abs(y) < L.CORR:
        return "Corridor"
    if r < L.R1:
        return "InnerRing"
    if r < L.R2:
        return "OuterRing"
    if r > L.R_OUT:
        return None
    a = math.degrees(math.atan2(y, x)) % 360.0
    if y > 0:
        return "Technical" if a < 94.0 else "Sleeping"
    if L.is_bathroom(x, y):
        return "Bathroom"
    return "Kitchen" if a < 270.0 else "Storage"


def _samples(p):
    """Points (local u along the piece, v toward its back) that decide where a piece belongs."""
    name, sx = p["piece"], p.get("sx", 1.0)
    if name in CENTRE_ONLY:
        return [(0.0, 0.0)]
    if name.startswith("Floor_"):
        return [(u, v) for u in (-1.8, 0.0, 1.8) for v in (-1.8, 0.0, 1.8)]
    if name == "Rock_Mass_4m":
        # backfill sits behind a wall; its front looks into the room
        return [(u * sx, -1.2) for u in (-1.5, 0.0, 1.5)]
    if name.startswith(("Wall_", "Door_", "Gate_", "Railing_")):
        return [(u * sx, v) for u in (-1.6, 0.0, 1.6) for v in (-0.9, 0.9)]
    # props and wall lamps: the piece itself and the space in front of it
    return [(0.0, 0.0), (0.0, -0.6)]


def sections_for(p):
    if p["piece"] in ALWAYS:
        return []
    x, y = p["loc"][0], p["loc"][1]
    t = math.radians(p["rot"])
    c, s = math.cos(t), math.sin(t)
    found = []
    for u, v in _samples(p):
        sec = section_of(x + u * c - v * s, y + u * s + v * c)
        if sec and sec not in found:
            found.append(sec)
    if found:
        return found
    # outside every section (e.g. rock far behind a wall): take the nearest one
    for radius in (1.5, 3.0, 5.0, 8.0, 12.0):
        for k in range(16):
            a = math.radians(k * 22.5)
            sec = section_of(x + math.cos(a) * radius, y + math.sin(a) * radius)
            if sec:
                return [sec]
    return []


def tag(placements):
    for p in placements:
        p["sections"] = sections_for(p)
    return placements


def section_map():
    """Rows along Unity +Z (Blender +Y), one character per cell: 'A' + section index or '.'."""
    w = int((MAP_X1 - MAP_X0) / CELL)
    h = int((MAP_Y1 - MAP_Y0) / CELL)
    rows = []
    for j in range(h):
        y = MAP_Y0 + (j + 0.5) * CELL
        row = []
        for i in range(w):
            sec = section_of(MAP_X0 + (i + 0.5) * CELL, y)
            row.append(chr(ord("A") + SECTIONS.index(sec)) if sec else ".")
        rows.append("".join(row))
    # Unity level space: x stays, Blender y becomes z
    return {"names": SECTIONS, "originX": MAP_X0, "originZ": MAP_Y0, "cellSize": CELL,
            "width": w, "height": h, "cells": "".join(rows)}
