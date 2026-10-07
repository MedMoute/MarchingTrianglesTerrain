using System;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

public partial class TerrainSettings(
    MarchingTrianglesTerrain parent,
    ShaderMaterial? shaderMaterial) : Node
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

    public ShaderMaterial? ShaderMaterial => shaderMaterial;

    [Export]
    public int MaxHeight
    {
        get => _maxHeight;
        set
        {
            _maxHeight = value;
            shaderMaterial?.SetShaderParameter("height", _maxHeight);
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
            shaderMaterial?.SetShaderParameter("chunkDimensions", _chunkDimensions);
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
            shaderMaterial?.SetShaderParameter("cellSize", value);
        }
    }

    private GeometryMode _defaultChunkGeometryMode = GeometryMode.SmoothLinear;

    [Export]
    public GeometryMode ChunkBlendMode
    {
        get => _defaultChunkGeometryMode;
        set => _defaultChunkGeometryMode = value;
    }
    
    private GeometryMode _defaultChunkGeometryModeOverThreshold = GeometryMode.FlatTriangles;

    [Export]
    public GeometryMode ChunkBlendModeFallBack
    {
        get => _defaultChunkGeometryModeOverThreshold;
        set => _defaultChunkGeometryModeOverThreshold = value;
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

    private ThresholdComputationMode _defaultThresholdComputationMode = ThresholdComputationMode.Angle;
    
    [Export]
    public ThresholdComputationMode ThresholdComputationMode
    {
        get => _defaultThresholdComputationMode;
        set => _defaultThresholdComputationMode = value;
    }
    
    private float _defaultThresholdValue = MathF.PI/4;
    
    [Export]
    public float ThresholdValue
    {
        get => _defaultThresholdValue;
        set => _defaultThresholdValue = value;
    }
}