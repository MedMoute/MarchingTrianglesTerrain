using Godot;
using Godot.Collections;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

[Tool]
// Chunk Data Exported by the plugin
public partial class MttChunkData : Resource
{
    // Fields of V1_STRUCT & V1_FULL
    [Export] public DataContent Data { get; internal set; }
    [Export] public string ParentTerrainId { get; internal set; }
    [Export] public Vector2I ChunkCoords { get; internal set; }
    [Export] public int MergeMode { get; internal set; }
    [Export] public DelegatedChunkDataStruct DataStruct { get; internal set; }
    [Export] public Array<byte> GroundTexturesIdx { get; internal set; }
    [Export] public Array<byte> WallTexturesIdx { get; internal set; }

    // Additional fields of V1_FULL
    // Transient data saved for caching but regenerated on load if missing
    [Export] public Mesh Mesh { get; internal set; }
    [Export] public Vector3[] CollisionFaces { get; internal set; }

    public void SetCollisionFromShape(ConcavePolygonShape3D shape)
    {
        if (shape != null)
        {
            CollisionFaces = shape.GetFaces();
        }
    }

    /// <summary>
    /// Helper to create ConcavePolyShape3D from Vector3[] data
    /// </summary>
    public ConcavePolygonShape3D GetCollisionShape()
    {
        if (CollisionFaces.IsEmpty())
        {
            return null;
        }

        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(CollisionFaces);
        return shape;
    }

    internal interface ChunkDataStruct
    {
        // -- Encoded Data grid (TriangleGrid)
        //----------------------------------------
        // ----> Grid Expected Size
        public abstract Vector3I FrameDimensions { get; set; }

        // ----> RegularUniformFrame (DoubleDeltaTiling) seeds
        public double[] TriFrameSeed1 { get; set; }

        public double[] TriFrameSeed2 { get; set; }

        // ----> Data
        public float[] Values { get; set; }

        // -- Encoded DualGrid (Hexagonal grid)
        //----------------------------------------
        // ----> RegularUniformFrame (DoubleDeltaTiling) seeds
        public double[] HexFrameSeed1 { get; set; }

        public double[] HexFrameSeed2 { get; set; }

        // ----> FullCells
        // -------> Cell indexes
        public Vector2I[] FullCellIndices { get; set; }

        // -------> DataIndexesMapping (FullCellIndices.length)
        public Vector3I[] FullCellMappings { get; set; }

        // ----> PendingCells
        // -------> Pending Cell indexes & Mapping
        public Vector2I[] PendingCellIndices { get; set; }
        public Vector3I?[] PendingCellsVisitsMappingKey { get; set; }
        public Vector2I?[] PendingCellsVisitsMappingValue { get; set; }

        // Encoded neighbors
        //----------------------
        public Vector2I[] ExistingNeighbors { get; set; }

        // Encoded Marching Triangles properties
        //----------------------
        public int MergeMode { get; set; }
        public float MergeThreshold { get; set; }

        // Encoded ColorMaps
        //----------------------
        public Color[] Ground0Colors { get; set; }
        public Color[] Ground1Colors { get; set; }
        public Color[] Wall0Colors { get; set; }
        public Color[] Wall1Colors { get; set; }
    }

    /// <summary>
    /// ChunkDataStruct Implementation based on AutoProperties
    /// </summary>
    public class ChunkDataStructImpl : ChunkDataStruct
    {
        public Vector3I FrameDimensions { get; set; }
        public double[] TriFrameSeed1 { get; set; }
        public double[] TriFrameSeed2 { get; set; }
        public float[] Values { get; set; }
        public double[] HexFrameSeed1 { get; set; }
        public double[] HexFrameSeed2 { get; set; }
        public Vector2I[] FullCellIndices { get; set; }
        public Vector3I[] FullCellMappings { get; set; }
        public Vector2I[] PendingCellIndices { get; set; }
        
        public Vector3I?[] PendingCellsVisitsMappingKey { get; set; }
        public Vector2I?[] PendingCellsVisitsMappingValue { get; set; }
        public Vector2I[] ExistingNeighbors { get; set; }
        public int MergeMode { get; set; }
        public float MergeThreshold { get; set; }
        public Color[] Ground0Colors { get; set; }
        public Color[] Ground1Colors { get; set; }
        public Color[] Wall0Colors { get; set; }
        public Color[] Wall1Colors { get; set; }
    }

