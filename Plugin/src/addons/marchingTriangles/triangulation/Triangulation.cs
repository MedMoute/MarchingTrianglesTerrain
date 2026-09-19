using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using static MarchingTrianglesTerrain.addons.marchingTriangles.utils.EngineUtils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Representation of the result of applying various "warping" operations to a triangle in 3D space.
/// </summary>
/// The operations may imply additional subdivisions of the triangulation, but it will remain a 2D-manifold.
/// The projection of the triangulation's convex hull into the XoY plane <b>CANNOT</b> be modified by any operation.
/// Essentially, valid operations include (but are not limited to)
///  - Moving a vertex of the triangulation along the Y axis
///  - Splitting an edge of the triangulation in two sub-edges
///  - Adding a "fan" to a "border edge" of the triangulation. (A "fan" means a triangle for which the normal vector
/// is orthogonal to the yAxis vector; and a "border edge" refers to edges that are of the edge of the manifold) 
public class Triangulation
{
    /// <summary>
    /// Vertex dictionary.
    /// </summary>
    internal readonly Dictionary<int, Vector3> Vertices = new();

    /// <summary>
    /// Vertex reverse lookup dictionary.
    /// </summary>
    internal readonly Dictionary<Vector3, int> ReverseVertices = new();

    /// <summary>
    /// (Vertex)index based representation of the triangulation. 
    /// </summary>
    internal readonly Dictionary<int, List<int>> TrianglesByVertices = new();

    /// <summary>
    /// Array representing each of the edges of the manifold.
    /// This representation relies on the sub-Edges.
    /// </summary>
    /// We will always have 3 edges since the represented manifold is a triangle.
    internal readonly LinkedList<int>[] Edges = new LinkedList<int>[3];

    /// <summary>
    /// Sub-edges dictionary.
    /// The index can be obtained via the IndexOf((int,int)) method
    /// The value is the amount of times the sub edge is used in the triangulation.
    /// </summary>
    internal readonly OrderedDictionary<(int, int), int> SubEdges = new(UnorderedTupleComparer.Instance);

    /// <summary>
    /// Position of the initial triangle's vertices.
    /// </summary>
    internal ImmutableArray<Vector3> SourceTriangle;

    private Dictionary<string, object>? _additionalHints;

    private readonly bool _verbose;
    private readonly bool _extensiveVerification;


    public Triangulation(Vector3[] triangle, bool verbose = false, bool extensiveVerification = false,
        Dictionary<string, object>? additionalHints = null)
    {
        if (triangle.Length != 3)
        {
            throw new ArgumentException("Wrong array size, expected 3, got " + triangle.Length, nameof(triangle));
        }

        if (HexTerrainCell.GetSignedArea(triangle) == 0)
        {
            throw new ArgumentException(
                "The projection of the input triangle on the xOz plane is a degenerated triangle." +
                " This is not supported", nameof(triangle));
        }

        SourceTriangle = [.. triangle];
        _additionalHints = additionalHints;
        _verbose = verbose;
        _extensiveVerification = extensiveVerification;
        for (int i = 0; i < 3; i++)
        {
            Vertices.Add(i, triangle[i]);
            ReverseVertices.Add(triangle[i], i);
            (int, int) implicitEdge = (i, mod(i + 1, 3));
            SubEdges.TryAdd(implicitEdge, 1);
            Edges[i] = new LinkedList<int>();
            Edges[i].AddFirst(SubEdges.IndexOf(implicitEdge));
        }

        TrianglesByVertices.Add(0, [0, 1, 2]);
        EnsureIntegrity(true);
    }

    public List<HexTerrainCell.TriangleInfo> ToTriangleInfoList()
    {
        var edgeDico = new OrderedDictionary<(int, int), int>(UnorderedTupleComparer.Instance);
        for (int j = 0; j < TrianglesByVertices.Count; j++)
        {
            for (int k = 0; k < 3; k++)
            {
                edgeDico.TryAdd(
                    (TrianglesByVertices[j][k],
                        TrianglesByVertices[j][mod(k + 1, 3)]), 0);
                edgeDico[(
                    TrianglesByVertices[j][k],
                    TrianglesByVertices[j][mod(k + 1, 3)])]++;
            }
        }

        List<HexTerrainCell.TriangleInfo> result = [];
        for (int i = 0; i < TrianglesByVertices.Count; i++)
        {
            var x = new HexTerrainCell.TriangleInfo
            {
                Points = [.. TrianglesByVertices[i].Select(key => Vertices[key])],
                edgeBorderFlags = TrianglesByVertices[i].Select(key => (
                        key,
                        TrianglesByVertices[i][(TrianglesByVertices[i].IndexOf(key) + 1) % 3]))
                    .Select(e => edgeDico[e] != 2).ToArray()
            };
            result.Add(x);
        }

        return result;
    }

