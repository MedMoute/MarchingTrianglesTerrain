using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
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

    /// <summary>
    /// Record holding the chunk-based indices of a triangulation vertex. 
    /// </summary>
    /// <param name="TriId">Index of the triangulation the vertex is part of.</param>
    /// <param name="TeIds">Index of the triangulation edges touching the triangulation vertex. </param>
    /// <param name="Idx">Index of the triangulation vertex</param>
    public record VertexTriangulationData(int TriId, int[] TeIds, int Idx)
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

    /// <summary>
    /// Record holding the chunk-based indices of a triangulation edge. 
    /// </summary>
    /// <param name="TriId">Index of the triangulation the vertex is part of.</param>
    /// <param name="TriVIds">Index of the triangulation vertices touching the triangulation edge. </param>
    /// <param name="EdgeIdx">Index of the triangulation edge.</param>
    public record EdgeTriangulationData(int TriId, int[] TriVIds, int EdgeIdx)
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
        public Triangulation Triangulation;
        public (int, int, int) TriVertices;
        public (int, int, int) TriEdges;
    }

    public Dictionary<int, VertexTriangulationData> TriangulationVerticesById = new();
    public Dictionary<int, GraphTriangulation> GraphTriangulationsById = new();
    public Dictionary<int, EdgeTriangulationData> TriangulationsEdgesById = new();

    private readonly Dictionary<Vector2I, Triangulation?[]?> _triangulationsMap = new();
    private readonly Dictionary<Vector2, ChunkTerrainNode> _edgeGraphNodesMapByPosition = new(V2CloseComp.Instance);

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

        foreach (var tv in graphNode.TriangulationVertices.Values)
        {
            var edgeIdx = 0;
            if (tv.Triangulation is null)
            {
                throw new InvalidOperationException($"Triangulation is not set for {tv.Cell.CellCoordsImplicit};" +
                                                    $"({tv.CellSummitIndex})");
            }

            foreach (var v in tv.Triangulation.SourceTriangle)
            {
                if (v.X.Equals(posF.X) && v.Z.Equals(posF.Y))
                {
                    //Update Source Triangle
                    tv.Triangulation.SourceTriangle =
                        tv.Triangulation.SourceTriangle.SetItem(edgeIdx, newPos);
                    break;
                }

                edgeIdx++;
            }

            //Update Vertex and Reverse Vertex mappings
            var idxOfFirstVertexInEdge = tv.Triangulation
                .SubEdges.ElementAt(tv.Triangulation.Edges[edgeIdx].First!.Value).Key.Item1;

            var oldPos = tv.Triangulation.Vertices[idxOfFirstVertexInEdge];
            //Swap oldPos with newPos
            if (!oldPos.X.AlmostEqual(newPos.X) || !oldPos.Z.AlmostEqual(newPos.Z))
            {
                throw new InvalidOperationException("Positions do not match");
            }

            tv.Triangulation.Vertices.Remove(idxOfFirstVertexInEdge);
            tv.Triangulation.ReverseVertices.Remove(oldPos);
            //
            tv.Triangulation.Vertices[idxOfFirstVertexInEdge] = newPos;
            tv.Triangulation.ReverseVertices[newPos] = idxOfFirstVertexInEdge;
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
                return graphNode.TriangulationVertices.Values;
            })
            .Distinct()
            .GroupBy(tv => tv.Cell.CellCoordsImplicit);

        //Iterate over the edges for each cell
        foreach (var cellGroup in affectedCellSummitsList)
        {
            var cell = Chunk.TerrainDualGrid.CompleteCells[cellGroup.Key];
            var newCenterPos = new Vector3((float)cell.CenterPosition.X,
                cell.AverageHeight,
                (float)cell.CenterPosition.Y);
            const int edgeIdx = 0;
            foreach (var tv in cellGroup)
            {
                if (tv.Triangulation is null)
                {
                    throw new InvalidOperationException($"Triangulation is not set for{tv.Cell.CellCoordsImplicit};" +
                                                        $"({tv.CellSummitIndex})");
                } //Update Source Triangle

                tv.Triangulation.SourceTriangle = tv.Triangulation.SourceTriangle.SetItem(edgeIdx, newCenterPos);
                //Update Vertex and Reverse Vertex mappings

                var idxOfFirstVertexInEdge = tv.Triangulation
                    .SubEdges.ElementAt(tv.Triangulation.Edges[edgeIdx].First!.Value).Key.Item1;

                var oldPos = tv.Triangulation.Vertices[idxOfFirstVertexInEdge];
                if (!oldPos.X.AlmostEqual(newCenterPos.X) || !oldPos.Z.AlmostEqual(newCenterPos.Z))
                {
                    throw new InvalidOperationException("Positions do not match");
                }

                //Swap oldPos with newPos
                tv.Triangulation.Vertices.Remove(idxOfFirstVertexInEdge);
                tv.Triangulation.ReverseVertices.Remove(oldPos);
                //
                tv.Triangulation.Vertices[idxOfFirstVertexInEdge] = newCenterPos;
                tv.Triangulation.ReverseVertices[newCenterPos] = idxOfFirstVertexInEdge;
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
        //Instantiate the Triangulation-related nodes and edges
        for (int i = 0; i < 3; i++) //Start with the vertices
        {     
            //Get the position of each edge start
            var pos = triangle.GetFirstPointOfEdge(i);
            var pos2 = new Vector2(pos.X, pos.Z);
            var node = _edgeGraphNodesMapByPosition[pos2];
            var tVertex = new TriangulationVertex( node, cell, cellSummit, triangle, i);
            
            node.AddTriangulationVertex(tVertex);
        }

        for (int i = 0; i < 3; i++) //Do the edges
        {
            var posS = triangle.GetFirstPointOfEdge(i);
            var posS2 = new Vector2(posS.X, posS.Z);
            var nodeS = _edgeGraphNodesMapByPosition[posS2];

            var posE = triangle.GetFirstPointOfEdge((i+1)%3);
            var posE2 = new Vector2(posE.X, posE.Z);
            var nodeE = _edgeGraphNodesMapByPosition[posE2];

            var success = TryGetEdge(nodeS, nodeE,out var edge);
            if (!success)
            {
                throw new ArgumentException("Triangle summit data does not match any entry in edges", nameof(triangle));
            }

            var tuple = GetCellSummitsFromIndexSubCellAndEdgeIndex(cellSummit, i);
            var tEdge = new TriangulationEdge(cell, triangle, i,tuple,edge);
            
            edge.AddBorder(tEdge);
        }
    }

    /// <summary>
    /// Returns the cell Summit index tuple for the provided edge of a triangulation.
    /// This method assumes that the first vertex is the cell center.
    /// </summary>
    private (int s1, int s2) GetCellSummitsFromIndexSubCellAndEdgeIndex(int cellSummit, int i)
    {
        return i switch
        {
            0 => (-1, cellSummit),
            1 => (cellSummit, EngineUtils.Mod(cellSummit + 1, HexTerrainCell.VertexCount)),
            2 => (EngineUtils.Mod(cellSummit + 1, HexTerrainCell.VertexCount), -1),
            _ => throw new NotSupportedException()
        };
    }
    
    public void RegisterTriangulationDtos()
    {
        // Instantiate the DTOs for all triangulations
        foreach (var triangle in _triangulationsMap.SelectMany(tArr=> tArr.Value!))
        {
            if (triangle==null) continue;
            GraphTriangulation gt = new ()
            {
                Triangulation = triangle
            };

        }
        foreach (var node in Vertices)
        {
            foreach (var triangulationVertex in node.TriangulationVertices)
            {
                triangulationVertex.Value.RegisterVertexData();
            }
        }
        foreach (var edge in Edges)
        {
            foreach (var triangulationEdge in edge.GetSubCellBorders())
            {
                triangulationEdge.RegisterEdgeData();
            }
        }
    }
}