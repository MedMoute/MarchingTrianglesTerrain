using System;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

/// <summary>
/// Defines a triangle from a pair of vertex indexes and a third point.
/// This action does not affect the edge's sub-edge array. 
/// </summary>
public class AddTriangleFan : DelegatedTriangulationEditAction<int>
{
    private readonly (int, int) _subEdge;
    private readonly Vector3 _pos;

    public AddTriangleFan((int, int) subEdge, Vector3 p)
    {
        if (subEdge.Item1 < 0 || subEdge.Item2 < 0)
        {
            throw new ArgumentException("Illegal index in " + subEdge, nameof(subEdge));
        }

        _subEdge = subEdge;
        _pos = p;
    }

    protected override void ValidateBefore(Triangulation t)
    {
        base.ValidateBefore(t);
        var edge = t.SubEdges.IndexOf(_subEdge);
        if (edge == -1)
        {
            throw new ArgumentException("The provided sub edge does not exist.");
        }

        if (t.SubEdges[_subEdge] > 1)
        {
            throw new NotSupportedException("Adding a triangle fan to an edge that is not on the manifold border is not supported.");

        }
    }

    protected override int DoApply(Triangulation t)
    {
        var edge = t.SubEdges.IndexOf(_subEdge);
        var sIdx = _subEdge.Item1;
        var eIdx = _subEdge.Item2;
        if (edge == -1)
        {
            t.Debug();
            throw new Exception(String.Format(
                "Unexpected state : the edge ({0}, {1}) is supposed to already exist but was not found.", sIdx,
                eIdx));
        }

        //Register a new point
        int newIdx = t.Vertices.Count;
        t.Vertices.Add(newIdx, _pos);
        t.ReverseVertices.Add(_pos, newIdx);

        //Register the new edges
        t.SubEdges.Add((sIdx, newIdx), 1);
        t.SubEdges.Add((newIdx, eIdx), 1);
        //Update the edge counter of the initial edge
        t.SubEdges[(sIdx, eIdx)]++;

        t.TrianglesByVertices.Add(t.TrianglesByVertices.Count, [sIdx, newIdx, eIdx]);
        return newIdx;
    }
}