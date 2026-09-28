using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

/// <summary>
/// Interface for serializing the data structure of a Chunk.
/// </summary>
/// All fields should be Godot.Variant-compatible or arrays of Variants.
public interface IChunkDataStruct
{
    // -- Encoded Data grid (TriangleGrid)
    //----------------------------------------
    // ----> Grid Expected Size
    public Vector3I FrameDimensions { get; set; }

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
    public int[] FullCellIndicesAsV2I { get; set; }

    // -------> DataIndexesMapping (FullCellIndices.length)
    public int[] FullCellMappingsAsV3I { get; set; }
    public int[] FullCellVisitsMappingKeyAsV3I { get; set; }
    public int[] FullCellVisitsMappingValueAsV2I { get; set; }
    // ----> PendingCells
    // -------> Pending Cell indexes & Mapping
    public int[] PendingCellIndicesAsV2I { get; set; }
    public int[] PendingCellsVisitsMappingKeyAsV3I { get; set; }
    public int[] PendingCellsVisitsMappingValueAsV2I { get; set; }

    // Encoded neighbors
    //----------------------
    public int[] ExistingNeighborsAsV2I { get; set; }

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