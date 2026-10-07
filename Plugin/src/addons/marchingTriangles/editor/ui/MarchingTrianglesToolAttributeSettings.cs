using System;
using System.Collections.Generic;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.ui;

/// <summary>
/// Attribute Settings for each of the plugin tools.
/// </summary>
/// The settings are akin to flags that will enable or disable a given property/configuration in the Editor UI.
// ReSharper disable once Godot.MissingParameterlessConstructor
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
        bool chunkManagement = false,
        bool terrainSettings = false,
        bool geometryMode = false,
        bool geometryModeParameterEditors=false)
    {
        BrushType = brushType;
        Size = size;
        EaseValue = easeValue;
        Height = height;
        Strength = strength;
        Flatten = flatten;
        Falloff = falloff;
        MaskMode = maskMode;
        ChunkManagement = chunkManagement;
        TerrainSettings = terrainSettings;
        GeometryMode = geometryMode;
        GeometryModeParameterEditors = geometryModeParameterEditors;
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

// Non-brush attributes
    [Export] public bool ChunkManagement;
    [Export] public bool TerrainSettings;
    [Export] public bool GeometryMode;
    [Export] public bool GeometryModeParameterEditors;


    public List<Tuple<string, bool>> GetPropertiesFlagList()
    {
        var res = new List<Tuple<string, bool>>
        {
            new(nameof(BrushType), BrushType),
            new(nameof(Size), Size),
            new(nameof(EaseValue), EaseValue),
            new(nameof(Height), Height),
            new(nameof(Strength), Strength),
            new(nameof(Flatten), Flatten),
            new(nameof(Falloff), Falloff),
            new(nameof(MaskMode), MaskMode),
            new(nameof(ChunkManagement), ChunkManagement),
            new(nameof(TerrainSettings), TerrainSettings),
            new(nameof(GeometryMode), GeometryMode),
            new(nameof(GeometryModeParameterEditors), GeometryModeParameterEditors)

        };
        return res;
    }
}