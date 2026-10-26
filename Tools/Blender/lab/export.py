"""FBX export per kit piece and the JSON manifests Unity reads."""

import json
import math
import os

import bpy


def export_piece(objs, path):
    """Exports one piece (all of its objects) at the origin. Same axis settings as the desert kit
    and the knight, so Unity's import rules (bakeAxisConversion) give identity rotation."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="OFF",
        use_mesh_modifiers=True,
        colors_type="LINEAR",
        bake_anim=False,
        add_leaf_bones=False,
        use_custom_props=False,
        path_mode="AUTO",
    )


def write_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)


def to_unity_position(loc):
    """Blender level space -> Unity level space: north (+Y) becomes +Z, east stays +X."""
    x, y, z = loc
    return [round(x, 4), round(z, 4), round(y, 4)]


def to_unity_yaw(rot_z):
    """The FBX import maps piece space (x, y, z) to Unity (x, z, y), the same mirror as the level
    frame, so a Blender turn is simply a Unity turn the other way."""
    return round((-rot_z) % 360.0, 3)


def direction_to_unity_yaw(angle):
    """Facing given as a compass angle in Blender space (CCW from +X) -> Unity yaw."""
    a = math.radians(angle)
    return round(math.degrees(math.atan2(math.cos(a), math.sin(a))) % 360.0, 3)


def layout_json(name, placements, spawns, blockers, gates, sections=None):
    return {
        "name": name,
        "pieces": [{
            "piece": p["piece"],
            "group": p["group"],
            "id": p.get("id", ""),
            "position": to_unity_position(p["loc"]),
            "yaw": to_unity_yaw(p["rot"]),
            "scale": [round(p.get("sx", 1.0), 4), 1.0, 1.0],
            "sections": p.get("sections", []),
        } for p in placements],
        "spawns": [{
            "kind": kind,
            "position": to_unity_position(loc),
            "yaw": direction_to_unity_yaw(facing),
        } for kind, loc, facing in spawns],
        "blockers": [{
            "position": to_unity_position(c),
            "size": [s[0], s[2], s[1]],
            "yaw": round(-r % 360.0, 3),
        } for c, s, r in blockers],
        "gates": gates,
        "sectionMap": sections,
    }
