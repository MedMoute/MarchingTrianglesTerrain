using System;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class MovePointAlongYAxisAction : DelegatedTriangulationEditAction
{
    private readonly int _editedVertexI;
    private readonly float _heightValue;
    
    public MovePointAlongYAxisAction(int editedPoint, float heightValue)
    {
        _editedVertexI = editedPoint;

        if (_editedVertexI < 0)
        {
            throw new ArgumentException("Illegal index "+editedPoint,nameof(editedPoint));
        }

        _heightValue = heightValue;
    }

    protected override void DoApply(Triangulation t)
    {
        var found = t.Vertices.TryGetValue(_editedVertexI, out var pos);
        if (!found)
        {
            throw new ArgumentException("Could not find the point inside the triangulation.");
        }

        t.ReverseVertices.Remove(pos);
        t.Vertices.Remove(_editedVertexI);
        var newVertex = new Vector3(pos.X, _heightValue, pos.Z);
        t.Vertices.Add(_editedVertexI, newVertex);
        t.ReverseVertices.Add(newVertex, _editedVertexI);
    }

    //Find indexes
}