    public void Debug(string title = "")
    {
        if (!_verbose)
        {
            return;
        }

        Console.WriteLine(">> Debug Triangulation " + title);
        Console.WriteLine("Triangles : " + TrianglesByVertices.Count);

        foreach (var triAsVertList in TrianglesByVertices)
        {
            var triEdges = triAsVertList.Value.Select(key => SubEdges.IndexOf((
                key,
                triAsVertList.Value[(triAsVertList.Value.IndexOf(key) + 1) % 3]))).ToList();
            Console.WriteLine("T[" + triAsVertList.Key + "] " + "Points : " + String.Join(",", triAsVertList.Value) +
                              " | Implicitly defined Edges : " + String.Join(",", triEdges));
        }

        var edgeDico = new OrderedDictionary<(int, int), int>(UnorderedTupleComparer.Instance);
        for (var j = 0; j < TrianglesByVertices.Count; j++)
        {
            for (var k = 0; k < 3; k++)
            {
                edgeDico.TryAdd(
                    (TrianglesByVertices[j][k],
                        TrianglesByVertices[j][mod(k + 1, 3)]), 0);
                edgeDico[(
                    TrianglesByVertices[j][k],
                    TrianglesByVertices[j][mod(k + 1, 3)])]++;
            }
        }

        for (var i = 0; i < TrianglesByVertices.Count; i++)
        {
            var edgeBorderFlags = TrianglesByVertices[i].Select(key => (
                    key,
                    TrianglesByVertices[i][(TrianglesByVertices[i].IndexOf(key) + 1) % 3]))
                .Select(e => edgeDico[e] != 2)
                .ToArray();
            Console.WriteLine("T[" + i + "] " + "IsEdgeBorder : " + string.Join(",", edgeBorderFlags));
        }

        Console.WriteLine("SubEdges : " + SubEdges.Count);
        foreach (var edge in SubEdges)
        {
            Console.WriteLine("SubEdge[" + SubEdges.IndexOf(edge.Key) + "] " + edge.Key + " used " + edge.Value +
                              " times.");
        }

        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine("Edge " + i + "  : [" + String.Join(",", Edges[i])+ "]");
        }
    }

    public void EnsureIntegrity(bool forceChecks = false)
    {
        if (forceChecks || _extensiveVerification)
        {
            try
            {
                DoExtensiveChecksOnTriangulation();
            }
            catch (Exception e)
            {
                Debug("Exception Caught : " + e.Message);
                throw;
            }
        }
    }

    private void DoExtensiveChecksOnTriangulation()
    {
        //Ensure the convex hull wasn't affected
        var area = HexTerrainCell.GetSignedArea([.. SourceTriangle]);
        var sumOfTrianglesArea =
            TrianglesByVertices.Sum(kvp => HexTerrainCell.GetSignedArea([.. kvp.Value.Select(i => Vertices[i])]));
        if (Math.Abs(area - sumOfTrianglesArea) > 1e-5)
        {
            throw new Exception("The last action affected the convex hull area !!");
        }

        //Check edge continuity
        for (int i = 0; i < 3; i++)
        {
            var firstSubEdge = Edges[i].First!.Value;
            var lastSubEdgeOfPrevEdge = Edges[mod(i - 1, 3)].Last!.Value;

            if (SubEdges.ElementAt(firstSubEdge).Key.Item1 != SubEdges.ElementAt(lastSubEdgeOfPrevEdge).Key.Item2)
                throw new Exception(
                    string.Format("Continuity Error between border edges {0} and {1}", mod(i - 1, 3), i));

            //Check sub edge border continuity
            var enumerator = Edges[i].GetEnumerator();
            enumerator.MoveNext();
            for (int j = 0; j < Edges.Length; j++)
            {
                var subEdge = enumerator.Current;
                if (enumerator.MoveNext())
                {
                    var nextSubEdge = enumerator.Current;
                    if (SubEdges.ElementAt(subEdge).Key.Item2 != SubEdges.ElementAt(nextSubEdge).Key.Item1)
                    {
                        throw new Exception(string.Format("Continuity Error between border sub edges {0} and {1}",
                            subEdge, nextSubEdge));
                    }
                }
            }
        }

        // Check vertex dictionaries
        if (Vertices.Count != ReverseVertices.Count)
        {
            throw new Exception("Vertex dictionaries have inconsistent sizes.");
        }

        foreach (var kvp in Vertices)
        {
            if (ReverseVertices[kvp.Value] != kvp.Key)
            {
                throw new Exception("Vertex dictionaries have inconsistent data.");
            }
        }
    }
}