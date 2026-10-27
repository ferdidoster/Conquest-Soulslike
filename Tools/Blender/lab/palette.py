"""
The underground laboratory palette, in the game's own look rather than the dark concept sheets:
the desert arena's light, flat stone values turned cold (lavender-blue stone and steel instead of
sand and mauve), cyan glow from the containment tech, and one saturated accent per room (hazard
yellow, cloth red, terracotta rust) the way the arena uses its red banners. Every lab kit piece
uses these materials by name; Unity builds one shared toon material per entry (see LabKit.json),
so recolouring the whole kit is a matter of editing a material in Unity or a value here.

Colours are sRGB hex. `grit` is the strength of the procedural wear noise in the toon shader,
`emission` an HDR colour with `emission_intensity` for glowing parts. `shadow` is the toon
shadow tint (the desert kit keeps the shader's warm mauve default; the lab uses its cold twin).
"""

import bpy

FLOOR = "Lab_Floor"
FLOOR_LIGHT = "Lab_FloorLight"
FLOOR_DARK = "Lab_FloorDark"
GRATE = "Lab_Grate"
TILE = "Lab_Tile"
WALL = "Lab_Wall"
WALL_LIGHT = "Lab_WallLight"
WALL_DARK = "Lab_WallDark"
CONCRETE = "Lab_Concrete"
METAL = "Lab_Metal"
METAL_DARK = "Lab_MetalDark"
STEEL = "Lab_Steel"
PLASTIC = "Lab_Plastic"
RUST = "Lab_Rust"
HAZARD = "Lab_Hazard"
MARKING = "Lab_Marking"
PORCELAIN = "Lab_Porcelain"
FABRIC = "Lab_Fabric"
FABRIC_BLUE = "Lab_FabricBlue"
MATTRESS = "Lab_Mattress"
WOOD = "Lab_Wood"
VOID = "Lab_Void"
GLOW = "Lab_Glow"            # containment cyan
GLOW_SOFT = "Lab_GlowSoft"   # tube glass / liquid
SCREEN = "Lab_Screen"
LAMP_COLD = "Lab_LampCold"
LAMP_WARM = "Lab_LampWarm"
LIGHT_RED = "Lab_LightRed"
LIGHT_GREEN = "Lab_LightGreen"
SPECIMEN = "Lab_Specimen"
GRIME = "Lab_Grime"
GRIME_DARK = "Lab_GrimeDark"
PUDDLE = "Lab_Puddle"
TANK = "Lab_TankGlass"
CARDBOARD = "Lab_Cardboard"
GUNMETAL = "Lab_Gunmetal"
WOOD_GREY = "Lab_WoodGrey"

# Aliases so the shared MeshBuilder defaults (written for the desert kit) resolve to lab materials.
STONE = CONCRETE
ROCK = CONCRETE
SAND = FLOOR
CLOTH = FABRIC
CRACK = WALL_DARK
BONE = MARKING
FLAME = LAMP_WARM

COLD = "#8088AA"  # lavender-blue, the cold twin of the desert's mauve shadow

# Values sit in the same light band as the desert kit's stone (#A0908F, #BFAAA0) so the ink
# lines and cel shadows read; the dark tones are kept for trims and voids only.
ENTRIES = {
    FLOOR:       dict(color="#8E93A3", grit=0.2, shadow=COLD),
    FLOOR_LIGHT: dict(color="#989DAD", grit=0.18, shadow=COLD),
    FLOOR_DARK:  dict(color="#6B7082", grit=0.16, shadow=COLD),
    GRATE:       dict(color="#737889", grit=0.14, shadow=COLD),
    TILE:        dict(color="#B8BCC7", grit=0.14, shadow=COLD),
    WALL:        dict(color="#8B91A5", grit=0.22, shadow=COLD),
    WALL_LIGHT:  dict(color="#A9AFC1", grit=0.2, shadow=COLD),
    WALL_DARK:   dict(color="#61677B", grit=0.2, shadow=COLD),
    CONCRETE:    dict(color="#9C9BA7", grit=0.26, shadow=COLD),
    METAL:       dict(color="#9DA2B2", grit=0.16, shadow=COLD),
    METAL_DARK:  dict(color="#4C4E60", grit=0.14, shadow=COLD),
    STEEL:       dict(color="#C2C6D0", grit=0.12, shadow=COLD),
    PLASTIC:     dict(color="#3C3E4C", grit=0.06, shadow=COLD),
    RUST:        dict(color="#B8654A", grit=0.24, shadow=COLD),
    HAZARD:      dict(color="#E9B23C", grit=0.2, shadow=COLD),
    MARKING:     dict(color="#EEEFF3", grit=0.2, shadow=COLD),
    PORCELAIN:   dict(color="#E7E9EE", grit=0.08, shadow=COLD),
    FABRIC:      dict(color="#A8323C", grit=0.06, shadow=COLD),
    FABRIC_BLUE: dict(color="#4E6A9C", grit=0.06, shadow=COLD),
    MATTRESS:    dict(color="#DCD5C6", grit=0.08, shadow=COLD),
    WOOD:        dict(color="#8C6548", grit=0.2, shadow=COLD),
    VOID:        dict(color="#2A2A3A", grit=0.0, shadow=COLD),
    GLOW:        dict(color="#8FE6FF", emission="#3AB4FF", emission_intensity=3.2, grit=0.0),
    GLOW_SOFT:   dict(color="#2E6E9C", emission="#1A6FC0", emission_intensity=1.1, grit=0.0),
    SCREEN:      dict(color="#3FA6D8", emission="#2490D0", emission_intensity=1.4, grit=0.0),
    LAMP_COLD:   dict(color="#D6ECFF", emission="#9CCFFF", emission_intensity=1.8, grit=0.0),
    LAMP_WARM:   dict(color="#FFB070", emission="#FF7020", emission_intensity=2.6, grit=0.0),
    LIGHT_RED:   dict(color="#FF4040", emission="#FF1A10", emission_intensity=2.6, grit=0.0),
    LIGHT_GREEN: dict(color="#60FFB0", emission="#20E080", emission_intensity=2.0, grit=0.0),
    GRIME:       dict(color="#7D8292", grit=0.24, shadow=COLD),
    GRIME_DARK:  dict(color="#717586", grit=0.22, shadow=COLD),
    PUDDLE:      dict(color="#6E7C94", emission="#000000", emission_intensity=0.0, grit=0.04),
    TANK:        dict(color="#4A8CB4", emission="#11528C", emission_intensity=0.85, grit=0.0),
    CARDBOARD:   dict(color="#B88F62", grit=0.18, shadow=COLD),
    GUNMETAL:    dict(color="#686D7E", grit=0.18, shadow=COLD),
    WOOD_GREY:   dict(color="#8E887E", grit=0.22, shadow=COLD),
    SPECIMEN:    dict(color="#3A6A86", emission="#0C2A40", emission_intensity=0.7, grit=0.0),
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
    if "emission" in entry:
        # previews: show glowing parts brighter, since Workbench has no emission
        r, g, b = (min(1.0, c * 1.6 + 0.1) for c in (r, g, b))
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
    """Palette section of LabKit.json."""
    out = []
    for name, e in ENTRIES.items():
        out.append({
            "name": name,
            "color": e["color"],
            "emission": e.get("emission", "#000000"),
            "emissionIntensity": e.get("emission_intensity", 0.0),
            "grit": e.get("grit", 0.2),
            "sway": 0.0,
            "doubleSided": e.get("double_sided", False),
            "shadow": e.get("shadow", "#9E809E"),
        })
    return out
