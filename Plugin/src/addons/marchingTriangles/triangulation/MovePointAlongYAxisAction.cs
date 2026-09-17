using System;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

internal class MovePointAlongYAxisAction : DelegatedTriangulationEditAction
{
    private readonly int _editedVertexI;
    private readonly float _heightValue;

    /// <summary>
    /// 
    /// </summary>
    internal MovePointAlongYAxisAction(int editedPoint, float heightValue)
    {
        if (_editedVertexI < 0)
        {
            throw new Exception(
                "Could not find the point inside the provided triangulation. Cannot register the action");
        }

        _editedVertexI = editedPoint;
        _heightValue = heightValue;
    }

    protected override Triangulation doApply(Triangulation t)
    {
        var found = t.Vertices.TryGetValue(_editedVertexI, out var pos);
        if (!found)
        {
            throw new Exception("Could not find the point inside the triangulation.");
        }

        t.ReverseVertices.Remove(pos);
        t.Vertices.Remove(_editedVertexI);
        var newVertex = new Vector3(pos.X, _heightValue, pos.Z);
        t.Vertices.Add(_editedVertexI, newVertex);
        t.ReverseVertices.Add(newVertex, _editedVertexI);
        return t;
    }

    //Find indexes
}