using Godot;
using Localproto.addons.marchingTriangles.tiling;
using MathNet.Spatial.Euclidean;

namespace Localproto.addons.marchingTriangles.utils;

public partial class TerrainSettings(
    MarchingTrianglesTerrain parent,
    ShaderMaterial shaderMaterial) : Node
{
    [Signal]
    public delegate void ChunkDimensionsChangedEventHandler(int height, Vector2I chunkSize);

    private int _maxHeight = 32;
    
    /// Chunks Global orientation.
    /// DO NOT USE for local computations !
    public static RegularUniformFrame OrientationSystem { get; } = new DoubleDeltaTileOrientationSystem(
        new Vector2D(0.5,1/(2*Math.Sqrt(3))),
        new Vector2D(1,1/Math.Sqrt(3))
        );

    public ShaderMaterial ShaderMaterial => shaderMaterial;

    [Export]
    public int MaxHeight
    {
        get => _maxHeight;
        set
        {
            _maxHeight = value;
            shaderMaterial.SetShaderParameter("height", _maxHeight);
            if (Engine.IsEditorHint())
            {
                EmitSignal(nameof(ChunkDimensionsChanged), _maxHeight, _chunkDimensions);
            }
        }
    }

    /// <summary>
    /// The default size value, in triangular cells, for the chunk dimensions.
    /// </summary>
    private Vector2I _chunkDimensions = new (10,10);

    [Export]
    public Vector2I ChunkDimensions

    {
        get => _chunkDimensions;
        set
        {
            _chunkDimensions = value;
            shaderMaterial.SetShaderParameter("chunkDimensions", _chunkDimensions);
            if (Engine.IsEditorHint())
            {
                EmitSignal(nameof(ChunkDimensionsChanged), _maxHeight, _chunkDimensions);
            }
        }
    }

    private float _cellScale = 1f;

    [Export]
    public float CellScale
    {
        get => _cellScale;
        set
        {
            _cellScale = value;
            shaderMaterial.SetShaderParameter("cellSize", value);
        }
    }

    private int _blendValue = 0;

    [Export(PropertyHint.Range, "0,2,1")]
    public int BlendMode
    {
        get => _blendValue;
        set
        {
            _blendValue = value;
            if (value == 1 || value == 2)
            {
                shaderMaterial.SetShaderParameter("UseHardTextures", true);
            }
            else
            {
                shaderMaterial.SetShaderParameter("UseHardTextures", false);
            }

            shaderMaterial.SetShaderParameter("BlendMode", _blendValue);
            parent.ForceRebuildTerrain();
        }
    }

    private int _collisionLayerIdx = 9;

    [Export]
    public int CollisionLayer
    {
        get => _collisionLayerIdx;
        set
        {
            _collisionLayerIdx = value;
            parent.ForceRebuildTerrain();
        }
    }

    private double _wallThreshold = 0.0;

    [Export]
    public double WallThreshold
    {
        get => _wallThreshold;
        set
        {
            _wallThreshold = value;
            shaderMaterial.SetShaderParameter("WallThreshold", value);
        }
    }
    
    private double _ledgeThreshold = 1.0;

    [Export]
    public double LedgeThreshold
    {
        get => _ledgeThreshold;
        set
        {
            _ledgeThreshold = value;
            shaderMaterial.SetShaderParameter("LedgeThreshold", value);
        }
    }
    
    private double _ridgeThreshold = 1.0;

    [Export]
    public double RidgeThreshold
    {
        get => _ridgeThreshold;
        set
        {
            _ridgeThreshold = value;
            shaderMaterial.SetShaderParameter("RidgeThreshold", value);
        }
    }
    
    [Export] public int DefaultWallTexture { get; set; } = 5;

    public ShaderMaterial GetMaterial()
    {
        return shaderMaterial;
    }
}