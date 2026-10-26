"""FBX export per kit piece and the JSON manifests Unity reads."""

import json
import os

import bpy

from . import layout


def export_piece(obj, path):
    """Exports one piece at the origin. Same axis settings as the knight, so Unity's import
    rules (bakeAxisConversion) give identity rotation and the front facing +Z."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
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


def layout_json(name, placements, spawns, blockers):
    return {
        "name": name,
        "pieces": [{
            "piece": piece,
            "group": group,
            "position": layout.to_unity_position(loc),
            "yaw": layout.to_unity_yaw(rot),
            "scale": scale,
        } for piece, group, loc, rot, scale in placements],
        "spawns": [{
            "kind": kind,
            "position": layout.to_unity_position(loc),
            "yaw": layout.direction_to_unity_yaw(facing),
        } for kind, loc, facing in spawns],
        "blockers": [{
            "position": layout.to_unity_position(c),
            "size": [s[0], s[2], s[1]],
            "yaw": round(-r % 360.0, 3),
        } for c, s, r in blockers],
    }
