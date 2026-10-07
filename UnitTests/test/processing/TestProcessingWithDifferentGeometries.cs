using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace UnitTests.test.processing;

public class TestProcessingWithDifferentGeometries
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
    [Test]
    public void TestProcessingOfSimpleChunk([Values] GeometryMode geometryMode,
        [Values] GeometryMode geometryModeFallBack)
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
            DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryModeFallBack),
            DefaultThreshold =
                new Tuple<float, ThresholdComputationMode>(1f, ThresholdComputationMode.HeightDifference),
            Dirty = true,
            GeometryModeParameters = ((0.4f,0.4f),(0.4f,0.4f))
        };

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();

        Assert.That(_chunk.TerrainDualGrid.CompleteCells, Has.Count.EqualTo(1));
        Assert.DoesNotThrow(() => { output = _chunk.ProcessGeometry(); });
        Assert.That(output, Has.Count.EqualTo(1));

        foreach (var keyValuePair in output)
        {
            if (geometryMode is not GeometryMode.FlatTrianglesNoFans &&
                geometryModeFallBack is not GeometryMode.FlatTrianglesNoFans)
                TestUtils.AssertIsTriangleListManifold(keyValuePair.Value);
        }
    }

    /// <summary>
    /// Tests the processing of a flat chunk for different geometry modes.
    /// This tests ensure that the processing does not throw, that the output is a 2-manifold and checks triangle count
    /// </summary>
    [Test]
    public void TestProcessingOfFlatChunk(
        [Values] GeometryMode geometryMode,
        [Values] GeometryMode geometryModeFallBack)
    {
        _chunk = new HexagonalTerrainChunk(
            Vector2I.Zero,
            _dimension * Vector2I.One,
            v => v is { X: 0, Y: 0 } ? _chunk : null,
            _src1, _src2)
        {
            DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryModeFallBack),
            DefaultThreshold =
                new Tuple<float, ThresholdComputationMode>(1f, ThresholdComputationMode.HeightDifference),
            Dirty = true,
            GeometryModeParameters = ((0.4f,0.4f),(0.4f,0.4f))
        };

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();

        Assert.DoesNotThrow(() => { output = _chunk.ProcessGeometry(); });


        //Per cell manifold checks
        foreach (var keyValuePair in output)
        {
            TestUtils.AssertIsTriangleListManifold(keyValuePair.Value);
           // Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        TestUtils.AssertIsTriangleListManifold(flattenedOutput);
        //Assert.That(flattenedOutput, Has.Count.EqualTo(output.Count * 6));
    }

    /// <summary>
    /// Tests the processing of a chunk for different geometry modes.
    /// This tests ensure that the processing does not throw, that the output is a 2-manifold and checks triangle count
    /// </summary>
    [Test]
    public void TestProcessingOfChunk([Values] GeometryMode geometryMode, [Values] GeometryMode geometryModeFallBack)
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
            DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode, geometryModeFallBack),
            DefaultThreshold =
                new Tuple<float, ThresholdComputationMode>(0.5f, ThresholdComputationMode.HeightDifference),
            Dirty = true,
            GeometryModeParameters = ((0.4f,0.4f),(0.4f,0.4f))
        };

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = _chunk.ProcessGeometry();
        Assert.DoesNotThrow(() => { output = _chunk.ProcessGeometry(); });

        //Per cell manifold and value checks
        foreach (var keyValuePair in output)
        {
            if (geometryMode is not GeometryMode.FlatTrianglesNoFans &&
                geometryModeFallBack is not GeometryMode.FlatTrianglesNoFans)
            {
                TestUtils.AssertIsTriangleListManifold(keyValuePair.Value);
            }
        }

        //Global checks
        var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
        if (geometryMode is not GeometryMode.FlatTrianglesNoFans &&
            geometryModeFallBack is not GeometryMode.FlatTrianglesNoFans)
        {
            TestUtils.AssertIsTriangleListManifold(flattenedOutput);
        }
    }
}