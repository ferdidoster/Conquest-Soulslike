using System;
using UnityEngine;

namespace Conquest.EditorTools
{
    // JSON written by Tools/Blender/build_arena_kit.py (DesertKit.json and Layouts/*.json).
    // Positions are already in Unity coordinates.

    [Serializable]
    public sealed class DesertKitManifest
    {
        public PaletteEntry[] palette;
        public KitPieceEntry[] pieces;
    }

    [Serializable]
    public sealed class PaletteEntry
    {
        public string name;
        public string color;
        public string emission;
        public float emissionIntensity;
        public float grit;
        public float sway;
        public bool doubleSided;
    }

    [Serializable]
    public sealed class KitPieceEntry
    {
        public string name;
        public string category;
        public string model;
        public string collider;
        public string notes;
        public bool hover;
        public float[] boxCenter;
        public float[] boxSize;
        public bool hasLight;
        public float[] lightOffset;
        public string lightColor;
        public float lightRange;
        public float lightIntensity;
    }

    [Serializable]
    public sealed class LevelLayout
    {
        public string name;
        public PlacedPiece[] pieces;
        public LayoutSpawn[] spawns;
        public LayoutBlocker[] blockers;
    }

    [Serializable]
    public sealed class PlacedPiece
    {
        public string piece;
        public string group;
        public float[] position;
        public float yaw;
        public float scale = 1f;
    }

    [Serializable]
    public sealed class LayoutSpawn
    {
        public string kind;
        public float[] position;
        public float yaw;
    }

    [Serializable]
    public sealed class LayoutBlocker
    {
        public float[] position;
        public float[] size;
        public float yaw;
    }

    internal static class KitJson
    {
        public static Vector3 Vec(float[] v) => v != null && v.Length >= 3 ? new Vector3(v[0], v[1], v[2]) : Vector3.zero;

        public static Color Col(string hex, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : fallback;
        }
    }
}
