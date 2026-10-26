"""
The desert palette, taken from the boss arena concept art. Every kit piece uses these
materials by name; Unity builds one shared toon material per entry (see DesertKit.json), so
recolouring the whole kit is a matter of editing a material in Unity or a value here.

Colours are sRGB hex. `grit` is the strength of the procedural wear noise in the toon shader,
`sway` the wind amplitude for cloth, `emission` an HDR intensity for glowing parts.
"""

import bpy

SAND = "Kit_Sand"
SAND_DARK = "Kit_SandDark"
SAND_DEEP = "Kit_SandDeep"
CRACK = "Kit_Crack"
INLAY = "Kit_Inlay"
ROCK = "Kit_Rock"
ROCK_LIGHT = "Kit_RockLight"
ROCK_DARK = "Kit_RockDark"
STONE = "Kit_Stone"
STONE_LIGHT = "Kit_StoneLight"
STONE_DARK = "Kit_StoneDark"
STONE_TEMPLE = "Kit_StoneTemple"
METAL = "Kit_Metal"
METAL_DARK = "Kit_MetalDark"
RUST = "Kit_Rust"
CLOTH = "Kit_Cloth"
CLOTH_DARK = "Kit_ClothDark"
BONE = "Kit_Bone"
BRONZE = "Kit_Bronze"
VOID = "Kit_Void"
FLAME = "Kit_Flame"
TECH_LIGHT = "Kit_TechLight"
FLOOR_DISC = "Kit_FloorDisc"
SANDSTONE = "Kit_Sandstone"
SANDSTONE_DARK = "Kit_SandstoneDark"

ENTRIES = {
    SAND:         dict(color="#ECC3A4", grit=0.18),
    SAND_DARK:    dict(color="#D9A68A", grit=0.22),
    SAND_DEEP:    dict(color="#A87A78", grit=0.25),
    CRACK:        dict(color="#A5705F", grit=0.0),
    INLAY:        dict(color="#C98A80", grit=0.12),
    ROCK:         dict(color="#C2675C", grit=0.28),
    ROCK_LIGHT:   dict(color="#DB9275", grit=0.26),
    ROCK_DARK:    dict(color="#8F5560", grit=0.28),
    STONE:        dict(color="#A0908F", grit=0.26),
    STONE_LIGHT:  dict(color="#BFAAA0", grit=0.24),
    STONE_DARK:   dict(color="#6F6168", grit=0.28),
    STONE_TEMPLE: dict(color="#958589", grit=0.3),
    METAL:        dict(color="#999BA4", grit=0.18),
    METAL_DARK:   dict(color="#4A4C58", grit=0.18),
    RUST:         dict(color="#9B5B3F", grit=0.32),
    CLOTH:        dict(color="#A8323C", grit=0.06, sway=0.18, double_sided=True),
    CLOTH_DARK:   dict(color="#72232F", grit=0.06, sway=0.18, double_sided=True),
    BONE:         dict(color="#EEDFC8", grit=0.03, sway=0.18, double_sided=True),
    BRONZE:       dict(color="#8C6A3C", grit=0.22),
    VOID:         dict(color="#2A2330", grit=0.0),
    FLAME:        dict(color="#FF8A2E", emission="#FF5A14", emission_intensity=0.7, grit=0.0),
    FLOOR_DISC:   dict(color="#DDA08F", grit=0.14),
    SANDSTONE:    dict(color="#D98F6E", grit=0.26),
    SANDSTONE_DARK: dict(color="#B56C5C", grit=0.28),
    TECH_LIGHT:   dict(color="#FF7A45", emission="#FF4A1A", emission_intensity=1.4, grit=0.0),
}


def srgb_to_linear(hex_color):
    hex_color = hex_color.lstrip("#")
    out = []
    for i in range(0, 6, 2):
        c = int(hex_color[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return out


def material(name):
    """Blender material for a palette entry (flat colour, used for previews and the FBX slot name)."""
    mat = bpy.data.materials.get(name)
    if mat is not None:
        return mat
    entry = ENTRIES[name]
    mat = bpy.data.materials.new(name)
    r, g, b = srgb_to_linear(entry["color"])
    mat.diffuse_color = (r, g, b, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
        bsdf.inputs["Roughness"].default_value = 1.0
        if "emission" in entry:
            er, eg, eb = srgb_to_linear(entry["emission"])
            bsdf.inputs["Emission Color"].default_value = (er, eg, eb, 1.0)
            bsdf.inputs["Emission Strength"].default_value = entry["emission_intensity"]
    return mat


def manifest():
    """Palette section of DesertKit.json."""
    out = []
    for name, e in ENTRIES.items():
        out.append({
            "name": name,
            "color": e["color"],
            "emission": e.get("emission", "#000000"),
            "emissionIntensity": e.get("emission_intensity", 0.0),
            "grit": e.get("grit", 0.2),
            "sway": e.get("sway", 0.0),
            "doubleSided": e.get("double_sided", False),
        })
    return out
