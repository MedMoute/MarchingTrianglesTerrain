using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.data;

/// <summary>
/// ChunkDataStruct Implementation based on AutoProperties
/// </summary>
public class ChunkDataStructImpl : IChunkDataStruct
{
    public Vector3I FrameDimensions { get; set; }
    public double[] TriFrameSeed1 { get; set; } = null!;
    public double[] TriFrameSeed2 { get; set; } = null!;
    public float[] Values { get; set; } = null!;
    public double[] HexFrameSeed1 { get; set; } = null!;
    public double[] HexFrameSeed2 { get; set; } = null!;
    public int[] FullCellIndicesAsV2I { get; set; } = null!;
    public int[] FullCellMappingsAsV3I { get; set; } = null!;
    public int[] FullCellVisitsMappingKeyAsV3I { get; set; } = null!;
    public int[] FullCellVisitsMappingValueAsV2I { get; set; } = null!;
    public int[] PendingCellIndicesAsV2I { get; set; } = null!;

    public int[] PendingCellsVisitsMappingKeyAsV3I { get; set; } = null!;
    public int[] PendingCellsVisitsMappingValueAsV2I { get; set; } = null!;
    public int[] ExistingNeighborsAsV2I { get; set; } = null!;
    public int MergeMode { get; set; }
    public float MergeThreshold { get; set; }
    public Color[] Ground0Colors { get; set; } = null!;
    public Color[] Ground1Colors { get; set; } = null!;
    public Color[] Wall0Colors { get; set; } = null!;
    public Color[] Wall1Colors { get; set; } = null!;
}