using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MathNet.Numerics;
using MathNet.Spatial.Euclidean;
using QuikGraph;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

/// <summary>
/// Representation of a terrain chunk as an undirected graph.
/// Nodes of the graph are cell summits (including the center point),
/// while the graph edges are sub-cell edges (e.g. triangulation "sides")
/// </summary>
/// This graph is a layered abstraction between the conformal mesh of the terrain and
/// the Dual Grids of the chunk.
public class TerrainChunkGraph : UndirectedGraph<ChunkTerrainNode, ChunkTerrainEdge>
{
    public HexagonalTerrainChunk Chunk { get; }

    public record TriangulationVertex(int TriId, int[] TeIds, int Idx)
    {
        public int Id => _id;

        private readonly int _id = GetId();

        private static int _counter;

        private static int GetId()
        {
            var id = _counter;
            _counter++;
            return id;
        }
    }

    public record TriangulationEdge(int TriId, int[] TriVIds, int EdgeIdx)
    {
        public int Id => _id;

        private readonly int _id = GetId();

        private static int _counter;

        private static int GetId()
        {
            var id = _counter;
            _counter++;
            return id;
        }
    }

    public struct GraphTriangulation
    {
        public int TriId;
        public (int, int, int) TriVertices;
        public (int, int, int) TriEdges;
    }

    public Dictionary<int, TriangulationVertex> TriangulationVerticesById = new();
    public Dictionary<int, GraphTriangulation> GraphTriangulationsById = new();
    public Dictionary<int, TriangulationEdge> TriangulationsEdgesById = new();
    public Dictionary<int, Triangulation> TriangulationsById = new();

    private readonly Dictionary<Vector2I, Triangulation?[]?> _triangulationsMap = new();
    private readonly Dictionary<Vector2, ChunkTerrainNode> _edgeGraphNodesMapByPosition = new();

    /// <summary>
    /// Public constructor. This only builds the instance and does not populate the graph
    /// </summary>
    /// <param name="parent"></param>
    public TerrainChunkGraph(HexagonalTerrainChunk parent)
    {
        Chunk = parent;
    }

