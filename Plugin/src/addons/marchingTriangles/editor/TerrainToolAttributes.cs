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
/// This attributes 
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
    
    public Curve FalloffCurve { get; set; } = FileUtils.Load<Curve>("res://addons/marchingTriangles/editor/resources/plugin_materials/curve_falloff.tres");

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
            SetVertexColorIndex(value);
        }
    }

    private void SetVertexColorIndex(int vertexColorIndex)
    {
        switch (vertexColorIndex)
        {
            case 0: // rr
                _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                break;
            case 1: // rg
                _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                break;
            case 2: // rb
                _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                break;
            case 3: // ra
                _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
            case 4: // gr
                _vertexColor0 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                break;
            case 5: // gg
                _vertexColor0 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                break;
            case 6: // gb
                _vertexColor0 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                break;
            case 7: // ga
                _vertexColor0 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
            case 8: // br
                _vertexColor0 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                break;
            case 9: // bg
                _vertexColor0 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                break;
            case 10: // bb
                _vertexColor0 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                break;
            case 11: // ba
                _vertexColor0 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
            case 12: // ar
                _vertexColor0 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                break;
            case 13: // ag
                _vertexColor0 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                _vertexColor1 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                break;
            case 14: // ab
                _vertexColor0 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                break;
            case 15: // aa
                _vertexColor0 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
        }
    }

    private Color _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);

    private Color _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);

    // Only relevant for Bridge building
    public Vector3 BridgeStartPos { get; set; }
    
    public Variant TerrainSettings { get; set; }

    // Flag to prevent _set_new_textures() when syncing preset from terrain node
    public bool SyncFromTerrain = false;
}
