using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.graph;
using MathNet.Spatial.Euclidean;
using QuikGraph;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// Representation of an edge of a HexTerrainChunk, as the geometrical relation between two linked vertices.
/// </summary>
public class ChunkTerrainEdge : EquatableUndirectedEdge<ChunkTerrainNode>,
    IImplicitlyBuildableFromGraph<(Vector2 source, Vector2 target)>
{
    public new ChunkTerrainNode Source { get; init; }
    public new ChunkTerrainNode Target { get; init; }

    private TriangulationEdge? _border1 = null;
    private TriangulationEdge? _border2 = null;

    internal ChunkTerrainEdge(ChunkTerrainNode source, ChunkTerrainNode target, TerrainChunkGraph parentGraph) : base(
        source, target)
    {
        Source = source;
        Target = target;
        ParentGraph = parentGraph;
    }

    /// <summary>
    /// This method generates the terrain's edges from a provided cell within the provided
    /// terrain chunk and considering the provided node graphs.
    /// </summary>
    public static List<ChunkTerrainEdge> BuildEdgesFromChunkAndNodes(
        HexagonalTerrainChunk hexagonalTerrainChunk,
        HexTerrainCell fullCell,
        Dictionary<Vector3I, CellSummitPoint> cellSummits,
        Dictionary<Vector2I, CellCenterPoint> cellCenters)
    {

        if (!fullCell.IsReady())
        {
            throw new ArgumentException($"cell {fullCell} is not ready for processing", nameof(fullCell));
        }
        List<ChunkTerrainEdge> edges = new();
        var idx = fullCell.CellCoordsImplicit;
        var centerPoint = cellCenters[idx];
        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            //Build interior edges
            //We use this loop to also add the CellSummitPoints's edge extremities
            Vector2D cellSummitPositionD = fullCell.VertexPositionsInPlane[i];
            ChunkTerrainNode matchingNode = cellSummits.Values.First(p => p.Position.IsEqualApprox(
                new Vector2((float)cellSummitPositionD.X, (float)cellSummitPositionD.Y)));

            //We build the edge instance 
            var intEdge = centerPoint.CompareTo(matchingNode) < 0
                ? new ChunkTerrainEdge(centerPoint, matchingNode, hexagonalTerrainChunk.Graph)
                : new ChunkTerrainEdge(matchingNode, centerPoint, hexagonalTerrainChunk.Graph);
            edges.Add(intEdge);

            //Build the exterior edges
            Vector2D cellSummitNextPositionD =
                fullCell.VertexPositionsInPlane[(i + 1) % HexTerrainCell.VertexCount];
            ChunkTerrainNode matchingNextNode = cellSummits.Values.First(p => p.Position.IsEqualApprox(
                new Vector2((float)cellSummitNextPositionD.X, (float)cellSummitNextPositionD.Y)));

            var extEdge = matchingNode.CompareTo(matchingNextNode) < 0
                ? new ChunkTerrainEdge(matchingNode, matchingNextNode, hexagonalTerrainChunk.Graph)
                : new ChunkTerrainEdge(matchingNextNode, matchingNode, hexagonalTerrainChunk.Graph);
            edges.Add(extEdge);
        }

        return edges;
    }

    /// <summary>
    /// This method generates the terrain's edges from the provided
    /// terrain chunk and considering the provided node graphs.
    /// </summary>
    public static List<ChunkTerrainEdge> BuildEdgesFromChunkAndNodes(
        HexagonalTerrainChunk hexagonalTerrainChunk,
        Dictionary<Vector3I, CellSummitPoint> cellSummits,
        Dictionary<Vector2I, CellCenterPoint> cellCenters)
    {
        var edges = new List<ChunkTerrainEdge>();

        foreach (var fullCell in hexagonalTerrainChunk.TerrainDualGrid.CompleteCells.Values)
        {
            edges.AddRange(BuildEdgesFromChunkAndNodes(hexagonalTerrainChunk,fullCell, cellSummits, cellCenters ));
        }

        return edges;
    }

    public List<TriangulationEdge> GetSubCellBorders()
    {
        if (_border1 == null)
        {
            if (_border2 == null) return [];
            return [_border2];
        }

        if (_border2 == null) return [_border1];
        return [_border1, _border2];
    }

    internal void AddBorder(TriangulationEdge triangulationEdge)
    {
        if (_border1 == null)
        {
            _border1 = triangulationEdge;
        }
        else if (_border2 == null)
        {
            _border2 = triangulationEdge;
        }
        else
        {
            throw new NotSupportedException("Two borders are already on this edge");
        }
    }

    public TerrainChunkGraph ParentGraph { get; }

}