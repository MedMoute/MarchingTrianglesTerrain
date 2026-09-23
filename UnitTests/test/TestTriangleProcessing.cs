using System.Diagnostics;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;

namespace UnitTests;

public class TestTriangleProcessing
{
    private double initAValue;
    private double initThetaValue;

    [SetUp]
    public void Setup()
    {
        initAValue = HexTerrainCell.A;
        initThetaValue = HexTerrainCell.Theta;
    }

    [TearDown]
    public void Teardown()
    {
        HexTerrainCell.A = initAValue;
        HexTerrainCell.Theta = initThetaValue;
    }

    [Test]
    public void TestBasicTriangleProcessing(
        [Values(
            GeometryMode.FlatHexagons,
            GeometryMode.FlatTriangles,
            GeometryMode.SmoothLinear,
            GeometryMode.Foothill,
            GeometryMode.Plateau,
            GeometryMode.BendingEdge)]
        GeometryMode geometryMode,
        [Values(
            GeometryMode.FlatHexagons,
            GeometryMode.FlatTriangles,
            GeometryMode.SmoothLinear, GeometryMode.Foothill, GeometryMode.Plateau, GeometryMode.BendingEdge)]
        GeometryMode geometryModeFallback,
        [Values(0, 1, 2, 3, 4, 5, 6, 7)] int mask)

    {
        //SETUP
        // TODO move in [Setup]
        HexTerrainCell.A = (float)0.5;
        HexTerrainCell.Theta = 0;
        var frame = new HexTileOrientationSystem(new Vector2D(0, 0), new Vector2D(1, 1));
        var dualFrame = frame.GetDual();

        int dimension = 2;
        var src1 = new float[dimension][];
        src1[0] = [0f, 1f];
        src1[1] = [2f, 3f];
        var src2 = new float[dimension][];
        src2[0] = [4f, 5f];
        src2[1] = [6f, 7f];

        var terrainHeightMap = TriangleGrid.BuildFrom(src1, src2, dualFrame);
        var terrainDualGrid = HexagonGrid.BuildFromDual(terrainHeightMap, Vector2I.One * dimension,
            v => v is { X: 0, Y: 0 } ? terrainHeightMap : null,
            v => v is { X: 0, Y: 0 });

        HexTerrainCell cell = terrainDualGrid.CompleteCells.First();
        cell.GeometryModesOverride = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryModeFallback);
        cell.Verbose = false;
        cell.RunIntegrityChecks = true;

        var data = cell.ExtractDataFromCell();

        // SETUP END
        // --------------------

