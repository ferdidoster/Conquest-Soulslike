using System;

namespace Conquest.EditorTools
{
    // JSON written by Tools/Blender/build_lab_kit.py (LabKit.json and Layouts/LabDemo.json).
    // Positions are already in Unity coordinates.

    [Serializable]
    public sealed class LabKitManifest
    {
        public LabPaletteEntry[] palette;
        public LabPieceEntry[] pieces;
    }

    [Serializable]
    public sealed class LabPaletteEntry
    {
        public string name;
        public string color;
        public string emission;
        public float emissionIntensity;
        public float grit;
        public float sway;
        public bool doubleSided;
        public string shadow;

        public PaletteEntry ToPaletteEntry() => new PaletteEntry
        {
            name = name, color = color, emission = emission, emissionIntensity = emissionIntensity,
            grit = grit, sway = sway, doubleSided = doubleSided,
        };
    }

    [Serializable]
    public sealed class LabPieceEntry
    {
        public string name;
        public string category;
        public string model;
        public string collider;
        public string notes;
        public float[] boxCenter;
        public float[] boxSize;
        public bool fade;
        public LabLightEntry[] lights;
        public bool isGate;
        public float gateTravel;
        public bool gateAuto;
        public bool gateStartsOpen;
        public float[] sensorCenter;
        public float[] sensorSize;
    }

    [Serializable]
    public sealed class LabLightEntry
    {
        public float[] offset;
        public string color;
        public float range;
        public float intensity;
        public float flicker;
        public float spot;
    }

    [Serializable]
    public sealed class LabLayout
    {
        public string name;
        public LabPlacedPiece[] pieces;
        public LayoutSpawn[] spawns;
        public LayoutBlocker[] blockers;
        public LabGateNote[] gates;
        public LabSectionMap sectionMap;
    }

    [Serializable]
    public sealed class LabSectionMap
    {
        public string[] names;
        public float originX;
        public float originZ;
        public float cellSize;
        public int width;
        public int height;
        public string cells;
    }

    [Serializable]
    public sealed class LabPlacedPiece
    {
        public string piece;
        public string group;
        public string id;
        public float[] position;
        public float yaw;
        public float[] scale;
        public string[] sections;
    }

    [Serializable]
    public sealed class LabGateNote
    {
        public string id;
        public string notes;
    }
}
