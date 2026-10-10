using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MathNet.Numerics;

// ReSharper disable AccessToModifiedClosure
// ReSharper disable UnusedVariable
namespace UnitTests.test.processing;

public class TestTriangleProcessing
{
    private HexagonalTerrainChunk? _chunk;

    private static int _dimension = 5;

    private float[][] _src1 = new float[_dimension][];
    private float[][] _src2 = new float[_dimension][];

    [TearDown]
    public void TearDown()
    {
        //Reset source & dimension
        _src1 = new float[_dimension][];
        _src2 = new float[_dimension][];
        _dimension = 5;
        _chunk = null;
    }

    /// <summary>
    /// Tests the processing of a chunk that only generates one full cell
    /// </summary>
    /// <param name="geometryMode"></param>
    [Test]
    public void TestProcessingOfSimpleChunk([Values] GeometryMode geometryMode)
    {
        _dimension = 2;
        _src1 = new float[_dimension][];
        _src2 = new float[_dimension][];


        for (int i = 0; i < _src1.Length; i++)
        {
            _src1[i] = new float[_dimension];
            _src2[i] = new float[_dimension];

            for (int j = 0; j < _src1.Length; j++)
            {
                _src1[i][j] = i * _src1.Length + j;
                _src2[i][j] = i * _src1.Length + j;
            }
        }

        _chunk = new HexagonalTerrainChunk(
            Vector2I.Zero,
            _dimension * Vector2I.One,
            v => v is { X: 0, Y: 0 } ? _chunk : null,
            _src1, _src2)
        {
            DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryMode),
            DefaultThreshold =
                new Tuple<float, ThresholdComputationMode>(1f, ThresholdComputationMode.HeightDifference),
            Dirty = true,
            GeometryModeParameters = ((0.4f, 0.4f), (0.4f, 0.4f))
        };

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();

        Assert.That(_chunk.TerrainDualGrid.CompleteCells, Has.Count.EqualTo(1));

        Assert.DoesNotThrow(() => { output = _chunk.ProcessGeometry(); });


