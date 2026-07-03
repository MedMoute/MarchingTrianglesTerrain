#pragma warning disable NUnit2021

using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;
using NUnit.Framework;

namespace UnitTests;

public class TestHexCells
{
    [Test]
    public void TestGridVerticesAreDualCellCentroids()
    {
        void TestForProvidedFrame(RegularUniformFrame regularUniformFrame)
        {
            int dimension = 2;

            var src1 = new float[dimension][];
            var src2 = new float[dimension][];

            var terrainHeightMap = TriangleGrid.BuildFrom(src1, src2, regularUniformFrame);
            var terrainDualGrid = HexagonGrid.BuildFromDual(terrainHeightMap, Vector2I.One * dimension,
                v => v is { X: 0, Y: 0 } ? terrainHeightMap : null,
                v=>v is { X: 0, Y: 0 });

            HexTerrainCell cell = terrainDualGrid.CompleteCells.First();

            Assert.That(() => cell.CellCoordsImplicit, Is.EqualTo(Vector2I.Down));
            for (int i = 0; i < HexTerrainCell.VertexCount; i++)
            {
                var dualIdx = cell.DualCellsMapping[i];
                Assert.That(() => cell.VertexPositionsInPlane[i],
                    Is.EqualTo(terrainHeightMap.OrientationSystem.GetCellCentroid(dualIdx))
                        .Using<Vector2D, Vector2D>((v1, v2) => (v1 - v2).Length < 1e-5));
            }
        }


        var trianglesOrientationSystem = TerrainSettings.OrientationSystem;
        //TestForProvidedFrame(trianglesOrientationSystem);

        trianglesOrientationSystem = TerrainSettings.OrientationSystem.OffsetBy(new Vector2D(1d, 1d));
        TestForProvidedFrame(trianglesOrientationSystem);
    }
}