using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MathNet.Spatial.Euclidean;

namespace UnitTests;

public class TestTriangleProcessingWithEdgeBleed
{
        private double initAValue;
    private double initThetaValue;

    [SetUp]
    public void Setup()
    {
        initAValue = HexTerrainCell.a;
        initThetaValue = HexTerrainCell.theta;
    }

    [TearDown]
    public void Teardown()
    {
        HexTerrainCell.a = initAValue;
        HexTerrainCell.theta = initThetaValue;
    }

    [Test]
    // TODO Asserts
    public void TestBasicTriangleProcessing()
    {
        HexTerrainCell.a = (float)0.5;
        HexTerrainCell.theta = 0.1;
        // Test 1 :
        //       * B
        //   A=O *  * C

        Vector3 A = Vector3.Up;
        Vector3 B = Vector3.Back;
        Vector3 C = Vector3.Right;
        Vector3[] triangle = new Vector3[3];
        triangle[0] = A;
        triangle[1] = B;
        triangle[2] = C;

        var res = HexTerrainCell.ProcessTriangle(triangle, 6);

        for (int i = 0; i < res.Count; i++)
        {
            var c = res[i];
            Console.WriteLine("Triangle {0}:  [{1},{2},{3}]", i, c.Points[0], c.Points[1], c.Points[2]);
            foreach (var tuple in c.GetBorderEdges())
            {
                Console.WriteLine("Edge :  [{0},{1}]", tuple.Item1, tuple.Item2);
            }
        }

        Console.WriteLine();
        foreach (var tuple in res.SelectMany(t => t.GetBorderEdges()))
        {
            Console.WriteLine("Edge :  [{0},{1}]", tuple.Item1, tuple.Item2);
        }

        // Test "is manifold" :  
        // Map all the Edges and get their count : should be 2 except for edges in that are in GetBorderEdges,
        // in chich case there should be 2
        TestTriangleProcessingNoEdgeBleed.AssertIsTriangleManifold(res);
    }

    [Test]
    public void TestProcessingOfTwoTriangles()
    {
        //SETUP
        // 
        HexTerrainCell.a = (float)0.5;
        HexTerrainCell.theta = 0.1;
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
            var trianglesWithWallEdges = TestTriangleProcessingNoEdgeBleed.ProcessTriangleGeometryIntoSplitTriangles(cell, i, data);
            //Ensure each of the triangular output is itself manifold
            TestTriangleProcessingNoEdgeBleed.AssertIsTriangleManifold(trianglesWithWallEdges);
            var (
                tmpDico,
                tmpBorderEdgesAsSets,
                tmpManifoldBorderEdgesAsSets) = TestTriangleProcessingNoEdgeBleed.ReprocessEdgeGeometry(trianglesWithWallEdges);

            internalEdgesCount[i] = countInternalEdges.Invoke(tmpDico).Count;

            outTriangleData.Add(i, trianglesWithWallEdges);

            var tri = TestTriangleProcessingNoEdgeBleed.GetCellTriangle(cell, i, data);
            baseTriangleInfos[i] = new HexTerrainCell.TriangleInfo
            {
                Points = tri
            };
        }

        var (
            baseDico,
            baseBorderEdgesAsSets,
            baseManifoldBorderEdgesAsSets) = TestTriangleProcessingNoEdgeBleed.ReprocessEdgeGeometry(baseTriangleInfos.ToList());

        // We now have all the data we need from outTriangleData
        // Test whether the output geometry can be manifold :
        // The cell was initially made of 6 triangles facing inward, meaning there was only 6 border edges.
        // Each of the border edges has been split in 2 or 3, meaning we should expect 8 to 12border edges.

        List<HexTerrainCell.TriangleInfo> flattenedData = outTriangleData.SelectMany(v => v.Value).ToList();
        var (dico, 
            borderEdgesAsSets,
            manifoldBorderEdgesAsSets) = TestTriangleProcessingNoEdgeBleed.ReprocessEdgeGeometry(flattenedData);
        // Map the borderEdges Index to their "dico" index

        var internalEdgeCountOfTrianglePair = countInternalEdges.Invoke(dico).Count;
        // If there are no differences between the count of internal edge of the set and the sum of each triangles'
        //  count of internal edges, it means the was no seam. 
        // First we ensure a seam was expected to happen
        if (countInternalEdges(baseDico).Count == 1)
        {
            // // Disable to run diagnostics
            // Assert.That(internalEdgeCountOfTrianglePair, Is.GreaterThan(internalEdgesCount.Sum()));
        }
        else
        {
            //We fail here because something is wrong if we end up here
            Assert.Fail("There was supposed to be a single seam between the base triangles");
        }

        var seamEdge = countInternalEdges(baseDico).First();
        Vector3 seamEdgeStart = new Vector3(seamEdge[0], seamEdge[1], seamEdge[2]);
        Vector3 seamEdgeEnd = new Vector3(seamEdge[3], seamEdge[4], seamEdge[5]);

        Console.WriteLine();
        Console.WriteLine("Base triangles' seaming Edge : ");
        Console.WriteLine("[" + seamEdgeStart + "," + seamEdgeEnd + "]");


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
            Console.WriteLine(edges[i]);
            Console.WriteLine(edges[closestNonEqual[i]]);
            Console.WriteLine();
        }

        Assert.That(manifoldBorderEdgesAsSets.Count, Is.GreaterThanOrEqualTo(8).And.LessThanOrEqualTo(12));
    }

    [Test]
    public void TestProcessingOfSingleCell()
    {
        HexTerrainCell.a = (float)0.5;
        HexTerrainCell.theta = 0.1;
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
        var data = cell.ExtractDataFromCell();


        var outTriangleData = new Dictionary<int, List<HexTerrainCell.TriangleInfo>>();
        // Extracted methods for HexTerrainCell's DoMarchingTriangles()
        for (var i = 0; i < 6; i++)
        {
            var center = cell.CenterPosition;
            var trianglesWithWallEdges = TestTriangleProcessingNoEdgeBleed.ProcessTriangleGeometryIntoSplitTriangles(cell, i, data);
            //Ensure each triangle's output is manifold
            TestTriangleProcessingNoEdgeBleed.AssertIsTriangleManifold(trianglesWithWallEdges);

            outTriangleData.Add(i, trianglesWithWallEdges);
        }

        // We now have all the data we need from outTriangleData
        // Test whether the output geometry can be manifold :
        // The cell was initially made of 6 triangles facing inward, meaning there was only 6 border edges.
        // Each of the border edges has been split in 2 or 3, meaning we should expect 12 to 18 border edges.

        List<HexTerrainCell.TriangleInfo> flattenedData = outTriangleData.SelectMany(v => v.Value).ToList();
        var (dico, borderEdgesAsSets, manifoldBorderEdgesAsSets) = TestTriangleProcessingNoEdgeBleed.ReprocessEdgeGeometry(flattenedData);

        Assert.That(manifoldBorderEdgesAsSets.Count, Is.GreaterThanOrEqualTo(12).And.LessThanOrEqualTo(18));
    }
}