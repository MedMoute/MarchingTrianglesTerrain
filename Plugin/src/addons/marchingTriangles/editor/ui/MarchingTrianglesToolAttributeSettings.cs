using System;
using System.Collections.Generic;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.ui;

/// <summary>
/// Attribute Settings for each of the plugin tools.
/// </summary>
/// The settings are akin to flags that will enable or disable a given property/configuration in the Editor UI.
public partial class MarchingTrianglesToolAttributeSettings : Resource
{
    internal MarchingTrianglesToolAttributeSettings(
        bool brushType = false,
        bool size = false,
        bool easeValue = false,
        bool height = false,
        bool strength = false,
        bool flatten = false,
        bool falloff = false,
        bool maskMode = false,
        bool material = false,
        bool textureName = false,
        bool texturePreset = false,
        bool quickPaintSelection = false,
        bool paintWalls = false,
        bool chunkManagement = false,
        bool terrainSettings = false)
    {
        BrushType = brushType;
        Size = size;
        EaseValue = easeValue;
        Height = height;
        Strength = strength;
        Flatten = flatten;
        Falloff = falloff;
        MaskMode = maskMode;
        Material = material;
        TextureName = textureName;
        TexturePreset = texturePreset;
        QuickPaintSelection = quickPaintSelection;
        PaintWalls = paintWalls;
        ChunkManagement = chunkManagement;
        TerrainSettings = terrainSettings;
    }

// General brush attributes
    [Export] public bool BrushType;
    [Export] public bool Size;
    [Export] public bool EaseValue;
    [Export] public bool Height;
    [Export] public bool Strength;
    [Export] public bool Flatten;
    [Export] public bool Falloff;

// Brush specific attributes
    [Export] public bool MaskMode;
    [Export] public bool Material;
    [Export] public bool TextureName;

// Vertex painting-related special attributes
    [Export] public bool TexturePreset;
    [Export] public bool QuickPaintSelection;
    [Export] public bool PaintWalls;

// Non-brush attributes
    [Export] public bool ChunkManagement;
    [Export] public bool TerrainSettings;

    public List<Tuple<string, bool>> GetPropertiesFlagList()
    {
        var res = new List<Tuple<string, bool>>
        {
            new("BrushType", BrushType),
            new("Size", Size),
            new("EaseValue", EaseValue),
            new("Height", Height),
            new("Strength", Strength),
            new("Flatten", Flatten),
            new("Falloff", Falloff),
            new("MaskMode", MaskMode),
            new("Material", Material),
            new("TextureName", TextureName),
            new("TexturePreset", TexturePreset),
            new("QuickPaintSelection", QuickPaintSelection),
            new("PaintWalls", PaintWalls),
            new("ChunkManagement", ChunkManagement),
            new("TerrainSettings", TerrainSettings)
        };
        return res;
    }
}