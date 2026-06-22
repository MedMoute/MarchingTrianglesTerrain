#pragma warning disable NUnit2021

using Godot;
using Localproto.addons.marchingTriangles;
using Localproto.addons.marchingTriangles.utils;

namespace Localproto.UnitTests;

public class TestGrids
{
    [Test]
    public void TestBasicGridBuild()
    {
        var trianglesOrientationSystem = TerrainSettings.OrientationSystem;
        int dimension = 2;

        var src1 = new float[dimension][];
        var src2 = new float[dimension][];

        var terrainHeightMap = TriangleGrid.BuildFrom(src1, src2, trianglesOrientationSystem);
        var terrainDualGrid = HexagonGrid.BuildFromDual(terrainHeightMap, Vector2I.One * dimension,
            v => v is { X: 0, Y: 0 } ? terrainHeightMap : null,
            v => v is { X: 0, Y: 0 });

        Assert.That(() => terrainDualGrid.PendingCells.Count, Is.EqualTo(9));
        Assert.That(() => terrainDualGrid.PendingCells.Where(c => c.Value != null).ToList().Count, Is.EqualTo(8));
        Assert.That(() => terrainDualGrid.CompleteCells.Count, Is.EqualTo(1));
    }
}