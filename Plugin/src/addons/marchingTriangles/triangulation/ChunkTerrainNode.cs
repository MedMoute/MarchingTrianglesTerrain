using System;
using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// Abstract class representing the abstraction of a node in the edge graph.
/// In this particular context, these are points with a fixes XZ value, an editable Y value,
/// and can have multiple related edges.
/// </summary>
public abstract class ChunkTerrainNode : IComparable<Vector2>,IComparable
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

    public List<TerrainChunkGraph.TriangulationVertex> subVertices;

    protected abstract float GetValue();

    public abstract void BuildTriangulationStructsFromChunk(TerrainChunkGraph graph);

    private bool Equals(ChunkTerrainNode other)
    {
        return Position.IsEqualApprox(other.Position);
    }

    public int CompareTo(Vector2 other)
    {
        return V2Comp.Instance.Compare(Position, other);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ChunkTerrainNode)obj);
    }

    public override int GetHashCode()
    {
        return Position.GetHashCode();
    }

    public int CompareTo(object? obj)
    {
        if (obj is ChunkTerrainNode oNode)
        {
            return CompareTo(oNode.Position);
        } 
        throw new ArgumentException();
    }

    public static bool operator ==(ChunkTerrainNode? left, ChunkTerrainNode? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ChunkTerrainNode? left, ChunkTerrainNode? right)
    {
        return !Equals(left, right);
    }
}

/// <summary>
/// Implementation of EdgeGraphNode for hexagonal cell centers.
/// </summary>
public sealed class CellCenterPoint : ChunkTerrainNode
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

    public override void BuildTriangulationStructsFromChunk(TerrainChunkGraph graph)
    {
        
    }
}

/// <summary>
/// Implementation of Edge Graph node for hexagonal cell summits.
/// </summary>
public sealed class CellSummitPoint : ChunkTerrainNode
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

    public override void BuildTriangulationStructsFromChunk(TerrainChunkGraph graph)
    {
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