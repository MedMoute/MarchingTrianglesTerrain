using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using UnitTests.test.processing;

namespace UnitTests;

public static class TestUtils
{
    internal static (Dictionary<float[], int> dico,
        List<float[]> borderEdgesAsSets,
        List<float[]>manifoldBorderEdgesAsSets) ReprocessEdgeGeometry(List<HexTerrainCell.TriangleInfo> outputTriangles)
    {
        var dico = new Dictionary<float[], int>(new FloatArrayComparer(1e-5));
        var borderEdgesAsSets = new Dictionary<float[], int>(new FloatArrayComparer(1e-5));

        var vertexSet = new SortedSet<Vector3>(V3Comp.Instance);
        foreach (var tInfo in outputTriangles)
        {
            foreach (var edge in tInfo.GetEdges())
            {
                var set = new SortedSet<Vector3>(V3Comp.Instance);
                vertexSet.Add(edge.Item1);
                vertexSet.Add(edge.Item2);
                set.Add(edge.Item1);
                set.Add(edge.Item2);
                float[] zob = set.SelectMany(v => Array.AsReadOnly([v.X, v.Y, v.Z])).ToArray();
                var exists = dico.TryGetValue(zob, out int val);
                if (exists)
                {
                    dico.Remove(zob);
                }

                dico.Add(zob, val + 1);
            }
        }

        foreach (var edge in outputTriangles.SelectMany(t => t.GetBorderEdges()))
        {
            var set = new SortedSet<Vector3>(V3Comp.Instance)
            {
                edge.Item1,
                edge.Item2
            };
            var edgeArray = set.SelectMany(v => Array.AsReadOnly([v.X, v.Y, v.Z])).ToArray();

            if (!borderEdgesAsSets.TryAdd(edgeArray, 0))
            {
                borderEdgesAsSets.Remove(edgeArray);
            }
        }

        var manifoldBordersAsSets = dico.Where(pair => pair.Value == 1).Select(pair => pair.Key).ToList();

        // Console.WriteLine("Expected : ");
        // foreach (var set in manifoldBordersAsSets)
        // {
        //     Console.WriteLine(set.Stringify());
        // }
        var eulerCharacteristic = vertexSet.Count - dico.Count + outputTriangles.Count;

        if (eulerCharacteristic != 1)
        {
            Console.WriteLine("WARNING : Euler characteristic is not one of a closed loop (got " + eulerCharacteristic +
                              " instead).");
        }

        // Checks on border edges sets : 
        // On the manifold border set
        AssertIsClosedManifoldBorder(manifoldBordersAsSets);

        // On the rebuilt border edge set
        var borderEdgesFromRebuilt = borderEdgesAsSets.Keys.ToList();

        AssertIsClosedManifoldBorder(borderEdgesFromRebuilt);
        return (dico, borderEdgesFromRebuilt, manifoldBordersAsSets);
    }

    private static void AssertIsClosedManifoldBorder(List<float[]> setOfBorders)
    {
        var borderVerticesEnumerable = setOfBorders.SelectMany(e =>
        {
            var set = new SortedSet<Vector3>(V3Comp.Instance);
            set.Add(new Vector3(e[0], e[1], e[2]));
            set.Add(new Vector3(e[3], e[4], e[5]));
            return set;
        });

        //Check that there is as many vertices as there are edges
        var verticesEnumerable = borderVerticesEnumerable as Vector3[] ?? borderVerticesEnumerable.ToArray();
        var borderVertices = verticesEnumerable.Distinct().ToList();

        var edgeDico = new OrderedDictionary<(int, int), int>(UnorderedValueTupleComparer.Instance);
        setOfBorders.ForEach(e =>
        {
            var v1 = borderVertices.Index()
                .Where(v => v.Item.IsEqualApprox(new Vector3(e[0], e[1], e[2]))) //Filter out
                .Select(kvp => kvp.Index) // Get index
                .ToList(); // Output as list

            if (v1.Count > 1)
            {
                Console.WriteLine(
                    $"Vertex {new Vector3(e[0], e[1], e[2])} is present {v1.Count} times in the vertex set." +
                    $" Expected a single occurence, got {String.Join(",", v1)}");
            }

            if (v1.Count == 0)
            {
                Console.WriteLine(
                    $"Vertex {new Vector3(e[0], e[1], e[2])} from the edge dataset is not in the vertex set.");
            }

            var v2 = borderVertices.Index()
                .Where(v => v.Item.IsEqualApprox(new Vector3(e[3], e[4], e[5]))) //Filter out
                .Select(kvp => kvp.Index) // Get index
                .ToList(); // Output as list

            if (v2.Count > 1)
            {
                Console.WriteLine(
                    $"Vertex {new Vector3(e[3], e[4], e[5])} is present {v1.Count} times in the vertex set." +
                    $" Expected a single occurence, got {String.Join(",", v1)}");
            }

            if (v1.Count == 0)
            {
                Console.WriteLine(
                    $"Vertex {new Vector3(e[3], e[4], e[5])} from the edge dataset is not in the vertex set.");
            }

            if (!edgeDico.TryAdd((v1[0], v2[0]), 1))
            {
                edgeDico[(v1[0], v2[0])]++;
            }
        });

        var count = 0;
        borderVertices.ForEach(v =>
        {
            var vOccurencesInEdges = edgeDico.Where(kvp => kvp.Key.Item1 == count || kvp.Key.Item2 == count)
                .Select(e => edgeDico.IndexOf(e.Key))
                .ToList();

            if (vOccurencesInEdges.Count != 2)
            {
                Console.WriteLine(
                    $"Vertex [{count}]({v}) is used {vOccurencesInEdges.Count} times in the border instead of 2.");
            }

            count++;
        });

        if (borderVertices.Count != setOfBorders.Count)
        {
            // Console.WriteLine("Debug count error");
            // foreach (var edge in edgeDico)
            // {
            // Console.WriteLine(edge.Key+ " : " + edge.Value);
            // }

            throw new Exception(
                "The provided data does not define closed manifold border because" +
                $" the count of border edges is not equal ({setOfBorders.Count})to the count of border vertices ({borderVertices.Count}).");
        }

        //Check that there every vertex is present twice
        var vertexList = verticesEnumerable.ToList();
        foreach (var v in borderVertices)
        {
            var vertexOccurenceCount = vertexList.FindAll(vec => vec.IsEqualApprox(v)).Count;
            if (vertexOccurenceCount != 2)
            {
                throw new Exception(
                    "The provided data does not define closed manifold border because" +
                    " not all vertices have two occurrences ");
            }
        }
    }

    /// <summary>
    /// Asserts that a list of triangles is a proper 2D manifold
    /// </summary>
    /// <param name="res"></param>
    internal static void AssertIsTriangleListManifold(List<HexTerrainCell.TriangleInfo> res)
    {
        var (dico, borderEdgesAsSets, _) = ReprocessEdgeGeometry(res);

        // This check ensures the border is bounded
        foreach (var kvp in dico)
        {
            Assert.That(
                EngineUtils.Mod(kvp.Value, 2) == 0 //inner edges 
                || kvp.Value == 1 && borderEdgesAsSets.Contains(kvp.Key, new FloatArrayComparer(1e-5)) //border edge
                , Is.True);
        }

        // This check ensures the surface is a 2 manifold
        foreach (var kvp in dico)
        {
            Assert.That(
                kvp.Value == 2 //inner edges are used once
                || kvp.Value == 1 && borderEdgesAsSets.Contains(kvp.Key, new FloatArrayComparer(1e-5)) //border edge
                , Is.True);
        }

    }
}