        //Per cell manifold checks
        foreach (var keyValuePair in output)
        {
            switch (geometryMode)
            {
                case GeometryMode.SmoothLinear:
                    TestUtils.AssertIsTriangleListManifold(keyValuePair.Value);
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
                    break;
                case GeometryMode.FlatHexagons:
                    TestUtils.AssertIsTriangleListManifold(keyValuePair.Value);
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
                    //Check all heights are identical and equal to the cell avg
                    Assert.That(
                        keyValuePair.Value.All(t => t.Points.All(p => p.Y.Equals(keyValuePair.Key.AverageHeight))),
                        Is.True);
                    break;
                case GeometryMode.FlatHexagonsNoFans:
                    TestUtils.AssertIsTriangleListManifold(keyValuePair.Value);
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
                    Assert.That(
                        keyValuePair.Value.All(t => t.Points.All(p => p.Y.Equals(keyValuePair.Key.AverageHeight))),
                        Is.True);
                    break;
                case GeometryMode.FlatTrianglesNoFans:
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
                    for (int i = 0; i < HexTerrainCell.VertexCount; i++)
                        //Check all heights are identical and equal to the cell edge value
                    {
                        Assert.That(
                            keyValuePair.Value[i].Points.All(p => p.Y.Equals(keyValuePair.Key.GetEdgeAvgHeight!(i))),
                            Is.True);
                    }

                    break;
                case GeometryMode.FlatTriangles:
                    Assert.That(keyValuePair.Value, Has.Count.EqualTo(6 + 12));
                    TestUtils.AssertIsTriangleListManifold(keyValuePair.Value);
                    break;
            }
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        switch (geometryMode)
        {
            case GeometryMode.FlatTrianglesNoFans: // Output is not manifold
                break;
            default:
                TestUtils.AssertIsTriangleListManifold(flattenedOutput);
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
        _dimension = 2;
        _src1 = new float[_dimension][];
        _src2 = new float[_dimension][];
        
        _chunk = new HexagonalTerrainChunk(
            Vector2I.Zero,
            _dimension * Vector2I.One,
            v => v is { X: 0, Y: 0 } ? _chunk : null,
            _src1, _src2)
        {
            DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryMode),
            DefaultThreshold =
                new Tuple<float, ThresholdComputationMode>(1f, ThresholdComputationMode.HeightDifference),
            Dirty = true,
            GeometryModeParameters = ((0.4f, 0.4f), (0.4f, 0.4f))
        };

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();

        Assert.DoesNotThrow(() => { output = _chunk.ProcessGeometry(); });

            int expectedCount;
            switch (geometryMode)
            {
                case GeometryMode.FlatTrianglesNoFans:
                case GeometryMode.FlatHexagons:
                case GeometryMode.FlatTriangles:
                case GeometryMode.FlatHexagonsNoFans:
                case GeometryMode.SmoothLinear:
                    expectedCount = 6;
                    break;
                case GeometryMode.Foothill:
                case GeometryMode.Plateau:
                    expectedCount = 6 + 12;
                    break;
                case GeometryMode.BendingEdge:
                    expectedCount = 6 + 12 + 12;
                    break;
                default: throw new NotSupportedException();
            }
        //Per cell manifold checks
        foreach (var keyValuePair in output)
        {


            Assert.That(keyValuePair.Value, Has.Count.EqualTo(expectedCount));
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        Assert.That(flattenedOutput, Has.Count.EqualTo(output.Count * expectedCount));
    }

    /// <summary>
    /// Tests the processing of a chunk for different geometry modes.
    /// This tests ensure that the processing does not throw, that the output is a 2-manifold and checks triangle count
    /// </summary>
    [Test]
    public void TestProcessingOfChunk([Values] GeometryMode geometryMode)
    {
        for (int i = 0; i < _src1.Length; i++)
        {
            _src1[i] = new float[_dimension];
            _src2[i] = new float[_dimension];

            for (int j = 0; j < _src1.Length; j++)
            {
                _src1[i][j] = i * _src1.Length + j;
                _src2[i][j] = 3 * (i * _src1.Length + j);
            }
        }

        _chunk = new HexagonalTerrainChunk(
            Vector2I.Zero,
            _dimension * Vector2I.One,
            v => v is { X: 0, Y: 0 } ? _chunk : null,
            _src1, _src2)
        {
            DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryMode),
            DefaultThreshold =
                new Tuple<float, ThresholdComputationMode>(0.5f, ThresholdComputationMode.HeightDifference),
            Dirty = true,
            GeometryModeParameters = ((0.4f, 0.4f), (0.4f, 0.4f))
        };

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = _chunk.ProcessGeometry();
        Assert.DoesNotThrow(() => { output = _chunk.ProcessGeometry(); });

        //Per cell manifold and value checks
        foreach (var keyValuePair in output)
        {
            TestUtils.AssertIsTriangleListManifold(keyValuePair.Value);

            var curCell = keyValuePair.Key;
            var triangulation = keyValuePair.Value;
            //Extra checks depending on the selected mode
            switch (geometryMode)
            {
                case GeometryMode.SmoothLinear:
                    Assert.That(triangulation, Has.Count.EqualTo(6));
                    break;
                case GeometryMode.FlatHexagonsNoFans:
                    Assert.That(triangulation, Has.Count.EqualTo(6));
                    //Check cell equality of Y values
                    Assert.That(triangulation.SelectMany(tri => tri.Points)
                        .All(p => p.Y.AlmostEqual(curCell.AverageHeight)));
                    break;
                case GeometryMode.FlatTrianglesNoFans:
                    Assert.That(triangulation, Has.Count.EqualTo(6));
                    //Per triangle equality of Y values
                    foreach (var triangle in triangulation)
                    {
                        Assert.That(triangle.Points.All(v => v.Y.AlmostEqual(triangle.Points[0].Y)));
                    }

                    break;
                case GeometryMode.FlatHexagons:
                    //Check that 6 triangles are flat and equal to the avg in each cell
                    Assert.That(triangulation.Count(tri => tri.Points
                        .All(p => p.Y.AlmostEqual(curCell.AverageHeight))), Is.EqualTo(6));
                    //Count the amount of neighbor cells : Each adds one triangle to the final triangulation 
                    var neighborCellEdgesCount = _chunk.GetHexCells(c => c.IsReady() && c.GetNeighborCellsCoordinates()
                        .Any(cIdx => curCell.CellCoords == cIdx)).Count();

                    Assert.That(triangulation, Has.Count.EqualTo(6 + neighborCellEdgesCount));
                    break;
                case GeometryMode.FlatTriangles:
                    //Check that 6 triangles are flat in each cell
                    Assert.That(triangulation.Count(tri => tri.Points
                        .All(p => p.Y.AlmostEqual(tri.Points[0].Y))), Is.EqualTo(6));
                    break;
            }
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        TestUtils.AssertIsTriangleListManifold(flattenedOutput);
    }

    //TODO : Edge connexity check
}
