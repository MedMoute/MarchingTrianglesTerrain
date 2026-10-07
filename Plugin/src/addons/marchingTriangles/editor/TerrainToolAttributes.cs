using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.data;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor;

/// <summary>
/// Tool Attribute values.
/// Those values are the ones actually used
/// by the underlying algorithmic operation performed by the plugin.
/// </summary>
public partial class TerrainToolAttributes : Node
{
    [Signal]
    public delegate void UiReloadRequestedEventHandler(int toolIndex);
    
    public int BrushIndex { get; set; } 
    public double BrushSize { get; set; } = 5D;
    public double EaseValue { get; set; } = -1; // No ease
    public double Strength { get; set; } = 1;
    public double Height { get; set; } 
    public bool Flatten { get; set; } = true;
    public bool Falloff { get; set; } = true;

    private Vector2I _geometryModes = Vector2I.Zero;
    public Vector2I GeometryModes
    {
        get => _geometryModes;
        set
        {
            _geometryModes = value;
            Vector4 defaultParams = new Vector4();

            if (MarchingTrianglesTerrainPlugin.Instance != null &&
                MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode != null)
            {
                if (((GeometryMode)_geometryModes.X).SupportsParameter(0))
                {
                    defaultParams.X = MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode.TerrainSettings
                        .ChunkGeometryParameters.X;
                }                else
                {
                    defaultParams.X = -1f;
                }
                if (((GeometryMode)_geometryModes.X).SupportsParameter(1))
                {
                    defaultParams.Y = MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode.TerrainSettings
                        .ChunkGeometryParameters.Y;
                }                else
                {
                    defaultParams.Y = -1f;
                }
                if (((GeometryMode)_geometryModes.Y).SupportsParameter(0))
                {
                    defaultParams.Z = MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode.TerrainSettings
                        .ChunkGeometryParameters.Z;
                }
                else
                {
                    defaultParams.Z = -1f;
                }
                if (((GeometryMode)_geometryModes.Y).SupportsParameter(1))
                {
                    defaultParams.W = MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode.TerrainSettings
                        .ChunkGeometryParameters.W;
                }
                else
                {
                    defaultParams.W = -1f;
                }
                if  (!defaultParams.IsEqualApprox(GeometryModesParameters))
                {
                    GeometryModesParameters = defaultParams;
                    //Force the UI reload by emitting signal
                    EmitSignal(nameof(UiReloadRequested),(int)TerrainToolMode.GeometryEdit);
                }
            }

        }
    }

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
