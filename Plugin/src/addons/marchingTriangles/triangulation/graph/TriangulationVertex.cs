using System;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// The extremity point of an edge (can be seen as a segment endpoint) within both a subCell (i.e. part of a cell in the
/// hexagonal grid), and a triangulation.
/// A triangulation vertex extremity can be assimilated to a vertex within a triangulation.
/// </summary>
public class TriangulationVertex(
    ChunkTerrainNode node,
    HexTerrainCell cell,
    int selfCellSummitIndex,
    Triangulation triangulation,
    int triVertexIndex)
{
    /// <summary>
    /// The edge graph node (i.e. vertex) this extremity leads to.
    /// </summary>
    public ChunkTerrainNode Node { get; } = node;

    /// <summary>
    /// The representation of the vertex as a cell summit.
    /// </summary>
    public int CellSummitIndex { get; } = selfCellSummitIndex;

    /// <summary>
    /// The index of the cell this vertex is part of.
    /// </summary>
    public HexTerrainCell Cell { get; } = cell;

    /// <summary>
    /// The triangulation this vertex is part of.
    /// </summary>
    public Triangulation Triangulation { get; } = triangulation;

    /// <summary>
    /// The record containing the unique indices of this instance.
    /// </summary>
    public TerrainChunkGraph.VertexTriangulationData? VertexData = null;

    public int TriVertexIndex { get; } = triVertexIndex;
    
    /// <summary>
    /// Registers the DTOs related to the vertex in the parent graph maps
    /// </summary>
    internal void RegisterVertexData()
    {
        //TODO
    }
}