using System;
using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.graph;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// Abstract class representing the abstraction of a node in the edge graph.
/// In this particular context, these are points with a fixes XZ value, an editable Y value,
/// and can have multiple related edges.
/// </summary>
public abstract class ChunkTerrainNode : IComparable<Vector2>,IComparable,IImplicitlyBuildableFromGraph<Vector2>
{
    public abstract Vector2 Position { get; init; }

    public readonly Dictionary<Vector3I, TriangulationVertex> TriangulationVertices = new();

    protected ChunkTerrainNode(TerrainChunkGraph parentGraph)
    {
        ParentGraph = parentGraph;
    }

    public void AddTriangulationVertex(TriangulationVertex tv)
    {
        if (TriangulationVertices.Count >= 12)
            throw new NotSupportedException("A node should not have move than 12 extremities.");
        TriangulationVertices.Add(
            new Vector3I(
                tv.Cell.CellCoordsImplicit.X,
                tv.Cell.CellCoordsImplicit.Y,
                tv.CellSummitIndex),
            tv);
    }
    
    protected abstract float GetValue();
    
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

    public TerrainChunkGraph ParentGraph { get; }

}

/// <summary>
/// Implementation of EdgeGraphNode for hexagonal cell centers.
/// </summary>
public sealed class CellCenterPoint : ChunkTerrainNode
{
    public CellCenterPoint(HexTerrainCell cell,TerrainChunkGraph graph):base(graph)
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
public sealed class CellSummitPoint : ChunkTerrainNode
{
    private readonly Vector3I _dataGridIndex;

    private readonly TriangleGrid _dataGrid;

    public override Vector2 Position { get; init; }

    public CellSummitPoint(TriangleGrid dataGrid, Vector3I dataGridIndex,TerrainChunkGraph graph):base(graph)
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