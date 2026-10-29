using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MathNet.Spatial.Euclidean;
using QuikGraph;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// Representation of an edge of a HexTerrainChunk, as the geometrical relation between two linked vertices.
///  
/// </summary>
public class ChunkTerrainEdge : EquatableUndirectedEdge<ChunkTerrainNode>
{
    public new ChunkTerrainNode Source { get; init; }
    public new ChunkTerrainNode Target { get; init; }

    private SubCellBorder? _border1 = null;
    private SubCellBorder? _border2 = null;
    
    internal ChunkTerrainEdge(ChunkTerrainNode source, ChunkTerrainNode target) : base(source, target)
    {
        Source = source;
        Target = target;

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
            var idx = fullCell.CellCoordsImplicit;
            var centerPoint = cellCenters[idx];
            for (int i = 0; i < HexTerrainCell.VertexCount; i++)
            {
                //Build interior edges

                //We first create the extremities of the edge and add them to the matching graph vertices 
                var cellCenterAsEdgeExtremity = new EdgeExtremity(centerPoint, idx, -1);
                centerPoint.AddEdgeExtremity(cellCenterAsEdgeExtremity);
                //We use this loop to also add the CellSummitPoints's edge extremities
                Vector2D cellSummitPositionD = fullCell.VertexPositionsInPlane[i];
                ChunkTerrainNode matchingNode = cellSummits.Values.First(p => p.Position.IsEqualApprox(
                    new Vector2((float)cellSummitPositionD.X, (float)cellSummitPositionD.Y)));
                var cellSummitAsInteriorEdgeExtremity = new EdgeExtremity(matchingNode, idx, i);
                var cellSummitAsExteriorEdgeExtremity =
                    new EdgeExtremity(matchingNode, idx, i);

                matchingNode.AddEdgeExtremity(cellSummitAsInteriorEdgeExtremity);
                matchingNode.AddEdgeExtremity(cellSummitAsExteriorEdgeExtremity);

                //We build the edge instance 
                var intEdge = centerPoint.CompareTo(matchingNode)< 0 ? 
                    new ChunkTerrainEdge(centerPoint, matchingNode):
                    new ChunkTerrainEdge(matchingNode,centerPoint);
                edges.Add(intEdge);

                //Build the exterior edges
                Vector2D cellSummitNextPositionD =
                    fullCell.VertexPositionsInPlane[(i + 1) % HexTerrainCell.VertexCount];
                ChunkTerrainNode matchingNextNode = cellSummits.Values.First(p => p.Position.IsEqualApprox(
                    new Vector2((float)cellSummitNextPositionD.X, (float)cellSummitNextPositionD.Y)));
                var cellNextSummitAsExteriorEdgeExtremity = new EdgeExtremity(matchingNextNode, idx, (i + 1) % HexTerrainCell.VertexCount);
                matchingNextNode.AddEdgeExtremity(cellNextSummitAsExteriorEdgeExtremity);
                var extEdge = matchingNode.CompareTo(matchingNextNode) < 0 ? 
                    new ChunkTerrainEdge(matchingNode, matchingNextNode) :
                    new ChunkTerrainEdge( matchingNextNode,matchingNode);
                edges.Add(extEdge); 

            }
        }

        return edges;
    }
    
    public List<SubCellBorder> GetSubCellBorders()
    {
        if (_border1 == null)
        {
            if (_border2 == null) return [];
            return [_border2];
        }

        if (_border2 == null) return [_border1];
        return [_border1,_border2];
    }

    public void AddBorder(SubCellBorder subCellBorder)
    {
        if (_border1==null)
        {
            _border1 = subCellBorder;
        }
        else if (_border2 == null)
        {
            _border2 = subCellBorder;
        }
        else
        {
            throw new NotSupportedException("Two borders are already on this edge");
        }
    }
}

