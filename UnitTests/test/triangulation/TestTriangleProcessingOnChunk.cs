using System.Collections;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;

namespace UnitTests.triangulation;

public class TestTriangleProcessingOnChunk
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
    /// Tests the processing of a flat chunk for different geometry modes.
    /// This tests ensure that the processing does not throw, that the output is a 2-manifold and checks triangle count
    /// </summary>
    /// <param name="geometryMode"></param>
    [Test]
    public void TestProcessingOfFlatChunk(
        [Values(
            GeometryMode.FlatHexagons,
            GeometryMode.FlatTriangles,
            GeometryMode.SmoothLinear,
            GeometryMode.Foothill,
            GeometryMode.Plateau,
            GeometryMode.BendingEdge)]
        GeometryMode geometryMode)
    {
        chunk = new HexagonalTerrainChunk(
            Vector2I.Zero,
            dimension * Vector2I.One,
            v => v is { X: 0, Y: 0 } ? chunk : null,
            src1, src2);

        Dictionary<Vector2I, List<HexTerrainCell.TriangleInfo>> output = new();

        Assert.DoesNotThrow(() =>
        {
            foreach (var hexagonCell in chunk._terrainDualGrid.CompleteCells)
            {
                hexagonCell.GeometryModesOverride =
                    new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryMode);
                var data = hexagonCell.ExtractDataFromCell();
                var cellAsTris = hexagonCell.ProcessCellGeometry(data, chunk);
                output.Add(hexagonCell.CellCoordsImplicit, cellAsTris);
            }
        });


        //Per cell manifold checks
        foreach (var keyValuePair in output)
        {
            TestTriangleProcessing.AssertIsTriangleListManifold(keyValuePair.Value);
            Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        TestTriangleProcessing.AssertIsTriangleListManifold(flattenedOutput);
        Assert.That(flattenedOutput, Has.Count.EqualTo(output.Count * 6));
    }

    /// <summary>
    /// Tests the processing of the triangles of a non-flat chunk for different geometry modes.
    /// This tests ensure that the processing does not throw, that the output is a 2-manifold and checks triangle count
    /// </summary>
    /// <param name="geometryMode"></param>
    [Test]
    public void TestProcessingOfChunkWithNoValueUnderThreshold(
        [Values(
            GeometryMode.FlatHexagons,
            GeometryMode.FlatTriangles,
            GeometryMode.SmoothLinear,
            GeometryMode.Foothill,
            GeometryMode.Plateau,
            GeometryMode.BendingEdge)]
        GeometryMode geometryMode)
    {
        //Change src1 and src2
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

        chunk = new HexagonalTerrainChunk(
            Vector2I.Zero,
            dimension * Vector2I.One,
            v => v is { X: 0, Y: 0 } ? chunk : null,
            src1, src2);

        Dictionary<Vector2I, List<HexTerrainCell.TriangleInfo>> output = new();

        Assert.DoesNotThrow(() =>
        {
            foreach (var hexagonCell in chunk._terrainDualGrid.CompleteCells)
            {
                if (hexagonCell.CellCoordsImplicit == new Vector2I(0, 1) && geometryMode == GeometryMode.FlatTriangles)
                {
                    Console.WriteLine("AAA");
                }
                hexagonCell.GeometryModesOverride =
                    new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryMode);
                var data = hexagonCell.ExtractDataFromCell();
                var cellAsTris = hexagonCell.ProcessCellGeometry(data, chunk);
                output.Add(hexagonCell.CellCoordsImplicit, cellAsTris);
            }
        });


        //Per cell manifold checks
        foreach (var keyValuePair in output)
        {
            Assert.That(keyValuePair.Value, Has.Count.EqualTo(GetTriangleCountInCell(keyValuePair.Key,geometryMode,(src1,src2))));
            TestTriangleProcessing.AssertIsTriangleListManifold(keyValuePair.Value);
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        TestTriangleProcessing.AssertIsTriangleListManifold(flattenedOutput);
    }

    private int GetTriangleCountInCell(Vector2I cellidx, GeometryMode geometryMode,(float[][]src1,float[][]src2) src)
    {
        switch (geometryMode)
        {
            case GeometryMode.FlatHexagons:
            {
                //2 cases  :
                //both points of the tri over the avg
                //or
                //one over one under => one additional triangle due to the split of the base triangle
                // Extra case to handle : vertex height at the avg heigh =>  -2 triangles per
                HexTerrainCell cell = chunk.GetHexCells().Where(c => c.CellCoordsImplicit == cellidx).First();
                var data = cell.ExtractDataFromCell();
                var heightList = data.Values.ToList();
                int notSplitTriangles = 0; 
                int vertexHeightIsTheAvg = 0; 

                for (int i = 0; i < heightList.Count; i++)
                {
                    if ((heightList[i]>=cell.AverageHeight && heightList[EngineUtils.mod(i+1,HexTerrainCell.VertexCount)]>=cell.AverageHeight) ||
                        (heightList[i]<=cell.AverageHeight && heightList[EngineUtils.mod(i+1,HexTerrainCell.VertexCount)]<=cell.AverageHeight))
                    {
                        notSplitTriangles++;
                    }

                    if (Math.Abs(heightList[i] - cell.AverageHeight) < 1e-5)
                    {
                        vertexHeightIsTheAvg++;
                    }
                }
                return 6 * (1 + 2) + (6-notSplitTriangles) - 2*vertexHeightIsTheAvg ; //Flat triangle, + 2 per outer edge + the split triangles + different cases
            }
            case GeometryMode.FlatTriangles:
                return 6 * (2 + 2 + 2 + 1); // 2 for Flat triangle split in two on the outer edge,
                                            // 2 for outer edge fans
                                            // 2 for next triangle edge fan
                                            // 1 for conformity of the edge fan on the new triangle
            default:
                return 6;
        }
    }
}