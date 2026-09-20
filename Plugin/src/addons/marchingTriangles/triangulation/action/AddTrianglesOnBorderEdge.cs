using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Action that appends a triangle on an edge of the triangulation.
/// This action may imply the creation of multiple sub-triangles if the edge is already split.
/// </summary>
public class AddTrianglesOnBorderEdge : DelegatedTriangulationEditAction<int>
{
    private readonly int _edgeIdx;
    private readonly Vector3 _p;

    /// <summary>
    /// Action that appends a triangle on an edge of the triangulation.
    /// This action may imply the creation of multiple sub-triangles if the edge is already split.
    /// </summary>
    public AddTrianglesOnBorderEdge(int edgeIdx, Vector3 p)
    {
        _edgeIdx = edgeIdx;
        _p = p;

        if (edgeIdx < 0 || edgeIdx >= 3)
        {
            throw new ArgumentOutOfRangeException("edgeIdx", edgeIdx, "Should have a value between 0 and 2");
        }
    }

    /// <summary>
    /// Ensure the provided point is valid, (i.e. its projection on the xOz plane is on the edge segment)
    /// </summary>
    /// <param name="t"></param>
    protected override void ValidateBefore(Triangulation t)
    {
        // Fetch first SubEdge's first vertex  and  last SubEdge's second vertex :
        var s = t.Vertices[t.SubEdges.GetAt(t.Edges[_edgeIdx].First!.Value).Key.Item1];
        var e = t.Vertices[t.SubEdges.GetAt(t.Edges[_edgeIdx].Last!.Value).Key.Item2];

        var triNormVector = (_p - s).Cross(e - s);

        if (Vector3.Up.Dot(triNormVector) > 1e-5)
        {
            throw new ArgumentException("The provided triangulation cannot apply the current action as it would" +
                                        "change its convex hull projection into the XoZ plane " +
                                        " (point not on edge line when projected on xOz)");
        }

        var pVecS = new Vector2(_p.X - s.X, _p.Z - s.Z);
        var pVecE = new Vector2(_p.X - e.X, _p.Z - e.Z);

        var edge = new Vector2(e.X - s.X, e.Z - s.Z);

        var angle1 = pVecS.AngleTo(edge);
        var angle2 = pVecE.AngleTo(edge);

        if (
            !(pVecE.LengthSquared() < 1e-5 ||
              pVecS.LengthSquared() < 1e-5) //Degenerated case exclusion (new point is start or end + value on Y axis)  
            && (MathF.Abs(angle1) > MathF.PI / 2 || MathF.Abs(angle2) > MathF.PI / 2))
        {
            throw new ArgumentException("The provided triangulation cannot apply the current action as it would" +
                                        " change its convex hull projection into the XoZ plane" +
                                        " (point not on edge segment)");
        }

        //Check if new point already exists
        if (t.ReverseVertices.ContainsKey(_p))
        {
            throw new InvalidOperationException("The triangulation already contains this point");
        }
    }

    protected override int DoApply(Triangulation t)
    {
        // fetch the sub-edges
        var affectedSubEdges = t.Edges[_edgeIdx];
        HashSet<int> createdIndexes = new();
        foreach (var affectedEdge in affectedSubEdges)
        {
            var edge = t.SubEdges.GetAt(affectedEdge).Key;
            //build a triangle fan from the sub edge
            // Walk along the implicit fan to obtain all the triangle fans to create
            List<(int, int)> borderSubEdges = FindBorderFromSubEdge(t, edge);
            foreach (var borderEdge in borderSubEdges)
            {
                var action = new AddTriangleFan(borderEdge, _p);
                createdIndexes.Add(action.Apply(t));

            }
        }

        if (createdIndexes.Count > 1)
        {
            throw new InvalidOperationException("The action created multiple vertices which is not expected");
        }

        return createdIndexes.First();
    }

    private List<(int, int)> FindBorderFromSubEdge(Triangulation t, (int, int) edge)
    {
        List<(int, int)> collectedEdges = [];
        if (t.SubEdges[edge] == 1) //The provided sub edge is already on the border, return it.
        {
            collectedEdges.Add(edge);
        }
        else
        {
            //Otherwise find the outer triangle that use the subEdge and whose other edges are NOT in the manifold border
            var validTriangles = t.TrianglesByVertices.Where(kvp =>
                // SubEdge belong to triangle
                kvp.Value.Contains(edge.Item1) &&
                kvp.Value.Contains(edge.Item2) &&
                // Triangle's sub edges are on no more than one edge.
                t.Edges.Where(subEdges =>
                    subEdges.Contains(t.SubEdges.IndexOf((kvp.Value[0], kvp.Value[1]))) ||
                    subEdges.Contains(t.SubEdges.IndexOf((kvp.Value[1], kvp.Value[2]))) ||
                    subEdges.Contains(t.SubEdges.IndexOf((kvp.Value[2], kvp.Value[0])))
                ).Select(_ => 1).Sum() <= 1).ToList();
            var validEdges = validTriangles.SelectMany(tri =>
            {
                List<(int, int)> triSubEdge = new([
                    (tri.Value[0], tri.Value[1]),
                    (tri.Value[1], tri.Value[2]),
                    (tri.Value[2], tri.Value[0])
                ]);
                return triSubEdge;
            }).Where(subEdge => !UnorderedTupleComparer.Instance.Equals(subEdge, edge)).ToList();
            foreach (var subEdge in validEdges)
            {
                collectedEdges.AddRange(FindBorderFromSubEdge(t, subEdge));
            }
        }

        return collectedEdges;
    }
}