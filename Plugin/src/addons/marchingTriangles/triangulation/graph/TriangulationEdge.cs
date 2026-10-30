using System;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// Representation of an Edge within the context of being the border of a SubCell,
/// and subsequently tied to a triangulation and an edgeIdx within said triangulation. 
/// </summary>
public class TriangulationEdge(
    HexTerrainCell cell,
    Triangulation triangulation,
    int triEdgeIndex,
    (int s1, int s2) cellSummits,
    ChunkTerrainEdge parentEdge)
{
    public Triangulation Triangulation { get;  } = triangulation;
    public  int TriEdgeIndex{ get;  } = triEdgeIndex;

    private ChunkTerrainEdge _parentEdge = parentEdge;

    public  HexTerrainCell ParentCell{ get;  } = cell;
    public  (int, int) CellSummitIndices{ get;  } = cellSummits;

    /// <summary>
    /// The record containing the unique indices of this instance.
    /// </summary>
    public TerrainChunkGraph.EdgeTriangulationData? EdgeData = null;
    
    public void SetParentEdge(ChunkTerrainEdge edge)
    {
        if (_parentEdge!=null && !_parentEdge.Equals(edge)) throw new ArgumentException("Cannot set the parent edge twice with different values");
        _parentEdge = edge;
    }

    public ChunkTerrainEdge GetParentEdge()
    {
        return _parentEdge;
    }

    internal void RegisterEdgeData()
    {
        //TODO
    }
}