    /// <summary>
    /// ChunkDataStruct Implementation based on a delegated Implementation
    /// </summary>
    public partial class DelegatedChunkDataStruct(ChunkDataStructImpl underlying) : Resource, ChunkDataStruct
    {
        public ChunkDataStructImpl GetUnderlying()
        {
            return underlying;
        }
        // -- Encoded Data grid (TriangleGrid)
        //----------------------------------------
        // ----> Grid Expected Size
        [Export] public Vector3I FrameDimensions
        {
            get => underlying.FrameDimensions;
            set => underlying.FrameDimensions = value;
        }

        // ----> RegularUniformFrame (DoubleDeltaTiling) seeds
        [Export] public double[] TriFrameSeed1
        {
            get => underlying.TriFrameSeed1;
            set => underlying.TriFrameSeed1 = value;
        }

        [Export] public double[] TriFrameSeed2
        {
            get => underlying.TriFrameSeed2;
            set => underlying.TriFrameSeed2 = value;
        }

        // ----> Data
        [Export] public float[] Values
        {
            get => underlying.Values;
            set => underlying.Values = value;
        }

        // -- Encoded DualGrid (Hexagonal grid)
        //----------------------------------------
        // ----> RegularUniformFrame (DoubleDeltaTiling) seeds
        [Export] public double[] HexFrameSeed1
        {
            get => underlying.HexFrameSeed1;
            set => underlying.HexFrameSeed1 = value;
        }

        [Export] public double[] HexFrameSeed2
        {
            get => underlying.HexFrameSeed2;
            set => underlying.HexFrameSeed2 = value;
        }

        // ----> FullCells
        // -------> Cell indexes
        [Export] public Vector2I[] FullCellIndices
        {
            get => underlying.FullCellIndices;
            set => underlying.FullCellIndices = value;
        }

        // -------> DataIndexesMapping (FullCellIndices.length)
        [Export] public Vector3I[] FullCellMappings
        {
            get => underlying.FullCellMappings;
            set => underlying.FullCellMappings = value;
        }

        // ----> PendingCells
        // -------> Pending Cell indexes & Mapping
        [Export] public Vector2I[] PendingCellIndices 
        {
            get => underlying.PendingCellIndices;
            set => underlying.PendingCellIndices = value;
        }

        // -------> DataIndexesMapping (FullCellIndices.length)
        [Export] public Vector3I?[] PendingCellsVisitsMappingKey         {
            get => underlying.PendingCellsVisitsMappingKey;
            set => underlying.PendingCellsVisitsMappingKey = value;
        }
        [Export] public Vector2I?[] PendingCellsVisitsMappingValue         {
            get => underlying.PendingCellsVisitsMappingValue;
            set => underlying.PendingCellsVisitsMappingValue = value;
        }

        // Encoded neighbors
        //----------------------
        [Export] public Vector2I[] ExistingNeighbors
        {
            get => underlying.ExistingNeighbors;
            set => underlying.ExistingNeighbors = value;
        }

        // Encoded Marching Triangles properties
        //----------------------
        [Export] public int MergeMode
        {
            get => underlying.MergeMode;
            set => underlying.MergeMode = value;
        }

        [Export] public float MergeThreshold
        {
            get => underlying.MergeThreshold;
            set => underlying.MergeThreshold = value;
        }

        // Encoded ColorMaps
        //----------------------
        [Export] public Color[] Ground0Colors
        {
            get => underlying.Ground0Colors;
            set => underlying.Ground0Colors = value;
        }

        [Export] public Color[] Ground1Colors
        {
            get => underlying.Ground1Colors;
            set => underlying.Ground1Colors = value;
        }

        [Export] public Color[] Wall0Colors
        {
            get => underlying.Wall0Colors;
            set => underlying.Wall0Colors = value;
        }

        [Export] public Color[] Wall1Colors
        {
            get => underlying.Wall1Colors;
            set => underlying.Wall1Colors = value;
        }
    }
}

public enum DataContent
{
    V1_STRUCT,
    V1_FULL
}