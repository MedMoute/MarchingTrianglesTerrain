using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

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
    public int[] FullCellIndicesAsV2I { get; set; }
    public int[] FullCellMappingsAsV3I { get; set; }
    public int[] PendingCellIndicesAsV2I { get; set; }

    public int[] PendingCellsVisitsMappingKeyAsV3I { get; set; }
    public int[] PendingCellsVisitsMappingValueAsV2I { get; set; }
    public int[] ExistingNeighborsAsV2I { get; set; }
    public int MergeMode { get; set; }
    public float MergeThreshold { get; set; }
    public Color[] Ground0Colors { get; set; }
    public Color[] Ground1Colors { get; set; }
    public Color[] Wall0Colors { get; set; }
    public Color[] Wall1Colors { get; set; }
}