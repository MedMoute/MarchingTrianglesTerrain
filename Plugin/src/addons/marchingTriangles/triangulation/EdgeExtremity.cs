using System;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// The extremity of an edge (can be seen as a segment endpoint) within a subCell.
/// An edge extremity can be assimilated to a (vertex,direction) tuple within a triangulation.
/// </summary>
public class EdgeExtremity
{
    /// <summary>
    /// The edge graph node (i.e. vertex) this extremity leads to.
    /// </summary>
    public ChunkTerrainNode Node { get; init; }

    /// <summary>
    /// The representation of the edge extremity as a cell summit.
    /// </summary>
    public int CellSummitIndex { get; init; }

    public Vector2I CellIndex { get; init; }

    private Triangulation? _triangulation;

    public Triangulation? Triangulation
    {
        get => _triangulation;

        set
        {
            if (_triangulation == value || _triangulation is null && value != null)
            {
                _triangulation = value;
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }

    private int? _indexInTriangulation;

    public int? IndexInTriangulation     {
        get => _indexInTriangulation;

        set
        {
            if (_indexInTriangulation == value || _indexInTriangulation is null && value != null)
            {
                _indexInTriangulation = value;
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
    

    public EdgeExtremity(ChunkTerrainNode node, Vector2I cellIndex, int selfCellSummitIndex)
    {
        Node = node;
        CellSummitIndex = selfCellSummitIndex;
        CellIndex = cellIndex;
    }
    
}