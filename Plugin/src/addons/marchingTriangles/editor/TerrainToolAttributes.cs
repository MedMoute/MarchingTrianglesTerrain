using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using Godot.Collections;
using MarchingTrianglesTerrain.addons.marchingTriangles.ui;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Tool Attribute values.
/// Those values are the ones actually used
/// by the underlying algorithmic operation performed by the plugin.
/// </summary>
public partial class TerrainToolAttributes : Node
{
    public int BrushIndex { get; set; } = 0;
    public double BrushSize { get; set; } = 5D;
    public double EaseValue { get; set; } = -1; // No ease
    public double Strength { get; set; } = 1;
    public double Height { get; set; } = 0;
    public bool Flatten { get; set; } = true;
    public bool Falloff { get; set; } = true;

    public bool MaskGrass { get; set; } = false;
    
    public Curve FalloffCurve { get; private set; } = FileUtils
        .Load<Curve>("res://addons/marchingTriangles/editor/resources/plugin_materials/curve_falloff.tres");

    // 3D point whe the tool dragging started
    public Vector3 DragBasePosition { get; set; }

    public Vector2I SelectedChunk { get; set; } = new Vector2I();

    // Only relevant for vertex painting
    public bool PaintWalls { get; set; } = false;

    private int _vertexColorIndex;

    public int VertexColorIndex
    {
        get => _vertexColorIndex;
        set
        {
            _vertexColorIndex = value;
        }
    }

    private Color _vertexColor0 = new(1.0f, 0.0f, 0.0f, 0.0f);

    private Color _vertexColor1 = new(1.0f, 0.0f, 0.0f, 0.0f);

    // Only relevant for Bridge building
    public Vector3 BridgeStartPos { get; set; }
    
    public Variant TerrainSettings { get; set; }

    // Flag to prevent _set_new_textures() when syncing preset from terrain node
    public bool SyncFromTerrain = false;
}
