"""Low-poly knight mesh, built from primitives. Every part is rigidly bound to one bone."""

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

PALETTE = {
    "Steel": ((0.62, 0.64, 0.68, 1), 0.9, 0.35),
    "DarkSteel": ((0.18, 0.19, 0.22, 1), 0.85, 0.45),
    "Cloth": ((0.38, 0.05, 0.06, 1), 0.0, 0.85),
    "Leather": ((0.25, 0.15, 0.08, 1), 0.0, 0.7),
    "Gold": ((0.85, 0.62, 0.22, 1), 1.0, 0.3),
    "Visor": ((0.02, 0.02, 0.02, 1), 0.0, 0.9),
    "Wood": ((0.35, 0.22, 0.12, 1), 0.0, 0.8),
}


def make_materials():
    mats = {}
    for name, (color, metallic, roughness) in PALETTE.items():
        m = bpy.data.materials.new("Knight_" + name)
        m.diffuse_color = color
        m.metallic = metallic
        m.roughness = roughness
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = color
            bsdf.inputs["Metallic"].default_value = metallic
            bsdf.inputs["Roughness"].default_value = roughness
        mats[name] = m
    return mats


class MeshBuilder:
    def __init__(self, group_names, material_names):
        self.bm = bmesh.new()
        self.deform = self.bm.verts.layers.deform.verify()
        self.groups = {n: i for i, n in enumerate(group_names)}
        self.mats = {n: i for i, n in enumerate(material_names)}

    # -- helpers -------------------------------------------------------------------------

    def _finish(self, geom, group, mat, matrix, smooth):
        verts = [g for g in geom if isinstance(g, bmesh.types.BMVert)]
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        gi = self.groups[group]
        for v in verts:
            v[self.deform][gi] = 1.0
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.material_index = self.mats[mat]
            f.smooth = smooth
        if smooth:
            # Keep caps crisp: edges between faces facing very different ways are sharp.
            for e in {e for f in faces for e in f.edges}:
                if len(e.link_faces) == 2 and e.link_faces[0].normal.angle(e.link_faces[1].normal, 0) > math.radians(50):
                    e.smooth = False
        return verts

    # -- primitives ----------------------------------------------------------------------

    def box(self, center, size, group, mat, taper=(1.0, 1.0), rotation=None, bevel=0.0):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        for v in verts:
            if v.co.z > 0:
                v.co.x *= taper[0]
                v.co.y *= taper[1]
        if bevel > 0:
            edges = list({e for v in verts for e in v.link_edges})
            out = bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES")
            verts = list({v for f in out["faces"] for v in f.verts} | set(v for v in verts if v.is_valid))
        m = Matrix.Translation(center) @ (rotation or Matrix.Identity(4)) @ Matrix.Diagonal((*size, 1))
        return self._finish(verts, group, mat, m, smooth=False)

    def cylinder(self, a, b, r1, r2, group, mat, segments=8, smooth=True):
        a, b = Vector(a), Vector(b)
        axis = b - a
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segments, radius1=r1, radius2=r2,
                                    depth=axis.length)
        rot = Vector((0, 0, 1)).rotation_difference(axis.normalized()).to_matrix().to_4x4()
        m = Matrix.Translation((a + b) / 2) @ rot
        return self._finish(res["verts"], group, mat, m, smooth)

    def sphere(self, center, radius, group, mat, scale=(1, 1, 1), segments=10, rings=6, smooth=True):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segments, v_segments=rings, radius=radius)
        m = Matrix.Translation(center) @ Matrix.Diagonal((*scale, 1))
        return self._finish(res["verts"], group, mat, m, smooth)

    def prism(self, outline, thickness, group, mat, matrix):
        """Extrudes a 2D outline (in XZ) along Y by `thickness`, then applies `matrix`."""
        front = [self.bm.verts.new((x, -thickness / 2, z)) for x, z in outline]
        back = [self.bm.verts.new((x, thickness / 2, z)) for x, z in outline]
        self.bm.faces.new(list(reversed(front)))
        self.bm.faces.new(back)
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((front[i], front[j], back[j], back[i]))
        return self._finish(front + back, group, mat, matrix, smooth=False)

    def to_object(self, name, materials):
        mesh = bpy.data.meshes.new(name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mat in materials:
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for group_name in self.groups:
            obj.vertex_groups.new(name=group_name)
        return obj


def build_knight_mesh(rig, mats):
    bone_names = [b.name for b in rig.data.bones if b.use_deform]
    mb = MeshBuilder(bone_names, list(mats.keys()))

    # ---- Head / helmet (great helm) ----
    mb.cylinder((0, 0, 1.46), (0, 0, 1.56), 0.06, 0.06, "Neck", "DarkSteel")
    mb.cylinder((0, 0.0, 1.55), (0, 0.0, 1.80), 0.125, 0.115, "Head", "Steel", segments=10)
    mb.sphere((0, 0, 1.80), 0.116, "Head", "Steel", scale=(1, 1, 0.55), segments=10, rings=5)
    mb.box((0, -0.118, 1.69), (0.17, 0.02, 0.022), "Head", "Visor")  # eye slit
    mb.box((0, -0.117, 1.61), (0.025, 0.02, 0.07), "Head", "Visor")  # breath slit
    mb.box((0, -0.005, 1.86), (0.025, 0.25, 0.05), "Head", "Gold")   # crest ridge
    mb.cylinder((0, 0, 1.55), (0, 0, 1.585), 0.135, 0.13, "Head", "Gold", segments=10)  # rim

    # ---- Torso ----
    mb.box((0, 0.0, 1.355), (0.44, 0.27, 0.22), "Chest", "Steel", taper=(1.08, 1.0), bevel=0.02)
    mb.box((0, -0.12, 1.35), (0.05, 0.04, 0.2), "Chest", "Gold")  # sternum ridge
    mb.box((0, 0.0, 1.18), (0.36, 0.23, 0.17), "Spine", "Steel", taper=(1.15, 1.08), bevel=0.015)
    mb.box((0, -0.005, 1.08), (0.36, 0.24, 0.07), "Hips", "Leather")  # belt
    mb.box((0, -0.125, 1.08), (0.07, 0.02, 0.06), "Hips", "Gold")     # buckle
    mb.box((0, 0, 1.0), (0.32, 0.21, 0.12), "Hips", "DarkSteel", taper=(1.0, 1.0))
    # Tabard: front and back panels hang from the belt.
    mb.box((0, -0.115, 0.86), (0.22, 0.02, 0.38), "Hips", "Cloth", taper=(1.15, 1.0))
    mb.box((0, 0.115, 0.86), (0.24, 0.02, 0.38), "Hips", "Cloth", taper=(1.1, 1.0))
    mb.box((0, -0.118, 0.97), (0.06, 0.02, 0.1), "Hips", "Gold")

    # ---- Arms ----
    for side, s in (("L", 1), ("R", -1)):
        mb.sphere((0.24 * s, 0.0, 1.44), 0.105, "UpperArm." + side, "Steel", scale=(1.05, 1.0, 0.8), segments=8)
        mb.cylinder((0.225 * s, 0.0, 1.42), (0.27 * s, 0.01, 1.15), 0.058, 0.052, "UpperArm." + side, "DarkSteel")
        mb.sphere((0.27 * s, 0.01, 1.15), 0.058, "LowerArm." + side, "Steel", segments=8)
        mb.cylinder((0.27 * s, 0.01, 1.15), (0.29 * s, -0.02, 0.92), 0.05, 0.062, "LowerArm." + side, "Steel")
        mb.box((0.30 * s, -0.035, 0.85), (0.075, 0.09, 0.11), "Hand." + side, "DarkSteel", bevel=0.01)

    # ---- Legs ----
    for side, s in (("L", 1), ("R", -1)):
        mb.cylinder((0.10 * s, 0, 0.98), (0.11 * s, 0, 0.56), 0.085, 0.07, "UpperLeg." + side, "DarkSteel")
        mb.sphere((0.11 * s, -0.035, 0.54), 0.06, "LowerLeg." + side, "Steel", scale=(1, 0.9, 1), segments=8)
        mb.cylinder((0.11 * s, 0.0, 0.54), (0.11 * s, 0.02, 0.13), 0.068, 0.055, "LowerLeg." + side, "Steel")
        mb.box((0.11 * s, -0.04, 0.05), (0.11, 0.27, 0.1), "Foot." + side, "Leather", taper=(0.9, 0.8), bevel=0.01)

    # ---- Sword (on Weapon.R, pointing forward along -Y) ----
    mb.cylinder((-0.30, 0.04, 0.85), (-0.30, -0.10, 0.85), 0.018, 0.018, "Weapon.R", "Leather", segments=6)
    mb.sphere((-0.30, 0.055, 0.85), 0.03, "Weapon.R", "Gold", segments=6, rings=4)
    mb.box((-0.30, -0.11, 0.85), (0.22, 0.03, 0.03), "Weapon.R", "Gold")  # crossguard
    blade = [(-0.022, 0.0), (0.022, 0.0), (0.022, 0.80), (0.0, 0.88), (-0.022, 0.80)]
    # Outline length (Z) -> forward (-Y), outline width -> vertical, so the edge faces down.
    blade_m = (Matrix.Translation((-0.30, -0.12, 0.85)) @ Matrix.Rotation(math.radians(90), 4, "Y")
               @ Matrix.Rotation(math.radians(90), 4, "X"))
    mb.prism(blade, 0.008, "Weapon.R", "Steel", blade_m)

    # ---- Heater shield (on Shield.L, face looking outward +X) ----
    heater = [(-0.2, 0.25), (0.2, 0.25), (0.2, 0.0), (0.12, -0.2), (0.0, -0.32), (-0.12, -0.2), (-0.2, 0.0)]
    shield_m = Matrix.Translation((0.36, -0.01, 1.0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    mb.prism(heater, 0.03, "Shield.L", "Wood", shield_m)
    trim = [(x * 1.08, z * 1.06 + 0.005) for x, z in heater]
    mb.prism(trim, 0.018, "Shield.L", "Steel", Matrix.Translation((-0.012, 0, 0)) @ shield_m)
    mb.box((0.38, -0.01, 1.03), (0.012, 0.06, 0.34), "Shield.L", "Gold")   # cross, vertical
    mb.box((0.38, -0.01, 1.1), (0.012, 0.3, 0.05), "Shield.L", "Gold")     # cross, horizontal
    mb.sphere((0.385, -0.01, 1.08), 0.035, "Shield.L", "Steel", scale=(0.6, 1, 1), segments=8)

    obj = mb.to_object("Knight_Mesh", list(mats.values()))
    obj.parent = rig
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    return obj
