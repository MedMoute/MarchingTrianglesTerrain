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
public class ChunkTerrainEdge : EquatableUndirectedEdge<EdgeGraphNode>
{
    public new EdgeGraphNode Source { get; init; }
    public new EdgeGraphNode Target { get; init; }

    private SubCellBorder? _border1 = null;
    private SubCellBorder? _border2 = null;
    
    internal ChunkTerrainEdge(EdgeGraphNode source, EdgeGraphNode target) : base(source, target)
    {
        Source = source;
        foreach (var extremity in Source.RelatedEdgeExtremities)
        {
            extremity.Value.SetParent(this);
        }

        Target = target;
        foreach (var extremity in Target.RelatedEdgeExtremities)
        {
            extremity.Value.SetParent(this);
        }
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
                var cellCenterAsEdgeExtremity = new EdgeExtremity(centerPoint, idx, -1,i);
                centerPoint.AddEdgeExtremity(cellCenterAsEdgeExtremity);
                //We use this loop to also add the CellSummitPoints's edge extremities
                Vector2D cellSummitPositionD = fullCell.VertexPositionsInPlane[i];
                EdgeGraphNode matchingNode = cellSummits.Values.First(p => p.Position.IsEqualApprox(
                    new Vector2((float)cellSummitPositionD.X, (float)cellSummitPositionD.Y)));
                var cellSummitAsInteriorEdgeExtremity = new EdgeExtremity(matchingNode, idx, i,-1);
                var cellSummitAsExteriorEdgeExtremity =
                    new EdgeExtremity(matchingNode, idx, i,(i + 1) % HexTerrainCell.VertexCount);

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
                EdgeGraphNode matchingNextNode = cellSummits.Values.First(p => p.Position.IsEqualApprox(
                    new Vector2((float)cellSummitNextPositionD.X, (float)cellSummitNextPositionD.Y)));
                var cellNextSummitAsExteriorEdgeExtremity = new EdgeExtremity(matchingNextNode, idx, (i + 1) % HexTerrainCell.VertexCount,i);
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

    public void GetEdgeExtremitiesAsNodes(out EdgeGraphNode n1, out EdgeGraphNode n2)
    {
        n1 = Source;
        n2 = Target;
    }

    public void GetEdgeExtremities(
        out Dictionary<Vector3I, EdgeExtremity> e1,
        out Dictionary<Vector3I, EdgeExtremity> e2)
    {
        e1 = Source.RelatedEdgeExtremities;
        e2 = Target.RelatedEdgeExtremities;
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

/// <summary>
/// Abstract class representing the abstraction of a node in the edge graph.
/// In this particular context, these are points with a fixes XZ value, an editable Y value,
/// and can have multiple related edges.
/// </summary>
public abstract class EdgeGraphNode : IComparable<Vector2>,IComparable
{
    public abstract Vector2 Position { get; init; }

    public readonly Dictionary<Vector3I, EdgeExtremity> RelatedEdgeExtremities = new();

    public void AddEdgeExtremity(EdgeExtremity ee)
    {
        if (RelatedEdgeExtremities.Count >= 12)
            throw new NotSupportedException("A node should not have move than 12 extremities.");
        RelatedEdgeExtremities.Add(
            new Vector3I(
                ee.CellIndex.X,
                ee.CellIndex.Y,
                ee.OtherCellSummitIndex),
            ee);
    }

    protected abstract float GetValue();

    protected bool Equals(EdgeGraphNode other)
    {
        return Position.IsEqualApprox(other.Position);
    }

    public int CompareTo(Vector2 other)
    {
        return V2Comp.Instance.Compare(this.Position, other);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((EdgeGraphNode)obj);
    }

    public override int GetHashCode()
    {
        return Position.GetHashCode();
    }

    public int CompareTo(object? obj)
    {
        if (obj is EdgeGraphNode oNode)
        {
            return CompareTo(oNode.Position);
        } 
        throw new ArgumentException();
    }

    public static bool operator ==(EdgeGraphNode? left, EdgeGraphNode? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(EdgeGraphNode? left, EdgeGraphNode? right)
    {
        return !Equals(left, right);
    }
}

/// <summary>
/// Implementation of EdgeGraphNode for hexagonal cell centers.
/// </summary>
public sealed class CellCenterPoint : EdgeGraphNode
{
    public CellCenterPoint(HexTerrainCell cell)
    {
        _parentCell = cell;
        Position = new Vector2(
            (float)cell.CenterPosition.X,
            (float)cell.CenterPosition.Y);
    }

    private readonly HexTerrainCell _parentCell;

    public override Vector2 Position { get; init; }

    protected override float GetValue()
    {
        return _parentCell.AverageHeight;
    }
}

/// <summary>
/// Implementation of Edge Graph node for hexagonal cell summits.
/// </summary>
public sealed class CellSummitPoint : EdgeGraphNode
{
    private readonly Vector3I _dataGridIndex;

    private readonly TriangleGrid _dataGrid;

    public override Vector2 Position { get; init; }

    public CellSummitPoint(TriangleGrid dataGrid, Vector3I dataGridIndex)
    {
        _dataGridIndex = dataGridIndex;
        _dataGrid = dataGrid;
        var posD = dataGrid.OrientationSystem.GetCellCentroid(dataGridIndex);
        Position = new Vector2((float)posD.X, (float)posD.Y);
    }

    protected override float GetValue()
    {
        return _dataGrid.Data[_dataGridIndex];
    }
}

/// <summary>
/// Representation of an Edge within the context of being the border of a SubCell,
/// and subsequently tied to a triangulation and an edgeIdx within said triangulation. 
/// </summary>
public class SubCellBorder
{
    public SubCellBorder(HexTerrainCell cell,
        Triangulation triangulation,
        int edgeTriIndex,
        (int s1, int s2) cellSummits)
    {
        ParentCell = cell;
        Triangulation = triangulation;
        TriEdgeIdx = edgeTriIndex;
        CellSummitIndices = cellSummits;
    }


    public Triangulation Triangulation { get; init; }
    public  int TriEdgeIdx{ get; init; }

    private ChunkTerrainEdge _parentEdge;

    public  HexTerrainCell ParentCell{ get; init; }
    public  (int, int) CellSummitIndices{ get; init; }
    
    public void SetParentEdge(ChunkTerrainEdge edge)
    {
        if (_parentEdge!=null && !_parentEdge.Equals(edge)) throw new ArgumentException("Cannot set the parent edge twice with different values");
        _parentEdge = edge;
    }

    public ChunkTerrainEdge GetParentEdge()
    {
        return _parentEdge;
    }
}