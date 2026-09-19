using System;
using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Action that appends a triangle on an edge of the triangulation.
/// This action may imply the creation of multiple sub-triangles if the edge is already split.
/// </summary>
public class AddTriangleOnBorderEdge(int edgeIdx, Vector3 p) : DelegatedTriangulationEditAction<HashSet<int>>
{
    /// <summary>
    /// Ensure the provided point is 
    /// </summary>
    /// <param name="t"></param>
    protected override void ValidateBefore(Triangulation t)
    {
        // Fetch first SubEdge's first vertex  and  last SubEdge's second vertex :
        var s = t.Vertices[t.SubEdges.GetAt(t.Edges[edgeIdx].First!.Value).Key.Item1];
        var e = t.Vertices[t.SubEdges.GetAt(t.Edges[edgeIdx].Last!.Value).Key.Item2];

        var triNormVector = (p - s).Cross(e - s);

        if (Vector3.Up.Dot(triNormVector) > 1e-5)
        {
            throw new ArgumentException("The provided triangulation cannot apply the current action as it would" +
                                        "change its convex hull projection into the XoZ plane " +
                                        " (point not on edge line when projected on xOz)");
        }
        
        var pVecS = new Vector2(p.X - s.X, p.Z - s.Z);
        var pVecE = new Vector2(p.X - e.X, p.Z - e.Z);

        var edge = new Vector2(e.X - s.X, e.Z - s.Z);

        var angle1 = pVecS.AngleTo(edge);
        var angle2 = pVecE.AngleTo(edge);

        if (MathF.Abs(angle1) > MathF.PI/2|| MathF.Abs(angle2) > MathF.PI/2)
        {
            throw new ArgumentException("The provided triangulation cannot apply the current action as it would" +
                                        "change its convex hull projection into the XoZ plane" +
                                        " (point not on edge segment)");
        }
    }

    protected override HashSet<int> DoApply(Triangulation t)
    {
        // fetch the sub-edges
        var affectedEdges = t.Edges[edgeIdx];
        HashSet<int> createdIndexes = new();
        foreach (var affectedEdge in affectedEdges)
        {
            var edge = t.SubEdges.GetAt(affectedEdge).Key;
            //build a triangle fan from the sub edge
            var action = new AddTriangleFan(edge, p);
            createdIndexes.Add(action.Apply(t));
        }

        return createdIndexes;
    }
}