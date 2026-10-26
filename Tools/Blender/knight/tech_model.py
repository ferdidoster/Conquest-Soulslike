"""
The tech-knight: a cloaked wanderer in a tall helm with a cyan visor, a tattered red cloak,
a tech backpack. Its weapons are separate meshes on the same rig, see weapons.py.

Uses the same rig, sockets and animations as the classic knight (rig.py / animations.py), so it is
a drop-in replacement. Hard parts are rigidly bound to one bone. Soft parts (cloak, scarf, coat
skirt) blend between bones so they follow the hips and legs instead of being pierced by them.
"""

import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

from .model import MeshBuilder

# name: (base colour (linear), metallic, roughness, emission strength)
# Concept palette: oxblood red, charcoal, warm grey, bone white, cyan.
PALETTE = {
    "Cloak": ((0.24, 0.036, 0.036, 1), 0.0, 0.9, 0.0),
    "CloakDark": ((0.13, 0.02, 0.024, 1), 0.0, 0.9, 0.0),
    "Suit": ((0.025, 0.025, 0.032, 1), 0.0, 0.8, 0.0),
    "Plate": ((0.06, 0.06, 0.07, 1), 0.3, 0.6, 0.0),
    "Boot": ((0.05, 0.035, 0.03, 1), 0.0, 0.7, 0.0),
    "Grey": ((0.30, 0.31, 0.31, 1), 0.2, 0.6, 0.0),
    "Coat": ((0.37, 0.39, 0.37, 1), 0.0, 0.85, 0.0),
    "Helm": ((0.48, 0.46, 0.43, 1), 0.2, 0.5, 0.0),
    "Leather": ((0.07, 0.05, 0.045, 1), 0.0, 0.8, 0.0),
    "Blade": ((0.05, 0.055, 0.065, 1), 0.6, 0.35, 0.0),
    "Glow": ((0.05, 0.75, 0.70, 1), 0.0, 0.4, 4.0),
    "NaniteGlow": ((0.05, 0.30, 1.0, 1), 0.0, 0.35, 5.0),  # the nano-injector's cartridges and port
}


def make_materials():
    mats = {}
    for name, (color, metallic, roughness, emission) in PALETTE.items():
        m = bpy.data.materials.new("TechKnight_" + name)
        m.diffuse_color = color
        m.metallic = metallic
        m.roughness = roughness
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = color
            bsdf.inputs["Metallic"].default_value = metallic
            bsdf.inputs["Roughness"].default_value = roughness
            if emission > 0:
                bsdf.inputs["Emission Color"].default_value = color
                bsdf.inputs["Emission Strength"].default_value = emission
        mats[name] = m
    return mats


