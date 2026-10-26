"""
Registry of lab kit pieces. A piece is a function that fills a MeshBuilder; the decorator records
how Unity should treat it (category folder, collider, lights, gate, camera fade) for LabKit.json.

Conventions for every piece (so they snap together in Unity):
  * Blender units are metres, Z up, the piece's front faces -Y.
  * The pivot sits on the floor at the piece's centre. Wall-like pieces (walls, doors, gates)
    are centred on the wall line, run along X and are WALL_T thick, so they line up on a 4 m
    (or 2 m) grid and turn in 90 degree steps.
  * Moving parts are separate objects in the same FBX, named <Piece>_Leaf_L / _Leaf_R (slide
    sideways) or <Piece>_Leaf_Up (slides up). Unity's Gate component moves them.
  * Objects named <Piece>_COL_<x> are invisible collision shapes (e.g. a ramp under stairs).
"""

from dataclasses import dataclass, field

from .geo import MeshBuilder


@dataclass
class Piece:
    name: str
    category: str
    build: callable
    collider: str = "mesh"          # "mesh", "box" or "none"
    box: tuple = None               # ((cx, cy, cz), (sx, sy, sz)) in Blender piece space
    lights: list = None             # [{"offset": (x, y, z), "color": hex, "range": m, "intensity": i, "flicker": 0..1}]
    parts: tuple = ()               # extra object names: "Leaf_L", "Leaf_R", "Leaf_Up", "COL_Ramp"
    gate: dict = None               # {"travel": m, "auto": bool, "startsOpen": bool, "sensor": (cx, cy, cz, sx, sy, sz)}
    fade: bool = False              # tall piece the isometric camera fades out when it hides the player
    notes: str = ""


PIECES = {}


def piece(name, category, collider="mesh", box=None, lights=None, parts=(), gate=None, fade=False, notes=""):
    def register(fn):
        PIECES[name] = Piece(name, category, fn, collider, box, lights, tuple(parts), gate, fade, notes)
        return fn
    return register


def build(name, collection=None):
    """Builds a piece; returns its objects, main object first."""
    p = PIECES[name]
    main = MeshBuilder(name)
    extra = {part: MeshBuilder(f"{name}_{part}", seed=hash(name + part) & 0xFFFF) for part in p.parts}
    if p.parts:
        p.build(main, extra)
    else:
        p.build(main)
    objs = [main.finish(collection=collection)]
    for part, builder in extra.items():
        objs.append(builder.finish(collection=collection))
    return objs


def _blender_to_unity_local(v):
    """Piece space conversion done by the FBX export + Unity import (verified in the editor):
    (x, y, z) -> (x, z, y)."""
    x, y, z = v
    return [round(x, 4), round(z, 4), round(y, 4)]


def _size(s):
    return [abs(s[0]), abs(s[2]), abs(s[1])]


def manifest_entry(p, fbx_path):
    entry = {
        "name": p.name,
        "category": p.category,
        "model": fbx_path,
        "collider": p.collider,
        "notes": p.notes,
        "boxCenter": [0, 0, 0],
        "boxSize": [0, 0, 0],
        "fade": p.fade,
        "lights": [],
        "isGate": p.gate is not None,
        "gateTravel": 0.0,
        "gateAuto": False,
        "gateStartsOpen": False,
        "sensorCenter": [0, 0, 0],
        "sensorSize": [0, 0, 0],
    }
    if p.box:
        (c, s) = p.box
        entry["boxCenter"] = _blender_to_unity_local(c)
        entry["boxSize"] = _size(s)
    for light in p.lights or []:
        entry["lights"].append({
            "offset": _blender_to_unity_local(light["offset"]),
            "color": light.get("color", "#9ED8FF"),
            "range": light.get("range", 6.0),
            "intensity": light.get("intensity", 2.0),
            "flicker": light.get("flicker", 0.0),
            "spot": light.get("spot", 0.0),
        })
    if p.gate:
        entry["gateTravel"] = p.gate.get("travel", 2.0)
        entry["gateAuto"] = p.gate.get("auto", False)
        entry["gateStartsOpen"] = p.gate.get("startsOpen", False)
        sensor = p.gate.get("sensor")
        if sensor:
            entry["sensorCenter"] = _blender_to_unity_local(sensor[:3])
            entry["sensorSize"] = _size(sensor[3:])
    return entry
