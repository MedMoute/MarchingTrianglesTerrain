using System;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

internal class AddTriangleFan(Vector3 p0, Vector3 p1, Vector3 p2) : DelegatedTriangulationEditAction
{
    protected override Triangulation doApply(Triangulation t)
    {
        //Find indexes
        var foundStart = t.ReverseVertices.TryGetValue(p0, out var p0Idx);
        var foundEnd = t.ReverseVertices.TryGetValue(p1, out var p1Idx);
        if (!foundStart || !foundEnd)
        {
            throw new Exception(
                "Could not find one of the split end points inside the triangulation");
        }


        var edge = t.SubEdges.IndexOf((p0Idx, p1Idx));
        if (edge == -1)
        {
            t.Debug();
            throw new Exception(String.Format(
                "Unexpected state : the edge ({0}, {1}) is supposed to already exist but was not found.", p0Idx,
                p1Idx));
        }

        //Register a new point
        int newIdx = t.Vertices.Count;
        t.Vertices.Add(newIdx, p2);
        t.ReverseVertices.Add(p2, newIdx);
        //Add the new edges
        t.SubEdges.Add((p0Idx, newIdx), 1);
        t.SubEdges.Add((newIdx, p1Idx), 1);
        //Update the edge counter
        t.SubEdges[(p0Idx, p1Idx)]++;

        var newEdge0idx = t.SubEdges.IndexOf((p0Idx, newIdx));
        var newEdge1idx = t.SubEdges.IndexOf((newIdx, p1Idx));

        t.TrianglesByVertices.Add(t.TrianglesByVertices.Count, [p0Idx, newIdx, p1Idx]);
        return t;
    }
}