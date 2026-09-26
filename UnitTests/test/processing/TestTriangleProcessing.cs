using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace UnitTests.processing;

public class TestTriangleProcessing
{
    private HexagonalTerrainChunk? chunk;

    private static int dimension = 5;

    private float[][] src1 = new float[dimension][];
    private float[][] src2 = new float[dimension][];

    [TearDown]
    public void TearDown()
    {
        //Reset source & dimension
        src1 = new float[dimension][];
        src2 = new float[dimension][];
        dimension = 5;
        chunk = null;
    }

    /// <summary>
    /// Tests the processing of a chunk that only generates one full cell
    /// </summary>
    /// <param name="geometryMode"></param>
    [Test]
    public void TestProcessingOfSimpleChunk([Values] GeometryMode geometryMode)
    {
        dimension = 2;
        src1 = new float[dimension][];
        src2 = new float[dimension][];


        for (int i = 0; i < src1.Length; i++)
        {
            src1[i] = new float[dimension];
            src2[i] = new float[dimension];

            for (int j = 0; j < src1.Length; j++)
            {
                src1[i][j] = i * src1.Length + j;
                src2[i][j] = i * src1.Length + j;
            }
        }

        chunk = new HexagonalTerrainChunk(
            Vector2I.Zero,
            dimension * Vector2I.One,
            v => v is { X: 0, Y: 0 } ? chunk : null,
            src1, src2)
        {
            DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryMode),
            DefaultThreshold = new Tuple<float, ThresholdComputationMode>(1f, ThresholdComputationMode.HeightDifference)
        };

        chunk.Dirty = true;

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();

        Assert.That(chunk._terrainDualGrid.CompleteCells, Has.Count.EqualTo(1));

        Assert.DoesNotThrow(() => { output = chunk.ProcessGeometry(); });