        // We ONLY PROCESS 1 TRIANGLES out of the 6
        var trianglesWithWallEdges = ProcessTriangleGeometryIntoSplitTriangles(cell, 0, data, mask);
        //Ensure the triangulation output is itself manifold
        AssertIsTriangleListManifold(trianglesWithWallEdges);
    }

    [Test]
    public void TestProcessingOfTwoTriangles([Values(
            GeometryMode.FlatHexagons,
            GeometryMode.FlatTriangles,
            GeometryMode.SmoothLinear,
            GeometryMode.Foothill,
            GeometryMode.Plateau,
            GeometryMode.BendingEdge)]
        GeometryMode geometryMode,
        [Values(
            GeometryMode.FlatHexagons,
            GeometryMode.FlatTriangles,
            GeometryMode.SmoothLinear,
            GeometryMode.Foothill,
            GeometryMode.Plateau,
            GeometryMode.BendingEdge)]
        GeometryMode geometryModeFallback,
        //Mask values are 0,1,2,7 because we always have equal side mask values inside a cell
        [Values(0,1,2,3,4,5,6,7)] int mask)
    {
        //SETUP
        // 
        HexTerrainCell.A = (float)0.5;
        HexTerrainCell.Theta = 0;
        var frame = new HexTileOrientationSystem(new Vector2D(0, 0), new Vector2D(1, 1));
        var dualFrame = frame.GetDual();

        int dimension = 2;
        var src1 = new float[dimension][];
        src1[0] = [0f, 1f];
        src1[1] = [2f, 3f];
        var src2 = new float[dimension][];
        src2[0] = [4f, 5f];
        src2[1] = [6f, 7f];

        var terrainHeightMap = TriangleGrid.BuildFrom(src1, src2, dualFrame);
        var terrainDualGrid = HexagonGrid.BuildFromDual(terrainHeightMap, Vector2I.One * dimension,
            v => v is { X: 0, Y: 0 } ? terrainHeightMap : null,
            v => v is { X: 0, Y: 0 });

        HexTerrainCell cell = terrainDualGrid.CompleteCells.First();
        cell.GeometryModesOverride = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryModeFallback);
        cell.Verbose = false;
        cell.RunIntegrityChecks = true;

        var data = cell.ExtractDataFromCell();

        Func<Dictionary<float[], int>, HashSet<float[]>> countInternalEdges =
            d => d.Where(kvp => kvp.Value == 2).Select(kvp => kvp.Key).ToHashSet();

        // SETUP END
        // --------------------


        var outTriangleData = new Dictionary<int, List<HexTerrainCell.TriangleInfo>>();
        // Extracted methods for HexTerrainCell's DoMarchingTriangles()
        var internalEdgesCount = new int[2];
        var baseTriangleInfos = new HexTerrainCell.TriangleInfo[2];
        // We ONLY PROCESS 2 TRIANGLES out of the 6

        for (var i = 0; i < 2; i++)
        {
            var trianglesWithWallEdges = ProcessTriangleGeometryIntoSplitTriangles(cell, i, data, mask);
            //Ensure each of the triangular output is itself manifold
            AssertIsTriangleListManifold(trianglesWithWallEdges);
            var (
                tmpDico,
                tmpBorderEdgesAsSets,
                tmpManifoldBorderEdgesAsSets) = ReprocessEdgeGeometry(trianglesWithWallEdges);

            internalEdgesCount[i] = countInternalEdges.Invoke(tmpDico).Count;

            outTriangleData.Add(i, trianglesWithWallEdges);

            var tri = GetCellTriangle(cell, i, data);
            baseTriangleInfos[i] = new HexTerrainCell.TriangleInfo
            {
                Points = tri
            };
        }

        var (
            baseDico,
            baseBorderEdgesAsSets,
            baseManifoldBorderEdgesAsSets) = ReprocessEdgeGeometry(baseTriangleInfos.ToList());

        // We now have all the data we need from outTriangleData
        // Test whether the output geometry can be manifold :
        // The cell was initially made of 6 triangles facing inward, meaning there was only 6 border edges.
        // Each of the border edges has been split in 2 or 3, meaning we should expect 8 to 12border edges.

        List<HexTerrainCell.TriangleInfo> flattenedData = outTriangleData.SelectMany(v => v.Value).ToList();
        var (dico, borderEdgesAsSets, manifoldBorderEdgesAsSets) = ReprocessEdgeGeometry(flattenedData,baseTriangleInfos.ToList());
        // Map the borderEdges Index to their "dico" index

        var internalEdgeCountOfTrianglePair = countInternalEdges.Invoke(dico).Count;
        // If there are no differences between the count of internal edge of the set and the sum of each triangles'
        //  count of internal edges, it means the was no seam. 
        // First we ensure a seam was expected to happen
        if (countInternalEdges(baseDico).Count == 1)
        {
            // Disable to run diagnostics
            Assert.That(internalEdgeCountOfTrianglePair, Is.GreaterThan(internalEdgesCount.Sum()));
        }
        else
        {
            //We fail here because something is wrong if we end up here
            Assert.Fail("There was supposed to be a single seam between the base triangles");
        }

        var seamEdge = countInternalEdges(baseDico).First();
        Vector3 seamEdgeStart = new Vector3(seamEdge[0], seamEdge[1], seamEdge[2]);
        Vector3 seamEdgeEnd = new Vector3(seamEdge[3], seamEdge[4], seamEdge[5]);

        // Console.WriteLine();
        // Console.WriteLine("Base triangles' seaming Edge : ");
        // Console.WriteLine("[" + seamEdgeStart + "," + seamEdgeEnd + "]");


        //Bruteforce find the closest border edge by sampling an edge and estimating the
        //Hausdorff distance between the samples and the other edges' samples
        List<Vector3[]> sampledEdges = new List<Vector3[]>();
        List<Tuple<Vector3, Vector3>> edges = new List<Tuple<Vector3, Vector3>>();
        int sampleSize = 20;
        // do the sampling :
        foreach (var edge in manifoldBorderEdgesAsSets)
        {
            Vector3 edgeStart = new Vector3(edge[0], edge[1], edge[2]);
            Vector3 edgeEnd = new Vector3(edge[3], edge[4], edge[5]);
            Vector3[] samplePoints = new Vector3[sampleSize];
            for (int i = 0; i < sampleSize; i++)
            {
                samplePoints[i] = edgeStart.Lerp(edgeEnd, (float)i / sampleSize);
            }

            edges.Add(new Tuple<Vector3, Vector3>(edgeStart, edgeEnd));
            sampledEdges.Add(samplePoints);
        }

        float[,] distances = new float[sampledEdges.Count, sampledEdges.Count];
        // Compute the Haussdorf estimation matrix
        for (int i = 0; i < sampledEdges.Count; i++)
        {
            var edge = manifoldBorderEdgesAsSets[i];
            Vector3 edgeStart = new Vector3(edge[0], edge[1], edge[2]);
            Vector3 edgeEnd = new Vector3(edge[3], edge[4], edge[5]);
            var iSize = edgeStart.DistanceTo(edgeEnd);

            for (int j = 0; j < sampledEdges.Count; j++)
            {
                var edgeJ = manifoldBorderEdgesAsSets[i];
                Vector3 edgeJStart = new Vector3(edgeJ[0], edgeJ[1], edgeJ[2]);
                Vector3 edgeJEnd = new Vector3(edgeJ[3], edgeJ[4], edgeJ[5]);
                var jSize = edgeJStart.DistanceTo(edgeJEnd);
                float[] distMins = new float[sampleSize];
                Vector3 p1, p2;
                for (int k = 0; k < sampleSize; k++)
                {
                    float value = float.MaxValue;

                    p1 = sampledEdges[i][k];
                    for (int l = 0; l < sampleSize; l++)
                    {
                        p2 = sampledEdges[j][l];
                        var d = p1.DistanceSquaredTo(p2);
                        if (d < value)
                        {
                            value = d;
                        }
                    }

                    distMins[k] = value;
                }

                distances[i, j] = distMins.Max() / (jSize * iSize);
            }
        }

        int[] closestNonEqual = new int[sampledEdges.Count];
        float[] estHaussdorf = new float[sampledEdges.Count];

        for (int i = 0; i < sampledEdges.Count; i++)
        {
            float val = float.MaxValue;
            int idx = -1;
            for (int j = 0; j < sampledEdges.Count; j++)
            {
                if (i != j && distances[i, j] < val)
                {
                    val = distances[i, j];
                    idx = j;
                }
            }

            closestNonEqual[i] = idx;
            estHaussdorf[i] = val;
        }

        for (int i = 0; i < sampledEdges.Count; i++)
        {
            Console.WriteLine("Closest to [{0}] is [{1}] (d={2})",
                i,
                closestNonEqual[i],
                estHaussdorf[i]);
            // Console.WriteLine(edges[i]);
            // Console.WriteLine(edges[closestNonEqual[i]]);
            // Console.WriteLine();
        }

    }

    [Test]
    public void TestProcessingOfSingleCell([Values(
            GeometryMode.FlatHexagons,
            GeometryMode.FlatTriangles,
            GeometryMode.SmoothLinear,
            GeometryMode.Foothill,
            GeometryMode.Plateau,
            GeometryMode.BendingEdge)]
        GeometryMode geometryMode,
        [Values(
            GeometryMode.FlatHexagons,
            GeometryMode.FlatTriangles,
            GeometryMode.SmoothLinear,
            GeometryMode.Foothill,
            GeometryMode.Plateau,
            GeometryMode.BendingEdge)]
        GeometryMode geometryModeFallback,
        //Mask values are 0,2,5,7 because we always have equal side mask values inside a cell
        [Values(0, 2,5,7)] int mask)
    {
        HexTerrainCell.A = (float)0.5;
        HexTerrainCell.Theta = 0;
        var frame = new HexTileOrientationSystem(new Vector2D(0, 0), new Vector2D(1, 1));
        var dualFrame = frame.GetDual();

        int dimension = 2;
        var src1 = new float[dimension][];
        src1[0] = [0f, 1f];
        src1[1] = [2f, 3f];
        var src2 = new float[dimension][];
        src2[0] = [4f, 5f];
        src2[1] = [6f, 7f];

        var terrainHeightMap = TriangleGrid.BuildFrom(src1, src2, dualFrame);
        var terrainDualGrid = HexagonGrid.BuildFromDual(terrainHeightMap, Vector2I.One * dimension,
            v => v is { X: 0, Y: 0 } ? terrainHeightMap : null,
            v => v is { X: 0, Y: 0 });

        HexTerrainCell cell = terrainDualGrid.CompleteCells.First();

        // TODO : PARAMETER
        cell.GeometryModesOverride = new Tuple<GeometryMode, GeometryMode>(
            geometryMode, geometryModeFallback);
        var data = cell.ExtractDataFromCell();


        var outTriangleData = new Dictionary<int, List<HexTerrainCell.TriangleInfo>>();
        // Extracted methods for HexTerrainCell's DoMarchingTriangles()
        for (var i = 0; i < 6; i++)
        {
            var center = cell.CenterPosition;
            var trianglesWithWallEdges = ProcessTriangleGeometryIntoSplitTriangles(cell, i, data);
            //Ensure each triangle's output is manifold
            AssertIsTriangleListManifold(trianglesWithWallEdges);

            outTriangleData.Add(i, trianglesWithWallEdges);
        }

        // We now have all the data we need from outTriangleData
        // Test whether the output geometry can be manifold :
        // The cell was initially made of 6 triangles facing inward, meaning there was only 6 border edges.
        // Each of the border edges has been split in 2 or 3, meaning we should expect 12 to 18 border edges.

        List<HexTerrainCell.TriangleInfo> flattenedData = outTriangleData.SelectMany(v => v.Value).ToList();
        var (dico, borderEdgesAsSets, manifoldBorderEdgesAsSets) = ReprocessEdgeGeometry(flattenedData);

        Assert.That(manifoldBorderEdgesAsSets.Count, Is.GreaterThanOrEqualTo(12).And.LessThanOrEqualTo(18));
    }


    internal static List<HexTerrainCell.TriangleInfo> ProcessTriangleGeometryIntoSplitTriangles(
        HexTerrainCell cell,
        int i,
        Dictionary<Vector2D, float> data,
        int? mask = null)
    {
        var tri = GetCellTriangle(cell, i, data);

        if (mask == null)
        {
            var A = tri[0];
            var B = tri[1];
            var C = tri[2];
            mask = (Math.Abs(A.Y - C.Y) > 0.5 ? 1 : 0) * 4 +
                   (Math.Abs(B.Y - C.Y) > 0.5 ? 1 : 0) * 2 +
                   (Math.Abs(A.Y - B.Y) > 0.5 ? 1 : 0) * 1;
        }

        Dictionary<string, object> hints = new()
        {
            [HexTerrainCell.AverageHeightHint] = cell.AverageHeight,
            [HexTerrainCell.NextTriangleEdgeAvgHeight] = cell.GetEdgeAvgHeight!(EngineUtils.mod(i + 1, HexTerrainCell.VertexCount)),
            [HexTerrainCell.NextTriangleVertexPos] = new Vector3(
                (float)cell.VertexPositionsInPlane[EngineUtils.mod(i + 1, HexTerrainCell.VertexCount)].X,
                cell.GetVertexData!(EngineUtils.mod(i + 1, HexTerrainCell.VertexCount)),
                (float)cell.VertexPositionsInPlane[EngineUtils.mod(i + 1,HexTerrainCell. VertexCount)].Y)

        };
        List<HexTerrainCell.TriangleInfo> trianglesWithWallEdges =
            HexTerrainCell.ProcessTriangle(
                cell,
                tri,
                HexTerrainCell.ComputeEdgeGeometryMode(mask.Value, cell.GeometryModesOverride), hints);
        return trianglesWithWallEdges;
    }

    internal static Vector3[] GetCellTriangle(HexTerrainCell cell, int i, Dictionary<Vector2D, float> data)
    {
        Vector2D center = cell.CenterPosition;
        var posB = cell.VertexPositionsInPlane[i];
        int index = (i + 1) % 6;
        var posC = cell.VertexPositionsInPlane[index];

        Vector3[] tri =
        [
            new((float)center.X, cell.AverageHeight, (float)center.Y),
            new((float)posB.X, data[posB], (float)posB.Y),
            new((float)posC.X, data[posC], (float)posC.Y)
        ];
        return tri;
    }


    internal static (Dictionary<float[], int> dico,
        List<float[]> borderEdgesAsSets,
        List<float[]>manifoldBorderEdgesAsSets) ReprocessEdgeGeometry(List<HexTerrainCell.TriangleInfo> outputTriangles,List<HexTerrainCell.TriangleInfo>? baseTriangles = null)
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
        
        if (eulerCharacteristic!=1)
        {
            Console.WriteLine("WARNING : Euler characteristic is not one of a closed loop (got "+eulerCharacteristic+ " instead).");
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
        
       var edgeDico = new OrderedDictionary<(int,int),int>(UnorderedTupleComparer.Instance);
       setOfBorders.ForEach(e =>
       {
           var v1= borderVertices.Index()
               .Where(v => v.Item.IsEqualApprox(new Vector3(e[0], e[1], e[2]))) //Filter out
               .Select(kvp=> kvp.Index) // Get index
               .ToList(); // Output as list
           
           if (v1.Count > 1)
           {
               Console.WriteLine($"Vertex {new Vector3(e[0], e[1], e[2])} is present {v1.Count} times in the vertex set." +
                                 $" Expected a single occurence, got {String.Join(",",v1)}");
           }

           if (v1.Count == 0)
           {
               Console.WriteLine($"Vertex {new Vector3(e[0], e[1], e[2])} from the edge dataset is not in the vertex set.");
           }
           
           var v2= borderVertices.Index()
               .Where(v => v.Item.IsEqualApprox(new Vector3(e[3], e[4], e[5]))) //Filter out
               .Select(kvp=> kvp.Index) // Get index
               .ToList(); // Output as list
                      
           if (v2.Count > 1)
           {
               Console.WriteLine($"Vertex {new Vector3(e[3], e[4], e[5])} is present {v1.Count} times in the vertex set." +
                                 $" Expected a single occurence, got {String.Join(",",v1)}");
           }

           if (v1.Count == 0)
           {
               Console.WriteLine($"Vertex {new Vector3(e[3], e[4], e[5])} from the edge dataset is not in the vertex set.");
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
                Console.WriteLine($"Vertex [{count}]({v}) is used {vOccurencesInEdges.Count} times in the border instead of 2.");
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
                EngineUtils.mod(kvp.Value,2)==0 //inner edges 
                || kvp.Value == 1 && borderEdgesAsSets.Contains(kvp.Key, new FloatArrayComparer(1e-5)) //border edge
                , Is.True);
        }
        // This check ensures the surface is a 2 manifold
        foreach (var kvp in dico)
        {
            Assert.That(
                kvp.Value==2 //inner edges are used once
                || kvp.Value == 1 && borderEdgesAsSets.Contains(kvp.Key, new FloatArrayComparer(1e-5)) //border edge
                , Is.True);
        }
    }
}

public class FloatArrayComparer : IEqualityComparer<float[]>
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
}