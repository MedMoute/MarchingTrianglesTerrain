using System;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

/// <summary>
/// Defines a triangle from a pair of vertex indexes and a third point.
/// This action does not affect the edge's sub-edge array. 
/// </summary>
internal class AddTriangleFan((int,int) subEdge, Vector3 p) : DelegatedTriangulationEditAction
{
    protected override void DoApply(Triangulation t)
    {

        var edge = t.SubEdges.IndexOf(subEdge);
        var sIdx = subEdge.Item1;
        var eIdx = subEdge.Item2;
        if (edge == -1)
        {
            t.Debug();
            throw new Exception(String.Format(
                "Unexpected state : the edge ({0}, {1}) is supposed to already exist but was not found.", sIdx,
                eIdx));
        }

        //Register a new point
        int newIdx = t.Vertices.Count;
        t.Vertices.Add(newIdx, p);
        t.ReverseVertices.Add(p, newIdx);
       
        //Register the new edges
        t.SubEdges.Add((sIdx, newIdx), 1);
        t.SubEdges.Add((newIdx, eIdx), 1);
        //Update the edge counter of the initial edge
        t.SubEdges[(sIdx, eIdx)]++;

        t.TrianglesByVertices.Add(t.TrianglesByVertices.Count, [sIdx, newIdx, eIdx]);
    }
}