        //Per cell manifold checks
        foreach (var keyValuePair in output)
        {
            switch (geometryMode)
            {
                case GeometryMode.SmoothLinear:
                    AssertIsTriangleListManifold(keyValuePair.Value);
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
                    break;
                case GeometryMode.FlatHexagons:
                    AssertIsTriangleListManifold(keyValuePair.Value);
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
                    //Check all heights are identical and equal to the cell avg
                    keyValuePair.Value.All(t => t.Points.All(p => p.Y.Equals(keyValuePair.Key.AverageHeight)));
                    break;
                case GeometryMode.FlatHexagonsNoFans:
                    AssertIsTriangleListManifold(keyValuePair.Value);
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
                    keyValuePair.Value.All(t => t.Points.All(p => p.Y.Equals(keyValuePair.Key.AverageHeight)));
                    break;
                case GeometryMode.FlatTrianglesNoFans:
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
                    for (int i = 0; i < HexTerrainCell.VertexCount; i++)
                        //Check all heights are identical and equal to the cell edge value
                    {
                        Assert.That(
                            keyValuePair.Value[i].Points.All(p => p.Y.Equals(keyValuePair.Key.GetEdgeAvgHeight(i))),
                            Is.True);
                    }

                    break;
                case GeometryMode.FlatTriangles:
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6 + 12));
                    AssertIsTriangleListManifold(keyValuePair.Value);
                    break;
                default:
break;
                
            }
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        switch (geometryMode)
        {
            case GeometryMode.FlatTrianglesNoFans:
                break;
            default:
                AssertIsTriangleListManifold(flattenedOutput);
                break;
        }
    }

    /// <summary>
    /// Tests the processing of a flat chunk for different geometry modes.
    /// This tests ensure that the processing does not throw, that the output is a 2-manifold and checks triangle count
    /// </summary>
    /// <param name="geometryMode"></param>
    [Test]
    public void TestProcessingOfFlatChunk([Values] GeometryMode geometryMode)
    {
        chunk = new HexagonalTerrainChunk(
            Vector2I.Zero,
            dimension * Vector2I.One,
            v => v is { X: 0, Y: 0 } ? chunk : null,
            src1, src2)
        {
            DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryMode),
            DefaultThreshold = new Tuple<float, ThresholdComputationMode>(1f, ThresholdComputationMode.HeightDifference)
        };

        chunk.Dirty = true;

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();

        Assert.DoesNotThrow(() => { output = chunk.ProcessGeometry(); });


        //Per cell manifold checks
        foreach (var keyValuePair in output)
        {
            Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        Assert.That(flattenedOutput, Has.Count.EqualTo(output.Count * 6));
    }


    [Test]
    public void TestProcessingOfChunk([Values] GeometryMode geometryMode)
    {
        //Change src1 and src2 : this case has a constant average  per cell WTFFFFF
        for (int i = 0; i < src1.Length; i++)
        {
            src1[i] = new float[dimension];
            src2[i] = new float[dimension];

            for (int j = 0; j < src1.Length; j++)
            {
                src1[i][j] = i * src1.Length + j;
                src2[i][j] = -(i * src1.Length + j);
            }
        }

        //Change src1 and src2 : this case has a constant average  per cell WTFFFFF
        for (int i = 0; i < src1.Length; i++)
        {
            src1[i] = new float[dimension];
            src2[i] = new float[dimension];

            for (int j = 0; j < src1.Length; j++)
            {
                src1[i][j] = i * src1.Length + j;
                src2[i][j] = 3 * (i * src1.Length + j);
            }
        }

        chunk = new HexagonalTerrainChunk(
            Vector2I.Zero,
            dimension * Vector2I.One,
            v => v is { X: 0, Y: 0 } ? chunk : null,
            src1, src2)
        {
            DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryMode),
            DefaultThreshold =
                new Tuple<float, ThresholdComputationMode>(0.5f, ThresholdComputationMode.HeightDifference)
        };
        chunk.Dirty = true;

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();
        output = chunk.ProcessGeometry();
        Assert.DoesNotThrow(() => { output = chunk.ProcessGeometry(); });


        //Per cell manifold checks
        foreach (var keyValuePair in output)
        {
            AssertIsTriangleListManifold(keyValuePair.Value);

            switch (geometryMode)
            {
                case GeometryMode.SmoothLinear:
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
                    break;
                case GeometryMode.FlatHexagons:
                    // Assert.That(keyValuePair.Value, Has.Count.EqualTo(6 
                    //         //Count the existing neighbor cells
                    //         // + output.Keys
                    //         //     .Count(c => c.GetNeighborCellsCoordinates()
                    //         //         .Any( cIdx => keyValuePair.Key.CellCoords == cIdx))
                    //         ));
                    break;
            }
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        AssertIsTriangleListManifold(flattenedOutput);
    }

    private static (Dictionary<float[], int> dico,
        List<float[]> borderEdgesAsSets,
        List<float[]>manifoldBorderEdgesAsSets) ReprocessEdgeGeometry(List<HexTerrainCell.TriangleInfo> outputTriangles,
            List<HexTerrainCell.TriangleInfo>? baseTriangles = null)
    {
        var dico = new Dictionary<float[], int>(new FloatArrayComparer(1e-5));
        var borderEdgesAsSets = new Dictionary<float[], int>(new FloatArrayComparer(1e-5));
        var manifoldBordersAsSets = new List<float[]>();

        var vertexSet = new SortedSet<Vector3>(new V3Comp());
        var z = 0;
        foreach (var tInfo in outputTriangles)
        {
            foreach (var edge in tInfo.GetEdges())
            {
                z++;
                var set = new SortedSet<Vector3>(new V3Comp());
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
            var set = new SortedSet<Vector3>(new V3Comp())
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

        manifoldBordersAsSets = dico.Where(pair => pair.Value == 1).Select(pair => pair.Key).ToList();

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
            var set = new SortedSet<Vector3>(new V3Comp());
            set.Add(new Vector3(e[0], e[1], e[2]));
            set.Add(new Vector3(e[3], e[4], e[5]));
            return set;
        });

        //Check that there is as many vertices as there are edges
        var borderVertices = borderVerticesEnumerable.Distinct().ToList();

        var edgeDico = new OrderedDictionary<(int, int), int>(UnorderedTupleComparer.Instance);
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
        var vertexList = borderVerticesEnumerable.ToList();
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

    internal static void AssertIsTriangleListManifold(List<HexTerrainCell.TriangleInfo> res)
    {
        var (
            dico,
            borderEdgesAsSets,
            manifoldBorderEdgesAsSets) = ReprocessEdgeGeometry(res);

        // This check ensures the border is bounded
        foreach (var kvp in dico)
        {
            Assert.That(
                EngineUtils.mod(kvp.Value, 2) == 0 //inner edges 
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

public class FloatArrayComparer : IEqualityComparer<float[]>, IComparer<float[]>
{
    internal FloatArrayComparer(double epsilon)
    {
        this.epsilon = epsilon;
    }

    private double epsilon;

    public bool Equals(float[]? x, float[]? y)
    {
        if (x?.Length != y?.Length)
            return false;
        for (int i = 0; i < x?.Length; i++)
        {
            if (Math.Abs(x[i] - y[i]) > epsilon)
            {
                // Console.WriteLine(Math.Abs(x[i] - y[i]));
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(float[] obj)
    {
        var v = 31;
        for (int i = 0; i < obj.Length; i++)
        {
            // Proximity of hashcode so that buckets are similar for similar points 
            v = v * 31 * (int)(100 * (obj[i])) + 7;
        }

        return v;
    }

    public int Compare(float[]? x, float[]? y)
    {
        return x.Equals(y) ? 0 : x.GetHashCode() - y.GetHashCode();
    }
}