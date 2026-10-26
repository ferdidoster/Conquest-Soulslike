"""
Registry of kit pieces. A piece is a function that fills a MeshBuilder; the decorator records
how Unity should treat it (category folder, collider, light, hover) for DesertKit.json.

Conventions for every piece (so they snap together in Unity):
  * Blender units are metres, Z up, the piece's front faces -Y.
  * The pivot sits on the ground. Ring pieces (floor, walls, stands) pivot on the arena
    centre and cover a wedge centred on +Y, so you place them all at one point and rotate them
    in 15/45 degree steps.
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
    light: dict = None              # {"offset": (x, y, z), "color": hex, "range": m, "intensity": i}
    hover: bool = False             # bob gently (sky ships)
    notes: str = ""


PIECES = {}


def piece(name, category, collider="mesh", box=None, light=None, hover=False, notes=""):
    def register(fn):
        PIECES[name] = Piece(name, category, fn, collider, box, light, hover, notes)
        return fn
    return register


def build(name, collection=None):
    p = PIECES[name]
    builder = MeshBuilder(name)
    p.build(builder)
    return builder.finish(collection=collection)


def _blender_to_unity_local(v):
    """Piece space conversion done by the FBX export + Unity import (verified in the editor):
    (x, y, z) -> (x, z, y)."""
    x, y, z = v
    return [round(x, 4), round(z, 4), round(y, 4)]


def manifest_entry(p, fbx_path):
    entry = {
        "name": p.name,
        "category": p.category,
        "model": fbx_path,
        "collider": p.collider,
        "hover": p.hover,
        "notes": p.notes,
        "boxCenter": [0, 0, 0],
        "boxSize": [0, 0, 0],
        "hasLight": p.light is not None,
        "lightOffset": [0, 0, 0],
        "lightColor": "#FFFFFF",
        "lightRange": 0.0,
        "lightIntensity": 0.0,
    }
    if p.box:
        (c, s) = p.box
        entry["boxCenter"] = _blender_to_unity_local(c)
        entry["boxSize"] = [abs(s[0]), abs(s[2]), abs(s[1])]
    if p.light:
        entry["lightOffset"] = _blender_to_unity_local(p.light["offset"])
        entry["lightColor"] = p.light.get("color", "#FFA850")
        entry["lightRange"] = p.light.get("range", 6.0)
        entry["lightIntensity"] = p.light.get("intensity", 2.0)
    return entry
