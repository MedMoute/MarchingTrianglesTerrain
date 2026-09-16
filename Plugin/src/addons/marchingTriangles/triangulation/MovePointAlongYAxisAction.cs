using System;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

internal class MovePointAlongYAxisAction : DelegatedTriangulationEditAction
{
    private readonly Vector3 _initPoint;
    private readonly float _heightValue;

    /// <summary>
    /// Warning !!! Declaring this action will mutate the content of the triangle
    /// </summary>
    internal MovePointAlongYAxisAction(Vector3 initPoint, float heightValue, Vector3[] triangle)
    {
        _initPoint = initPoint;
        _heightValue = heightValue;
        int editedPoint = -1;
        int cursor = 0;
        foreach (var p in triangle)
        {
            if (p.IsEqualApprox(_initPoint))
            {
                editedPoint = cursor;
                triangle[editedPoint] = new Vector3(p.X, heightValue, p.Z);
            }

            cursor++;
        }

        if (editedPoint == -1)
        {
            throw new Exception("Could not find the point inside the provided triangle. Cannot apply the action");
        }
    }

    protected override Func<Triangulation, Triangulation> DelegateAction
    {
        get
        {
            return t =>
            {
                //Find indexes
                var found = t.ReverseVertices.TryGetValue(_initPoint, out var idx);
                if (!found)
                {
                    throw new Exception("Could not find the point inside the triangulation");
                }

                t.ReverseVertices.Remove(_initPoint);
                t.Vertices.Remove(idx);

                var newVertex = new Vector3(_initPoint.X, _heightValue, _initPoint.Z);
                t.Vertices.Add(idx, newVertex);
                t.ReverseVertices.Add(newVertex, idx);
                return t;
            };
        }
    }
}