using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

[Tool]
public partial class DelegatedChunkDataStruct(ChunkDataStruct underlying) : Resource, ChunkDataStruct
{

    public DelegatedChunkDataStruct() : this(new ChunkDataStructImpl())
    {}
    
    public ChunkDataStruct GetUnderlying()
    {
        return underlying;
    }

    // -- Encoded Data grid (TriangleGrid)
    //----------------------------------------
    // ----> Grid Expected Size
    [Export]
    public Vector3I FrameDimensions
    {
        get => underlying.FrameDimensions;
        set => underlying.FrameDimensions = value;
    }

    // ----> RegularUniformFrame (DoubleDeltaTiling) seeds
    [Export]
    public double[] TriFrameSeed1
    {
        get => underlying.TriFrameSeed1;
        set => underlying.TriFrameSeed1 = value;
    }

    [Export]
    public double[] TriFrameSeed2
    {
        get => underlying.TriFrameSeed2;
        set => underlying.TriFrameSeed2 = value;
    }

    // ----> Data
    [Export]
    public float[] Values
    {
        get => underlying.Values;
        set => underlying.Values = value;
    }

    // -- Encoded DualGrid (Hexagonal grid)
    //----------------------------------------
    // ----> RegularUniformFrame (DoubleDeltaTiling) seeds
    [Export]
    public double[] HexFrameSeed1
    {
        get => underlying.HexFrameSeed1;
        set => underlying.HexFrameSeed1 = value;
    }

    [Export]
    public double[] HexFrameSeed2
    {
        get => underlying.HexFrameSeed2;
        set => underlying.HexFrameSeed2 = value;
    }

    // ----> FullCells
    // -------> Cell indexes
    [Export]
    public int[] FullCellIndicesAsV2I
    {
        get => underlying.FullCellIndicesAsV2I;
        set => underlying.FullCellIndicesAsV2I = value;
    }

    // -------> DataIndexesMapping (FullCellIndices.length)
    [Export]
    public int[] FullCellMappingsAsV3I
    {
        get => underlying.FullCellMappingsAsV3I;
        set => underlying.FullCellMappingsAsV3I = value;
    }
    [Export]
    public int[] FullCellVisitsMappingKeyAsV3I
    {
        get => underlying.FullCellVisitsMappingKeyAsV3I;
        set => underlying.FullCellVisitsMappingKeyAsV3I = value;
    }

    [Export]
    public int[] FullCellVisitsMappingValueAsV2I
    {
        get => underlying.FullCellVisitsMappingValueAsV2I;
        set => underlying.FullCellVisitsMappingValueAsV2I = value;
    }
    // ----> PendingCells
    // -------> Pending Cell indexes & Mapping
    [Export]
    public int[] PendingCellIndicesAsV2I
    {
        get => underlying.PendingCellIndicesAsV2I;
        set => underlying.PendingCellIndicesAsV2I = value;
    }

    // -------> DataIndexesMapping (FullCellIndices.length)
    [Export]
    public int[] PendingCellsVisitsMappingKeyAsV3I
    {
        get => underlying.PendingCellsVisitsMappingKeyAsV3I;
        set => underlying.PendingCellsVisitsMappingKeyAsV3I = value;
    }

    [Export]
    public int[] PendingCellsVisitsMappingValueAsV2I
    {
        get => underlying.PendingCellsVisitsMappingValueAsV2I;
        set => underlying.PendingCellsVisitsMappingValueAsV2I = value;
    }

    // Encoded neighbors
    //----------------------
    [Export]
    public int[] ExistingNeighborsAsV2I
    {
        get => underlying.ExistingNeighborsAsV2I;
        set => underlying.ExistingNeighborsAsV2I = value;
    }

    // Encoded Marching Triangles properties
    //----------------------
    [Export]
    public int MergeMode
    {
        get => underlying.MergeMode;
        set => underlying.MergeMode = value;
    }

    [Export]
    public float MergeThreshold
    {
        get => underlying.MergeThreshold;
        set => underlying.MergeThreshold = value;
    }

    // Encoded ColorMaps
    //----------------------
    [Export]
    public Color[] Ground0Colors
    {
        get => underlying.Ground0Colors;
        set => underlying.Ground0Colors = value;
    }

    [Export]
    public Color[] Ground1Colors
    {
        get => underlying.Ground1Colors;
        set => underlying.Ground1Colors = value;
    }

    [Export]
    public Color[] Wall0Colors
    {
        get => underlying.Wall0Colors;
        set => underlying.Wall0Colors = value;
    }

    [Export]
    public Color[] Wall1Colors
    {
        get => underlying.Wall1Colors;
        set => underlying.Wall1Colors = value;
    }
}