class TechBuilder(MeshBuilder):
    """MeshBuilder with blended skin weights and a few soft-surface primitives."""

    def weigh(self, verts, fn):
        """Replaces the weights of `verts` with fn(co) -> {bone: weight} (normalised here)."""
        for v in verts:
            weights = {k: w for k, w in fn(v.co).items() if w > 1e-4}
            total = sum(weights.values())
            v[self.deform].clear()
            for bone, w in weights.items():
                v[self.deform][self.groups[bone]] = w / total

    def sheet(self, rows, group, mat, thickness=0.012, closed=False):
        """
        A surface through a grid of points (rows of equal length, top to bottom), thickened
        inward so it renders from both sides with back-face culling.
        """
        grid = [[self.bm.verts.new(p) for p in row] for row in rows]
        faces = []
        cols = len(rows[0])
        span = cols if closed else cols - 1
        for r in range(len(rows) - 1):
            for c in range(span):
                c2 = (c + 1) % cols
                faces.append(self.bm.faces.new((grid[r][c], grid[r + 1][c], grid[r + 1][c2], grid[r][c2])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        out = bmesh.ops.solidify(self.bm, geom=faces, thickness=thickness)
        verts = list({v for f in faces for v in f.verts} |
                     {v for g in out["geom"] if isinstance(g, bmesh.types.BMFace) for v in g.verts})
        return self._finish(verts, group, mat, Matrix.Identity(4), smooth=False)

    def loft(self, rings, group, mat, cap_top=True, cap_bottom=True, smooth=False):
        """Closed loft through rings of points (top to bottom), each a list of the same length."""
        grid = [[self.bm.verts.new(p) for p in ring] for ring in rings]
        n = len(rings[0])
        faces = []
        for r in range(len(rings) - 1):
            for c in range(n):
                c2 = (c + 1) % n
                faces.append(self.bm.faces.new((grid[r][c], grid[r][c2], grid[r + 1][c2], grid[r + 1][c])))
        if cap_top:
            faces.append(self.bm.faces.new(list(reversed(grid[0]))))
        if cap_bottom:
            faces.append(self.bm.faces.new(grid[-1]))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        verts = [v for row in grid for v in row]
        return self._finish(verts, group, mat, Matrix.Identity(4), smooth=smooth)


def _ellipse(cx, cy, z, rx, ry, n, start=0.0, end=2 * math.pi, closed=True):
    count = n if closed else n + 1
    step = (end - start) / n
    return [(cx + rx * math.sin(start + i * step), cy - ry * math.cos(start + i * step), z) for i in range(count)]


def _lerp(a, b, t):
    return a + (b - a) * t


def _smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


# --------------------------------------------------------------------------------------------
# Parts. The character faces -Y; +X is its left. Bone positions are in rig.py.
# --------------------------------------------------------------------------------------------

def _helmet(mb):
    # Neck seal and gorget.
    mb.cylinder((0, 0.01, 1.44), (0, 0.01, 1.56), 0.065, 0.06, "Neck", "Suit", segments=8)
    # Tall bucket helm: rounded box, a bit deeper than wide, slightly narrowing toward the crown.
    rings = []
    profile = [  # z, half width, half depth, forward offset
        (1.905, 0.050, 0.060, 0.005),
        (1.89, 0.085, 0.098, 0.000),
        (1.85, 0.105, 0.120, -0.005),
        (1.76, 0.112, 0.128, -0.010),
        (1.65, 0.113, 0.130, -0.012),
        (1.57, 0.106, 0.124, -0.010),
        (1.54, 0.094, 0.110, -0.004),
    ]
    for z, hw, hd, fwd in profile:
        ring = []
        for i in range(12):
            a = 2 * math.pi * i / 12
            # Superellipse so the helm reads boxy, like the concept.
            sx, cy = math.sin(a), math.cos(a)
            px = hw * math.copysign(abs(sx) ** 0.6, sx)
            py = hd * math.copysign(abs(cy) ** 0.6, cy)
            ring.append((px, 0.02 - py + fwd, z))
        rings.append(ring)
    mb.loft(rings, "Head", "Helm")
    # Face plate: a flat bevelled slab on the front, angled back at the top.
    # Dark face plate, narrowing toward the chin, with cyan slits down both edges.
    face = [(-0.075, 1.555), (0.075, 1.555), (0.095, 1.70), (0.08, 1.82), (-0.08, 1.82), (-0.095, 1.70)]
    face_m = Matrix.Translation((0, -0.112, 0)) @ Matrix.Rotation(math.radians(-6), 4, "X")
    mb.prism([(x, z - 1.70) for x, z in face], 0.03, "Head", "Plate",
             Matrix.Translation((0, 0, 1.70)) @ face_m)
    for s in (1, -1):
        mb.box((0.074 * s, -0.130, 1.69), (0.014, 0.012, 0.15), "Head", "Glow",
               rotation=Matrix.Rotation(math.radians(-8 * s), 4, "Y"))
        mb.box((0.045 * s, -0.130, 1.755), (0.04, 0.010, 0.011), "Head", "Glow")
    # Side ear pods with a small cyan light.
    for s in (1, -1):
        mb.cylinder((0.108 * s, 0.03, 1.69), (0.13 * s, 0.03, 1.69), 0.038, 0.034, "Head", "Plate", segments=8)
        mb.box((0.132 * s, 0.03, 1.69), (0.008, 0.012, 0.034), "Head", "Glow")
    # The tall antenna fin rising from the right side of the crown, swept slightly back.
    fin = [(0.0, 0.0), (0.05, 0.0), (0.03, 0.14), (0.008, 0.26), (-0.008, 0.14)]
    fin_m = (Matrix.Translation((-0.085, 0.04, 1.80)) @ Matrix.Rotation(math.radians(-12), 4, "X")
             @ Matrix.Rotation(math.radians(6), 4, "Y") @ Matrix.Rotation(math.radians(90), 4, "Z"))
    mb.prism(fin, 0.02, "Head", "Plate", fin_m)
    # Crown seam.
    mb.box((0, 0.02, 1.90), (0.026, 0.14, 0.016), "Head", "Grey")


def _torso(mb):
    # Dark undersuit, chest plate and belt.
    mb.box((0, 0.01, 1.35), (0.38, 0.24, 0.22), "Chest", "Suit", taper=(1.1, 1.0), bevel=0.02)
    mb.box((0, -0.105, 1.36), (0.30, 0.05, 0.18), "Chest", "Plate", taper=(1.05, 1.0), bevel=0.012)
    mb.box((0, 0.01, 1.18), (0.32, 0.22, 0.16), "Spine", "Suit", taper=(1.12, 1.05), bevel=0.015)
    mb.box((0, 0.0, 1.06), (0.33, 0.23, 0.07), "Hips", "Leather")
    mb.box((0, -0.118, 1.06), (0.07, 0.02, 0.055), "Hips", "Grey")
    mb.box((0.1, -0.118, 1.05), (0.06, 0.03, 0.07), "Hips", "Plate")  # pouch
    mb.box((0, 0, 0.98), (0.30, 0.21, 0.12), "Hips", "Suit")
    # Bandolier strap from the right shoulder to the left hip, over the coat.
    strap = [(-0.02, 0.0), (0.02, 0.0), (0.02, 0.42), (-0.02, 0.42)]
    for y, rot in ((-0.128, 0), (0.135, 180)):
        m = (Matrix.Translation((0.0, y, 1.24)) @ Matrix.Rotation(math.radians(rot), 4, "Z")
             @ Matrix.Rotation(math.radians(-38), 4, "Y") @ Matrix.Translation((0, 0, -0.21)))
        mb.prism(strap, 0.012, "Chest", "Leather", m)


def _coat(mb):
    """Bone-grey surcoat over the suit, split front and back, with a ragged hem around the knees."""
    rng = random.Random(7)
    for side in (1, -1):
        for face in (-1, 1):  # front (-Y), back (+Y)
            rows = []
            for zi, z in enumerate((1.13, 0.98, 0.80, 0.62)):
                t = zi / 3
                row = []
                for xi in range(4):
                    x = side * _lerp(0.012, 0.19 + 0.05 * t, xi / 3)
                    y = face * (0.122 + 0.025 * t) - (0.0 if face < 0 else 0.0)
                    zz = z
                    if zi == 3:
                        zz += rng.uniform(-0.06, 0.05)
                    row.append((x, y, zz))
                rows.append(row)
            # Wrap the outer edge around the hip.
            for row in rows:
                x, y, z = row[-1]
                row.append((x + side * 0.02, y * 0.35, z + 0.02))
            verts = mb.sheet(rows, "Hips", "Coat", thickness=0.01)
            leg = "UpperLeg.L" if side > 0 else "UpperLeg.R"
            mb.weigh(verts, lambda co, leg=leg: {"Hips": 1.0, leg: _smooth((1.0 - co.z) / 0.35) * 1.2})


def _cloak(mb):
    """Tattered red cloak: wraps over both shoulders, open at the front, long and torn at the back."""
    rng = random.Random(11)
    cols = 17
    heights = [1.53, 1.44, 1.32, 1.16, 0.98, 0.80, 0.60, 0.40]
    last = len(heights) - 1
    rows = []
    for ri, z in enumerate(heights):
        t = ri / last
        rx = _lerp(0.25, 0.33, _smooth(t * 2.5)) + 0.06 * t
        ry = _lerp(0.17, 0.27, _smooth(t * 1.3))
        cy = _lerp(0.035, 0.15, t)
        arc = math.radians(_lerp(150, 112, _smooth(t * 1.6)))  # closes in over the shoulders
        row = []
        for c in range(cols):
            a = -arc + 2 * arc * c / (cols - 1)
            x = rx * math.sin(a)
            y = cy + ry * math.cos(a)
            front = _smooth((abs(a) - math.radians(80)) / math.radians(50))
            y -= 0.04 * front * t  # front panels hang forward of the arms
            zz = z
            if ri == last:
                # Torn hem: long strips with ragged gaps, longest at the back, shorter at the front.
                zz += -0.08 * math.cos(a) + 0.24 * front
                zz += rng.uniform(0.10, 0.26) if c % 2 else rng.uniform(-0.10, 0.0)
            elif ri == last - 1:
                zz += 0.12 * front + (rng.uniform(0.0, 0.10) if c % 4 == 1 else rng.uniform(-0.03, 0.02))
            elif ri == last - 2:
                zz += 0.05 * front
            fold = 0.012 * (1 if c % 2 else -1) * min(1.0, t * 2) * (1 + 0.6 * math.sin(c * 1.7))
            row.append((x * (1 + fold), y + abs(math.cos(a)) * fold, zz))
        rows.append(row)

    verts = mb.sheet(rows, "Chest", "Cloak", thickness=0.016)

    def cloak_weights(co):
        h = co.z
        w = {
            "Chest": _smooth((h - 1.05) / 0.3),
            "Spine": max(0.0, 1 - abs(h - 1.15) / 0.2) * 0.6,
            "Hips": _smooth((1.25 - h) / 0.3),
        }
        # The lower cape follows the legs a little so they do not punch through when running.
        legs = _smooth((0.95 - h) / 0.5) * 0.45
        side = _smooth((co.x + 0.12) / 0.24)
        w["UpperLeg.L"] = legs * side
        w["UpperLeg.R"] = legs * (1 - side)
        # The parts draped over the arms ride along with them.
        arm = _smooth((abs(co.x) - 0.2) / 0.1) * _smooth((h - 0.9) / 0.3) * _smooth((0.1 - co.y) / 0.15) * 0.35
        w["UpperArm.L" if co.x > 0 else "UpperArm.R"] = arm
        return w

    mb.weigh(verts, cloak_weights)

    # Cowl: a thick, bunched wrap around the neck that rides up the lower helm.
    rings = []
    for z, r, ry, bunch in ((1.66, 0.120, 0.135, 0.0), (1.60, 0.175, 0.18, 0.02), (1.52, 0.255, 0.23, 0.03),
                            (1.44, 0.27, 0.22, 0.02), (1.40, 0.22, 0.18, 0.0)):
        ring = []
        for i in range(14):
            a = 2 * math.pi * i / 14
            k = 1 + bunch * (1 if i % 2 else -1) + bunch * math.sin(a * 3 + z * 20)
            ring.append((r * k * math.sin(a), 0.03 - ry * k * math.cos(a), z + (0.02 if i % 3 == 0 else 0)))
        rings.append(ring)
    scarf = mb.loft(rings, "Chest", "Cloak", cap_top=True, cap_bottom=True)
    mb.weigh(scarf, lambda co: {"Chest": 1.0, "Neck": _smooth((co.z - 1.50) / 0.14) * 0.8})
    # Hood bunched behind the head.
    hood = [
        [(-0.10, 0.12, 1.66), (-0.04, 0.17, 1.70), (0.04, 0.17, 1.70), (0.10, 0.12, 1.66)],
        [(-0.15, 0.16, 1.55), (-0.06, 0.24, 1.58), (0.06, 0.24, 1.58), (0.15, 0.16, 1.55)],
        [(-0.17, 0.20, 1.45), (-0.06, 0.27, 1.42), (0.06, 0.27, 1.43), (0.17, 0.20, 1.45)],
    ]
    h_verts = mb.sheet(hood, "Chest", "CloakDark", thickness=0.03)
    mb.weigh(h_verts, lambda co: {"Chest": 1.0, "Neck": _smooth((co.z - 1.5) / 0.15) * 0.6})
    # Scarf tail hanging over the right chest, narrowing to a torn point.
    tail = [
        [(-0.19, -0.17, 1.50), (-0.13, -0.20, 1.50), (-0.06, -0.21, 1.50)],
        [(-0.21, -0.19, 1.34), (-0.15, -0.21, 1.33), (-0.09, -0.21, 1.36)],
        [(-0.22, -0.18, 1.18), (-0.17, -0.20, 1.12), (-0.12, -0.19, 1.20)],
        [(-0.22, -0.17, 1.08), (-0.20, -0.18, 0.98), (-0.17, -0.18, 1.10)],
    ]
    t_verts = mb.sheet(tail, "Chest", "Cloak", thickness=0.012)
    mb.weigh(t_verts, lambda co: {"Chest": _smooth((co.z - 1.0) / 0.25) + 0.05, "Spine": _smooth((1.25 - co.z) / 0.3)})


def _backpack(mb):
    """Tall, narrow tech pack strapped over the cloak between the shoulder blades."""
    mb.box((0, 0.245, 1.31), (0.17, 0.10, 0.32), "Chest", "Plate", taper=(0.85, 0.85), bevel=0.015)
    mb.box((0, 0.30, 1.29), (0.11, 0.03, 0.22), "Chest", "Grey", bevel=0.006)
    mb.box((0.03, 0.317, 1.30), (0.014, 0.01, 0.15), "Chest", "Glow")
    mb.box((-0.025, 0.317, 1.22), (0.035, 0.01, 0.012), "Chest", "Glow")
    # Side power cells and a top cap.
    for s in (1, -1):
        mb.box((0.095 * s, 0.25, 1.30), (0.035, 0.07, 0.22), "Chest", "Suit", bevel=0.008)
        mb.box((0.114 * s, 0.25, 1.33), (0.006, 0.012, 0.07), "Chest", "Glow")
    mb.box((0, 0.24, 1.48), (0.12, 0.08, 0.04), "Chest", "Plate", bevel=0.01)
    mb.cylinder((0.05, 0.25, 1.49), (0.06, 0.27, 1.60), 0.008, 0.006, "Chest", "Suit", segments=6)


def _arms(mb):
    for side, s in (("L", 1), ("R", -1)):
        # Pauldron under the cloak, padded upper arm, armoured forearm and a heavy gauntlet.
        mb.sphere((0.225 * s, 0.0, 1.42), 0.075, "UpperArm." + side, "Plate", scale=(1.0, 1.05, 0.85), segments=8)
        mb.cylinder((0.225 * s, 0.0, 1.40), (0.27 * s, 0.01, 1.16), 0.06, 0.054, "UpperArm." + side, "Suit")
        mb.box((0.255 * s, -0.04, 1.27), (0.06, 0.03, 0.13), "UpperArm." + side, "Grey", bevel=0.008)
        mb.sphere((0.27 * s, 0.01, 1.15), 0.056, "LowerArm." + side, "Plate", segments=8)
        mb.cylinder((0.27 * s, 0.01, 1.14), (0.29 * s, -0.02, 0.93), 0.056, 0.064, "LowerArm." + side, "Plate")
        mb.box((0.29 * s, -0.05, 1.03), (0.07, 0.03, 0.15), "LowerArm." + side, "Grey", bevel=0.008)
        mb.cylinder((0.29 * s, -0.02, 0.95), (0.295 * s, -0.025, 0.90), 0.07, 0.07, "LowerArm." + side, "Suit")
        mb.box((0.30 * s, -0.035, 0.85), (0.085, 0.10, 0.12), "Hand." + side, "Plate", bevel=0.012)
        mb.box((0.30 * s, -0.065, 0.80), (0.08, 0.045, 0.06), "Hand." + side, "Grey", bevel=0.008)


def _bracer(mb):
    """Tech bracer on the left forearm. It replaces the shield and catches blows in the guard pose."""
    plate = [(-0.06, -0.13), (0.06, -0.13), (0.075, 0.0), (0.06, 0.13), (-0.06, 0.13), (-0.075, 0.0)]
    m = Matrix.Translation((0.345, -0.01, 1.03)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    mb.prism(plate, 0.025, "Shield.L", "Plate", m)
    mb.box((0.36, -0.01, 1.03), (0.006, 0.012, 0.16), "Shield.L", "Glow")


def _legs(mb):
    for side, s in (("L", 1), ("R", -1)):
        # Loose dark trousers.
        mb.cylinder((0.10 * s, 0, 0.98), (0.11 * s, 0, 0.56), 0.092, 0.076, "UpperLeg." + side, "Suit")
        mb.sphere((0.11 * s, -0.04, 0.55), 0.06, "LowerLeg." + side, "Plate", scale=(1, 0.9, 1.15), segments=8)
        mb.cylinder((0.11 * s, 0.0, 0.56), (0.11 * s, 0.01, 0.44), 0.076, 0.07, "LowerLeg." + side, "Suit")
        # Tall boots with a folded cuff and a shin plate.
        mb.cylinder((0.11 * s, 0.01, 0.46), (0.11 * s, 0.02, 0.12), 0.074, 0.064, "LowerLeg." + side, "Boot")
        mb.cylinder((0.11 * s, 0.01, 0.47), (0.11 * s, 0.01, 0.42), 0.082, 0.08, "LowerLeg." + side, "Boot")
        mb.box((0.11 * s, -0.06, 0.30), (0.07, 0.03, 0.2), "LowerLeg." + side, "Plate", bevel=0.008)
        mb.box((0.11 * s, -0.045, 0.055), (0.12, 0.28, 0.11), "Foot." + side, "Boot", taper=(0.9, 0.82), bevel=0.012)
        mb.box((0.11 * s, -0.045, 0.01), (0.125, 0.29, 0.02), "Foot." + side, "Suit")


def build_mesh(rig, mats):
    bone_names = [b.name for b in rig.data.bones if b.use_deform]
    mb = TechBuilder(bone_names, list(mats.keys()))
    _helmet(mb)
    _torso(mb)
    _coat(mb)
    _cloak(mb)
    _backpack(mb)
    _arms(mb)
    _bracer(mb)
    _legs(mb)

    obj = mb.to_object("TechKnight_Mesh", list(mats.values()))
    obj.parent = rig
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    return obj
