using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;

namespace UnitTests.processing;

public class TestTriangleProcessing {
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
             src1, src2)
         {
             DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(geometryMode,geometryMode),
             DefaultThreshold = new Tuple<float, ThresholdComputationMode>(1f, ThresholdComputationMode.HeightDifference)
         };

         chunk.Dirty = true;

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();

         Assert.DoesNotThrow(() =>
         {
             output = chunk.ProcessGeometry();
         });


         //Per cell manifold checks
         foreach (var keyValuePair in output)
         {
             Assert.That(keyValuePair.Value, Has.Count.EqualTo(6));
         }

         //Global checks
         var flattenedOutput = output.SelectMany(kvp => kvp.Value).ToList();
         Assert.That(flattenedOutput, Has.Count.EqualTo(output.Count * 6));
     }

}