using System.Collections.Generic;
using UnityEngine;

namespace SmearFramework.Editor
{
    internal enum CapturePreset
    {
        SideLeft,
        SideRight,
        TopDownSouth,
        TopDownWest,
        TopDownNorth,
        TopDownEast,
        IsometricSouth,
        IsometricSouthWest,
        IsometricWest,
        IsometricNorthWest,
        IsometricNorth,
        IsometricNorthEast,
        IsometricEast,
        IsometricSouthEast,
        Custom,
    }

    internal static class CapturePresetUtility
    {
        private readonly struct Entry
        {
            public readonly CapturePreset Preset;
            public readonly string Label;
            public readonly Vector3 Euler;

            public Entry(CapturePreset preset, string label, Vector3 euler)
            {
                Preset = preset;
                Label = label;
                Euler = euler;
            }
        }

        private static readonly Entry[] Entries =
        {
            new Entry(CapturePreset.SideLeft,          "Side View/Left",  new Vector3(  0f,  -90f,   0f)),
            new Entry(CapturePreset.SideRight,         "Side View/Right", new Vector3(  0f,   90f,   0f)),
            new Entry(CapturePreset.TopDownSouth,      "Top-down RPG/S",  new Vector3(-45f,    0f,   0f)),
            new Entry(CapturePreset.TopDownWest,       "Top-down RPG/W",  new Vector3(-45f,  -90f,   0f)),
            new Entry(CapturePreset.TopDownNorth,      "Top-down RPG/N",  new Vector3(-45f,  180f,   0f)),
            new Entry(CapturePreset.TopDownEast,       "Top-down RPG/E",  new Vector3(-45f,   90f,   0f)),
            new Entry(CapturePreset.IsometricSouth,    "Isometric/S",     new Vector3(-35f,    0f,   0f)),
            new Entry(CapturePreset.IsometricSouthWest,"Isometric/SW",    new Vector3(-35f,  -45f,   0f)),
            new Entry(CapturePreset.IsometricWest,     "Isometric/W",     new Vector3(-35f,  -90f,   0f)),
            new Entry(CapturePreset.IsometricNorthWest,"Isometric/NW",    new Vector3(-35f, -135f,   0f)),
            new Entry(CapturePreset.IsometricNorth,    "Isometric/N",     new Vector3(-35f,  180f,   0f)),
            new Entry(CapturePreset.IsometricNorthEast,"Isometric/NE",    new Vector3(-35f,  135f,   0f)),
            new Entry(CapturePreset.IsometricEast,     "Isometric/E",     new Vector3(-35f,   90f,   0f)),
            new Entry(CapturePreset.IsometricSouthEast,"Isometric/SE",    new Vector3(-35f,   45f,   0f)),
        };

        public static IReadOnlyList<CapturePreset> OrderedPresets { get; } = new[]
        {
            CapturePreset.SideLeft,
            CapturePreset.SideRight,
            CapturePreset.TopDownSouth,
            CapturePreset.TopDownWest,
            CapturePreset.TopDownNorth,
            CapturePreset.TopDownEast,
            CapturePreset.IsometricSouth,
            CapturePreset.IsometricSouthWest,
            CapturePreset.IsometricWest,
            CapturePreset.IsometricNorthWest,
            CapturePreset.IsometricNorth,
            CapturePreset.IsometricNorthEast,
            CapturePreset.IsometricEast,
            CapturePreset.IsometricSouthEast,
        };

        public static string GetLabel(CapturePreset preset)
        {
            foreach (var entry in Entries)
                if (entry.Preset == preset)
                    return entry.Label;
            return "Char Perspective";
        }

        public static Vector3 GetEuler(CapturePreset preset)
        {
            foreach (var entry in Entries)
                if (entry.Preset == preset)
                    return entry.Euler;
            return Vector3.zero;
        }

        public static CapturePreset GetPreset(Vector3 euler)
        {
            foreach (var entry in Entries)
            {
                if ((euler - entry.Euler).sqrMagnitude <= 0.1f)
                    return entry.Preset;
            }
            return CapturePreset.Custom;
        }
    }
}
