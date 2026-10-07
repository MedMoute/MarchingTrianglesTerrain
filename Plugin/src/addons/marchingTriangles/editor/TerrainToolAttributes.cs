using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.data;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor;

/// <summary>
/// Tool Attribute values.
/// Those values are the ones actually used
/// by the underlying algorithmic operation performed by the plugin.
/// </summary>
public partial class TerrainToolAttributes : Node
{
    public int BrushIndex { get; set; } 
    public double BrushSize { get; set; } = 5D;
    public double EaseValue { get; set; } = -1; // No ease
    public double Strength { get; set; } = 1;
    public double Height { get; set; } 
    public bool Flatten { get; set; } = true;
    public bool Falloff { get; set; } = true;
    
    public Vector2I GeometryModes { get; set; } = Vector2I.Zero;
    
    public Vector4 GeometryModesParameters { get; set; } = new(-1f,-1f,-1f,-1f);


    public bool MaskGrass { get; set; } 
    
    public Curve FalloffCurve { get; private set; } = FileUtils
        .Load<Curve>("res://addons/marchingTriangles/editor/resources/plugin_materials/curve_falloff.tres");

    // 3D point whe the tool dragging started
    public Vector3 DragBasePosition { get; set; }

    /// <summary>
    /// The coordinates of the chunk currently selected by the UI
    /// </summary>
    public Vector2I SelectedChunk { get; set; }

    // Only relevant for vertex painting
    public bool PaintWalls { get; set; } 

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
