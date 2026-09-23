using System;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Action that Displaces a triangulation's vertex along the Y axis to a provided height.
/// </summary>
public class MovePointAlongYAxisAction : DelegatedTriangulationEditAction<int>
{
    private readonly int _editedVertexI;
    private readonly float _heightValue;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="editedPoint">the edited vertex index</param>
    /// <param name="heightValue">the set height</param>
    /// <exception cref="ArgumentException">If the provided index is strictly negative</exception>
    public MovePointAlongYAxisAction(int editedPoint, float heightValue)
    {
        _editedVertexI = editedPoint;

        if (_editedVertexI < 0)
        {
            throw new ArgumentException("Illegal index " + editedPoint, nameof(editedPoint));
        }

        _heightValue = heightValue;
    }

    protected override int DoApply(Triangulation t)
    {
        var found = t.Vertices.TryGetValue(_editedVertexI, out var pos);
        if (!found)
        {
            throw new ArgumentException("Could not find the point inside the triangulation.");
        }

        t.ReverseVertices.Remove(pos);
        t.Vertices.Remove(_editedVertexI);
        var newVertex = new Vector3(pos.X, _heightValue, pos.Z);
        if (t.ReverseVertices.TryAdd(newVertex, _editedVertexI))
        {
            t.Vertices.TryAdd(_editedVertexI, newVertex);
        }
        //TODO :  support edge and triiangle deletion here by re adressingg the content
        return t.ReverseVertices[newVertex];
    }
}