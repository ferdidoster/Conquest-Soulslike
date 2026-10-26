"""
Low-poly mesh building blocks. Every kit piece is built with one MeshBuilder: primitives are
added in piece space (Blender: Z up, front faces -Y, pivot on the ground at the origin), each
with a palette material, and finish() turns the result into one flat-shaded object.

Per-face colour jitter is written to the "Col" attribute (Unity multiplies it into the albedo,
which gives the hand-painted, chiselled look). Its alpha carries the wind-sway weight that the
toon shader uses for cloth: 0 where cloth is attached, 1 at the free edge.
"""

import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

from . import palette


def _xf(loc=(0, 0, 0), rot_z=0.0, rot_x=0.0, rot_y=0.0):
    """Matrix from a location and Euler angles in degrees."""
    return (Matrix.Translation(Vector(loc))
            @ Matrix.Rotation(math.radians(rot_z), 4, "Z")
            @ Matrix.Rotation(math.radians(rot_y), 4, "Y")
            @ Matrix.Rotation(math.radians(rot_x), 4, "X"))


class MeshBuilder:
    def __init__(self, name, seed=None):
        self.name = name
        self.bm = bmesh.new()
        self.sway = self.bm.verts.layers.float.new("sway")
        self.materials = []
        self.rng = random.Random(seed if seed is not None else hash(name) & 0xFFFF)

    # ------------------------------------------------------------------ bookkeeping
    def _mat(self, name):
        if name not in self.materials:
            self.materials.append(name)
        return self.materials.index(name)

    def _begin(self):
        """Snapshot of the existing geometry. BMesh reuses freed slots (e.g. after a bevel), so
        new elements are found by exclusion rather than by index."""
        return set(self.bm.verts), set(self.bm.faces)

    def _end(self, mark, mat, matrix=None, bevel=0.0):
        old_verts, old_faces = mark
        verts = [v for v in self.bm.verts if v not in old_verts]
        faces = [f for f in self.bm.faces if f not in old_faces]
        if matrix is not None:
            bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        idx = self._mat(mat)
        for f in faces:
            f.material_index = idx
        if bevel > 0:
            edges = {e for f in faces for e in f.edges}
            result = bmesh.ops.bevel(self.bm, geom=list(edges), offset=bevel, segments=1,
                                     affect="EDGES", clamp_overlap=True)
            for f in result.get("faces", []):
                f.material_index = idx
            verts = [v for v in self.bm.verts if v not in old_verts]
        return verts

    # ------------------------------------------------------------------ primitives
    def box(self, size, loc=(0, 0, 0), mat=palette.STONE, rot_z=0.0, rot_x=0.0, rot_y=0.0, bevel=0.0,
            base=True, taper=1.0):
        """Box of `size`; `loc` is the bottom centre when base=True, else the centre.
        `taper` scales the top face (1 = straight sides)."""
        sx, sy, sz = size
        mark = self._begin()
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        for v in verts:
            top = v.co.z > 0
            k = taper if top else 1.0
            v.co = Vector((v.co.x * sx * k, v.co.y * sy * k, (v.co.z + (0.5 if base else 0.0)) * sz))
        return self._end(mark, mat, _xf(loc, rot_z, rot_x, rot_y), bevel)

    def cylinder(self, radius, depth, loc=(0, 0, 0), mat=palette.METAL, segments=8, radius_top=None,
                 rot_z=0.0, rot_x=0.0, rot_y=0.0, base=True, bevel=0.0):
        mark = self._begin()
        r2 = radius if radius_top is None else radius_top
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                              radius1=radius, radius2=r2, depth=depth)
        m = _xf(loc, rot_z, rot_x, rot_y) @ Matrix.Translation((0, 0, depth / 2 if base else 0))
        return self._end(mark, mat, m, bevel)

    def cone(self, radius1, radius2, depth, matrix, mat=palette.METAL, segments=10):
        """Centred cone/cylinder along local Z, placed by an arbitrary matrix."""
        mark = self._begin()
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                              radius1=radius1, radius2=radius2, depth=depth)
        return self._end(mark, mat, matrix)

    def prism(self, points, z0, z1, mat=palette.ROCK, loc=(0, 0, 0), rot_z=0.0, top_scale=1.0, bevel=0.0,
              matrix=None):
        """Extrudes a 2D polygon (counter-clockwise) from z0 to z1; top_scale shrinks the top."""
        mark = self._begin()
        bottom = [self.bm.verts.new((x, y, z0)) for x, y in points]
        top = [self.bm.verts.new((x * top_scale, y * top_scale, z1)) for x, y in points]
        n = len(points)
        self.bm.faces.new(list(reversed(bottom)))
        self.bm.faces.new(top)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
        return self._end(mark, mat, matrix if matrix is not None else _xf(loc, rot_z), bevel)

    def ring_segment(self, r_in, r_out, z0, z1, a0, a1, mat=palette.STONE, segments=None, bevel=0.0,
                     matrix=None):
        """Annular wedge between angles a0..a1 (degrees, counter-clockwise from +X)."""
        if segments is None:
            segments = max(1, int(abs(a1 - a0) / 7.5))
        mark = self._begin()
        rows = []
        for i in range(segments + 1):
            a = math.radians(a0 + (a1 - a0) * i / segments)
            c, s = math.cos(a), math.sin(a)
            rows.append([self.bm.verts.new((c * r, s * r, z)) for r, z in
                         ((r_in, z0), (r_out, z0), (r_out, z1), (r_in, z1))])
        for i in range(segments):
            a, b = rows[i], rows[i + 1]
            for k in range(4):
                k2 = (k + 1) % 4
                self.bm.faces.new((a[k], b[k], b[k2], a[k2]))
        self.bm.faces.new(list(reversed(rows[0])))
        self.bm.faces.new(rows[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f not in mark[1]])
        return self._end(mark, mat, matrix, bevel)

    def disc(self, radius, z0, z1, mat=palette.SAND, segments=32, loc=(0, 0, 0)):
        return self.cylinder(radius, z1 - z0, (loc[0], loc[1], z0), mat, segments)

    def sheet(self, grid, mat=palette.CLOTH, sway=None):
        """Double-sided cloth from a grid of points grid[row][col]; sway[row][col] weights."""
        mark = self._begin()
        rows = [[self.bm.verts.new(p) for p in row] for row in grid]
        for r in range(len(rows) - 1):
            for c in range(len(rows[r]) - 1):
                self.bm.faces.new((rows[r][c], rows[r][c + 1], rows[r + 1][c + 1], rows[r + 1][c]))
        if sway is not None:
            for r, row in enumerate(rows):
                for c, v in enumerate(row):
                    v[self.sway] = sway[r][c]
        return self._end(mark, mat)

    def tube(self, points, radius, mat=palette.METAL_DARK, sides=5):
        """A tube along a polyline (cables, pipes, ropes)."""
        mark = self._begin()
        rings = []
        for i, p in enumerate(points):
            p = Vector(p)
            d = (Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
            side = d.cross(Vector((0, 0, 1)))
            if side.length < 1e-4:
                side = Vector((1, 0, 0))
            side.normalize()
            up = side.cross(d).normalized()
            ring = []
            for k in range(sides):
                a = 2 * math.pi * k / sides
                ring.append(self.bm.verts.new(p + (side * math.cos(a) + up * math.sin(a)) * radius))
            rings.append(ring)
        for i in range(len(rings) - 1):
            for k in range(sides):
                k2 = (k + 1) % sides
                self.bm.faces.new((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]))
        self.bm.faces.new(list(reversed(rings[0])))
        self.bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f not in mark[1]])
        return self._end(mark, mat)

    def ribbon(self, points, width, z=0.004, mat=palette.CRACK):
        """Flat strip lying on the ground along a 2D/3D polyline: painted lines, cracks, seams."""
        mark = self._begin()
        left, right = [], []
        for i, p in enumerate(points):
            a = Vector(points[max(i - 1, 0)][:2])
            c = Vector(points[min(i + 1, len(points) - 1)][:2])
            d = (c - a).normalized()
            n = Vector((-d.y, d.x)) * width / 2
            left.append(self.bm.verts.new((p[0] + n.x, p[1] + n.y, z)))
            right.append(self.bm.verts.new((p[0] - n.x, p[1] - n.y, z)))
        for i in range(len(points) - 1):
            self.bm.faces.new((right[i], right[i + 1], left[i + 1], left[i]))
        return self._end(mark, mat)

    # ------------------------------------------------------------------ composites
    def strata_rock(self, radius, height, layers=4, taper=0.75, loc=(0, 0, 0), mat=palette.ROCK,
                    sides=8, jitter=0.18, z_start=0.0, ledge=0.92, accent=None):
        """Stacked, slightly shrinking irregular prisms: the layered mesa look of the concept.
        Every other layer can use the `accent` material for visible strata."""
        rng = self.rng
        h = height / layers
        r = radius
        phase = rng.uniform(0, math.pi * 2)
        shrink = taper ** (1.0 / max(layers - 1, 1))
        for i in range(layers):
            last = i == layers - 1
            pts = []
            for k in range(sides):
                a = phase + 2 * math.pi * k / sides + rng.uniform(-0.2, 0.2)
                rr = r * rng.uniform(1 - jitter, 1 + jitter)
                pts.append((math.cos(a) * rr, math.sin(a) * rr))
            z0 = z_start + i * h
            z1 = z_start + height if last else z0 + h * rng.uniform(1.0, 1.08)
            m = accent if (accent and i % 2 == 1) else mat
            self.prism(pts, z0, z1, m, loc, top_scale=0.8 if last else shrink)
            r = r * shrink * ledge
        return self

    def boulder(self, radius, loc=(0, 0, 0), mat=palette.ROCK, squash=0.7, detail=1):
        mark = self._begin()
        bmesh.ops.create_icosphere(self.bm, subdivisions=detail, radius=1.0)
        verts = [v for v in self.bm.verts if v not in mark[0]]
        rng = self.rng
        for v in verts:
            v.co *= rng.uniform(0.8, 1.15)
            v.co.z = max(v.co.z * squash, -0.15)
        m = _xf((loc[0], loc[1], loc[2] + radius * 0.1), rng.uniform(0, 360)) @ Matrix.Scale(radius, 4)
        return self._end(mark, mat, m)

    def block_ring(self, r_in, r_out, z0, z1, a0, a1, blocks, mat=palette.STONE, gap=0.06, bevel=0.04,
                   jitter=0.0):
        """A curved wall made of separate stone blocks (gaps read as mortar lines)."""
        step = (a1 - a0) / blocks
        for i in range(blocks):
            g = math.degrees(gap / ((r_in + r_out) / 2))
            dz = self.rng.uniform(-jitter, jitter)
            self.ring_segment(r_in, r_out, z0, z1 + dz, a0 + i * step + g / 2, a0 + (i + 1) * step - g / 2,
                              mat, segments=max(1, int(step / 5)), bevel=bevel)

    def flame(self, loc, scale=1.0):
        """A low cluster of flat tongues in the emissive flame colour, sitting in a bowl."""
        tongues = ((0.0, 0.0, 0.52, 0.17), (0.09, 0, 0.36, 0.12), (0.09, 120, 0.4, 0.12), (0.09, 240, 0.32, 0.11),
                   (0.15, 60, 0.24, 0.09), (0.15, 300, 0.22, 0.08))
        for dist, a, h, r in tongues:
            a += self.rng.uniform(-20, 20)
            off = (math.cos(math.radians(a)) * dist * scale, math.sin(math.radians(a)) * dist * scale)
            self.cylinder(r * scale, h * scale * self.rng.uniform(0.85, 1.1),
                          (loc[0] + off[0], loc[1] + off[1], loc[2]), palette.FLAME,
                          segments=4, radius_top=0.0, rot_z=a + 45)

    def glyph(self, center, height, depth_y, mat=palette.BONE, width=0.06):
        """The banner emblem from the concept: a vertical stroke, a ring and two bars, as thin
        strips laid on a cloth plane facing -Y. Returns nothing; strips sway with the cloth."""
        cx, cz = center
        y = depth_y
        h = height

        def strip(x0, z0, x1, z1):
            d = Vector((x1 - x0, 0, z1 - z0))
            n = Vector((-d.z, 0, d.x)).normalized() * width / 2
            pts = [(x0 + n.x, y, z0 + n.z), (x1 + n.x, y, z1 + n.z), (x1 - n.x, y, z1 - n.z), (x0 - n.x, y, z0 - n.z)]
            mark = self._begin()
            vs = [self.bm.verts.new(p) for p in pts]
            self.bm.faces.new(vs)
            self._end(mark, mat)

        strip(cx, cz - h * 0.5, cx, cz + h * 0.5)
        strip(cx - h * 0.12, cz + h * 0.3, cx + h * 0.12, cz + h * 0.3)
        strip(cx - h * 0.18, cz - h * 0.32, cx + h * 0.18, cz - h * 0.32)
        ring_r = h * 0.16
        prev = None
        for k in range(9):
            a = 2 * math.pi * k / 8
            p = (cx + math.cos(a) * ring_r, cz - h * 0.05 + math.sin(a) * ring_r)
            if prev:
                strip(prev[0], prev[1], p[0], p[1])
            prev = p

    # ------------------------------------------------------------------ output
    def finish(self, value_jitter=0.1, collection=None):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)
        # Unity fan-triangulates n-gons, which breaks concave ones; triangulate them here.
        ngons = [f for f in bm.faces if len(f.verts) > 4]
        if ngons:
            bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
        mesh = bpy.data.meshes.new(self.name)
        # Per-face colour jitter + per-vertex sway in alpha.
        col = bm.loops.layers.float_color.new("Col")
        rng = self.rng
        for f in bm.faces:
            v = 1.0 - rng.uniform(0.0, value_jitter)
            for loop in f.loops:
                loop[col] = (v, v, v, loop.vert[self.sway])
        bm.to_mesh(mesh)
        bm.free()
        for poly in mesh.polygons:
            poly.use_smooth = False
        obj = bpy.data.objects.new(self.name, mesh)
        for m in self.materials:
            mesh.materials.append(palette.material(m))
        (collection or bpy.context.scene.collection).objects.link(obj)
        return obj


def sag_curve(p0, p1, sag, segments):
    """Points of a hanging rope from p0 to p1 sagging by `sag` metres in the middle."""
    p0, p1 = Vector(p0), Vector(p1)
    pts = []
    for i in range(segments + 1):
        t = i / segments
        p = p0.lerp(p1, t)
        p.z -= sag * 4 * t * (1 - t)
        pts.append(tuple(p))
    return pts
