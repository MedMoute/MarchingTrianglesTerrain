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
    }

    protected override int DoApply(Triangulation t)
    {
        // fetch the sub-edges
        var affectedSubEdges = t.Edges[_edgeIdx];
        OrderedDictionary<int, List<int>> createdIndexes = new();
        foreach (var affectedEdge in affectedSubEdges)
        {
            var edge = t.SubEdges.GetAt(affectedEdge).Key;
            //build a triangle fan from the sub edge
            // Walk along the implicit fan to obtain all the triangle fans to create
            List<(int, int)> borderSubEdges = FindBorderFromSubEdge(t, edge);
            createdIndexes.TryAdd(affectedEdge, new List<int>());
            foreach (var borderEdge in borderSubEdges)
            {
                var action = new AddTriangleFan(borderEdge, _p);
                createdIndexes[affectedEdge].Add(action.Apply(t));
            }
        }

        //Generate an enumeration of all the subEdges that created a point (indexed by the initiating SubEdge,
        //with the said creation as value v)
        //The enumeration is insertion-ordered thanks to the OrderedDictionary
        //Then we reverse it to get the latest sub edge and change it.
        var flattenedPointCreations = createdIndexes
            .SelectMany(e => e.Value.Select(v => (e.Key, v)))
            .Reverse();
        // Make sure all sub-actions have created the same vertex
        if (flattenedPointCreations.DistinctBy(v => v.v).Count() > 1)
        {
            throw new InvalidOperationException("The action created multiple vertices which is not expected");
        }
        // If there is more than one entry, each of the listed subEdge executed an action that generates a triangle,
        // We check all the created triangles and we need to make sure that the triangles that intersect with
        // preexisisting triangles are removed from the triangle list 
        
        var otherEdges = flattenedPointCreations.ToList();
       otherEdges.Remove(flattenedPointCreations.DistinctBy(v => v.v).First());

       otherEdges.ForEach(
           e=>
           {
               var triangles = t.TrianglesByVertices.Where(tri =>
                   tri.Value.Contains(e.v) &&
                   tri.Value.Contains(t.SubEdges.ElementAt(e.Key).Key.Item1) &&
                   tri.Value.Contains(t.SubEdges.ElementAt(e.Key).Key.Item1)).ToList();
               if (triangles.Count != 1)
                   throw new InvalidOperationException("There should have been a matching triangle here");
               var triangleIdx = triangles[0].Key;
               var triangle = triangles[0].Value;
               var trianglePos = triangle.Select(idx => t.Vertices[idx]).ToArray();
               bool isIntersecting = t.TrianglesByVertices.Any(tri =>
               {
                   var n = TriangleUtils.GetNormal(trianglePos);
                   Vector3[] triPos = tri.Value.Select(idx => t.Vertices[idx]).ToArray();
                   if (!n.IsEqualApprox(TriangleUtils.GetNormal(triPos)))
                   {
                       return false;
                   }

                   return TriangleUtils.CoplanarTrianglesIntersect(trianglePos, triPos);
               });
               if (!isIntersecting)
               {
                   return;
               }
               
               if (triangle.Count !=3)
                   throw new InvalidOperationException("WTF");
               for (int i = 0; i < triangle.Count; i++)
               {
                   //Reduce edge count for the triangle's edges
                   t.SubEdges[(triangle[i], triangle[EngineUtils.mod(i + 1, triangle.Count)])]--;
               }
               //Remove triangle
               t.TrianglesByVertices.Remove(triangleIdx);
           });
       

        foreach (var placeValuePair in flattenedPointCreations.DistinctBy(v => v.v))
        {
            var affectedSubEdge = placeValuePair.Key;
            (int, int) subEdgeIdxs = t.SubEdges.ElementAt(affectedSubEdge).Key;
            var newPointIdx = placeValuePair.v;
            var node = t.Edges[_edgeIdx].Find(affectedSubEdge);
            if (node != null)
            {
                var newFirstHalfNode=t.Edges[_edgeIdx].AddAfter(node, t.SubEdges.IndexOf((subEdgeIdxs.Item1, newPointIdx)));
                t.Edges[_edgeIdx].AddAfter(newFirstHalfNode, t.SubEdges.IndexOf((newPointIdx, subEdgeIdxs.Item2)));
                t.Edges[_edgeIdx].Remove(node);
            }
        }

        if (createdIndexes.Count == 0)
        {
            throw new InvalidOperationException("The action created no vertices which is not expected");
        }

        return flattenedPointCreations.First().v;
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