    public void AddTriangulationToMap(Vector2I cellIndex, int summitIndex, Triangulation triangulation)
    {
        if (!_triangulationsMap.TryGetValue(cellIndex, out var triangulations))
        {
            triangulations = new Triangulation[6];
            _triangulationsMap.Add(cellIndex, triangulations);
        }

        if (triangulations != null && triangulations[summitIndex] == null)
        {
            triangulations[summitIndex] = triangulation;
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public void AddNodeToMap(Vector2 point, ChunkTerrainNode node)
    {
        if (!_edgeGraphNodesMapByPosition.TryGetValue(point, out var existingNode))
        {
            _edgeGraphNodesMapByPosition.Add(point, node);
        }
        else
        {
            throw new NotSupportedException(
                $"The position {node.Position} is already mapped by another node : {existingNode}");
        }
    }

    public Dictionary<Vector2I, Triangulation?[]?> GetTriangulationsMap()
    {
        return _triangulationsMap;
    }

    /// <summary>
    /// Updates the triangulation values for a given data point position.
    /// If the provided position does not match any entry in the point graph, nothing is done.
    /// </summary>
    public void UpdateVertexValueInTris(Vector2D pos, float yValue)
    {
        var posF = new Vector2((float)pos.X, (float)pos.Y);
        _edgeGraphNodesMapByPosition.TryGetValue(posF, out var graphNode);
        if (graphNode is CellCenterPoint or null) return;
        var nbVertex = 0;
        var newPos = new Vector3(posF.X, yValue, posF.Y);

        foreach (var ee in graphNode.RelatedEdgeExtremities.Values)
        {
            var edgeIdx = 0;
            if (ee.Triangulation is null)
            {
                throw new InvalidOperationException($"Triangulation is not set for {ee.CellIndex};" +
                                                    $"({ee.CellSummitIndex},{ee.OtherCellSummitIndex})");
            }

            foreach (var v in ee.Triangulation.SourceTriangle)
            {
                if (v.X.Equals(posF.X) && v.Z.Equals(posF.Y))
                {
                    //Update Source Triangle
                    ee.Triangulation.SourceTriangle =
                        ee.Triangulation.SourceTriangle.SetItem(edgeIdx, newPos);
                    break;
                }

                edgeIdx++;
            }

            //Update Vertex and Reverse Vertex mappings
            var idxOfFirstVertexInEdge = ee.Triangulation
                .SubEdges.ElementAt(ee.Triangulation.Edges[edgeIdx].First!.Value).Key.Item1;

            var oldPos = ee.Triangulation.Vertices[idxOfFirstVertexInEdge];
            //Swap oldPos with newPos
            if (!oldPos.X.AlmostEqual(newPos.X) || !oldPos.Z.AlmostEqual(newPos.Z))
            {
                throw new InvalidOperationException("Positions do not match");
            }

            ee.Triangulation.Vertices.Remove(idxOfFirstVertexInEdge);
            ee.Triangulation.ReverseVertices.Remove(oldPos);
            //
            ee.Triangulation.Vertices[idxOfFirstVertexInEdge] = newPos;
            ee.Triangulation.ReverseVertices[newPos] = idxOfFirstVertexInEdge;
        }
    }

    /// <summary>
    /// Updates the triangulation values that are obtained by computing cell average (triIdx==0 <=>cellCenter)
    /// that are affected by a change in data values
    /// </summary>
    /// <param name="dataIndexes">the indices of the affected data values </param>
    public void UpdateCellCenterValuesInTris(ICollection<Vector3I> dataIndexes)
    {
        var affectedCellSummitsList = dataIndexes
            .SelectMany(dataIndex =>
            {
                var pos = Chunk.DataGrid.OrientationSystem.GetCellCentroid(dataIndex);
                var posF = new Vector2((float)pos.X, (float)pos.Y);
                _edgeGraphNodesMapByPosition.TryGetValue(posF, out var graphNode);
                if (graphNode is CellCenterPoint or null) return [];
                return graphNode.RelatedEdgeExtremities.Values;
            })
            .Distinct()
            .GroupBy(ee => ee.CellIndex);

        //Iterate over the edges for each cell
        foreach (var cellGroup in affectedCellSummitsList)
        {
            var cell = Chunk.TerrainDualGrid.CompleteCells[cellGroup.Key];
            var newCenterPos = new Vector3((float)cell.CenterPosition.X,
                cell.AverageHeight,
                (float)cell.CenterPosition.Y);
            const int edgeIdx = 0;
            foreach (var ee in cellGroup)
            {
                if (ee.Triangulation is null)
                {
                    throw new InvalidOperationException($"Triangulation is not set for{ee.CellIndex};" +
                                                        $"({ee.CellSummitIndex},{ee.OtherCellSummitIndex})");
                } //Update Source Triangle

                ee.Triangulation.SourceTriangle = ee.Triangulation.SourceTriangle.SetItem(edgeIdx, newCenterPos);
                //Update Vertex and Reverse Vertex mappings

                var idxOfFirstVertexInEdge = ee.Triangulation
                    .SubEdges.ElementAt(ee.Triangulation.Edges[edgeIdx].First!.Value).Key.Item1;

                var oldPos = ee.Triangulation.Vertices[idxOfFirstVertexInEdge];
                if (!oldPos.X.AlmostEqual(newCenterPos.X) || !oldPos.Z.AlmostEqual(newCenterPos.Z))
                {
                    throw new InvalidOperationException("Positions do not match");
                }

                //Swap oldPos with newPos
                ee.Triangulation.Vertices.Remove(idxOfFirstVertexInEdge);
                ee.Triangulation.ReverseVertices.Remove(oldPos);
                //
                ee.Triangulation.Vertices[idxOfFirstVertexInEdge] = newCenterPos;
                ee.Triangulation.ReverseVertices[newCenterPos] = idxOfFirstVertexInEdge;
            }
        }
    }

    /// <summary>
    /// Sets the triangulation to the proper entries of the graph's nodes.
    /// </summary>
    /// <param name="cell"></param>
    /// <param name="cellSummit"></param>
    /// <param name="triangle"></param>
    public void RegisterTriangulationInGraph(HexTerrainCell cell, int cellSummit, Triangulation triangle)
    {
        foreach (var edgeGraphNode in Vertices)
        {
            var posF = edgeGraphNode.Position;
            var source2D = triangle.SourceTriangle.Select(v3 => new Vector2(v3.X, v3.Z));
            //The triangle does not contain the vertex node
            var match = source2D.Index().Where(v => v.Item.IsEqualApprox(posF));

            if (!match.Any()) continue;
            var idxSelf = match.First().Index;

            var list = edgeGraphNode.RelatedEdgeExtremities.Where(ee => ee.Value.Triangulation == null).ToList();
            foreach (var ee in list)
            {
                if (ee.Value.CellIndex != cell.CellCoordsImplicit) continue;
                //We are in the same cell
                if (ee.Value.OtherCellSummitIndex != cellSummit && ee.Value.CellSummitIndex != cellSummit) continue;
                //We are touching the provided cell summit
                var otherIndex = ee.Value.OtherCellSummitIndex;
                var posOther = otherIndex == -1
                    ? new Vector2((float)cell.CenterPosition.X, (float)cell.CenterPosition.Y)
                    : new Vector2((float)cell.VertexPositionsInPlane[otherIndex].X,
                        (float)cell.VertexPositionsInPlane[otherIndex].Y);
                match = source2D.Index().Where(v => v.Item.IsEqualApprox(posOther));
                if (!match.Any()) continue;
                var idxOther = match.First().Index;
                if (idxOther == idxSelf)
                {
                    throw new InvalidOperationException();
                }

                int triIdx = idxSelf;
                ee.Value.Triangulation = triangle;
                ee.Value.IndexInTriangulation = triIdx;
            }
        }
    }
}