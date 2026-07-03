#pragma warning disable NUnit2021
using System;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;
using MathNet.Spatial.Units;
using NUnit.Framework;

namespace UnitTests;

public class TestHexesFromDual
{
    [Test]
    public void TestDual()
    {
        int dimension = 2;
        var src1 = new float[dimension][];
        var src2 = new float[dimension][];
        var doubleTriangles = TriangleGrid.BuildFrom(src1, src2, TerrainSettings.OrientationSystem);
        var hexagons = HexagonGrid.BuildFromDual(doubleTriangles,Vector2I.One*dimension,
            v=>v is { X: 0, Y: 0 } ? doubleTriangles :null,
            v=>v is { X: 0, Y: 0 });


        var points = hexagons.Frame.GetVertexPositions(Vector2I.Zero).Select(v => v.Item1).ToList();
        var center = hexagons.Frame.GetCellCentroid(Vector2I.Zero, 0);

        for (int i = 0; i < 6; i++)
        {
            var v1 = points[i] - center;
            var v2 = points[(i + 1) % 6] - center;
            var v = points[(i + 1) % 6] - points[i];
            //Segment to center length check
            Assert.That(() => v1.Length, Is.EqualTo(hexagons.Frame.TilingScale).Within(1E-5));
            //Polygon segments length check
            Assert.That(() => v.Length, Is.EqualTo(hexagons.Frame.TilingScale).Within(1E-5));
            //Relative angles
            Assert.That(() => v1.SignedAngleTo(v2).Radians, Is.EqualTo(Angle.FromDegrees(60).Radians).Within(1E-5));
        }
    }

    [Test]
    public void TestDualTileAreaIsSameAsSelf()
    {
        RegularUniformFrame frame = new HexTileOrientationSystem(new Vector2D(0, 0), new Vector2D(1, 1));
        Assert.That(() => frame.GetDual().GetTileArea(),
            Is.EqualTo(frame.GetTileArea()).Within(1E-5));

        frame = new DoubleDeltaTileOrientationSystem(new Vector2D(0, 0), new Vector2D(1, 1));
        Assert.That(() => frame.GetDual().GetTileArea(),
            Is.EqualTo(frame.GetTileArea()).Within(1E-5));
    }

    [Test]
    public void TestDualOfDualIsSelfForHex()
    {
        RegularUniformFrame frame = new HexTileOrientationSystem(new Vector2D(0, 0), new Vector2D(1, 1));
        
        var hexFrame = frame.GetDual();

        var cell = new HexTerrainCell(Vector2I.Zero, hexFrame, frame);

        Assert.That(() => frame.GetDual().GetDual().Transform.ToArray(),
            Is.EqualTo(frame.Transform.ToArray()).AsCollection.Within(1E-5));
        
        // Do the same for offset frame : 
        
        RegularUniformFrame frame2 = new HexTileOrientationSystem(new Vector2D(10, 10), new Vector2D(11, 11));
        Assert.That(() => frame2.GetDual().GetDual().Transform.ToArray(),
            Is.EqualTo(frame2.Transform.ToArray()).AsCollection.Within(1E-5));
    }

    [Test]
    public void TestDualOfDualIsSelfForTris()
    {
        // Test with usual system
        RegularUniformFrame frame = TerrainSettings.OrientationSystem;
        
         Assert.That(() => frame.GetDual().GetDual().Transform.ToArray(),
             Is.EqualTo(frame.Transform.ToArray()).AsCollection.Within(1E-5));

        //Test with randomly seeded grid
        frame = new DoubleDeltaTileOrientationSystem(new Vector2D(1, -1), new Vector2D(4, 3));


        var f = TerrainToolPluginHelper.FormatVector2;
        Console.WriteLine("Center base               : "
                          + f(frame.GetCellCentroid(Vector2I.Zero, 0)) +
                          f(frame.GetCellCentroid(Vector2I.Zero, 1)));
        var hexFrame = frame.GetDual();
        Console.WriteLine("Angle dual " + ((HexTileOrientationSystem)hexFrame).TilingAngle.Degrees);
        Console.WriteLine("Center dual               : "
                          + f(hexFrame.GetCellCentroid(Vector2I.Zero, 0)));
        var ddFrame = hexFrame.GetDual();
        Console.WriteLine("Center dual_dual          : "
                          + f(ddFrame.GetCellCentroid(Vector2I.Zero, 0)) +
                          f(ddFrame.GetCellCentroid(Vector2I.Zero, 1)));
        var dddFrame = ddFrame.GetDual();
        Console.WriteLine("Angle dual_dual_dual " + ((HexTileOrientationSystem)dddFrame).TilingAngle.Degrees);
        Console.WriteLine("Center dual_dual_dual     : "
                          + f(dddFrame.GetCellCentroid(Vector2I.Zero, 0)));
        var ddddFrame = dddFrame.GetDual();
        Console.WriteLine("Center dual_dual_dual_dual: "
                          + f(ddddFrame.GetCellCentroid(Vector2I.Zero, 0)) +
                          f(ddddFrame.GetCellCentroid(Vector2I.Zero, 1)));

        var cell = new HexTerrainCell(Vector2I.Zero, hexFrame, hexFrame.GetDual());
        // Console.WriteLine(cell.ToString());
        // Console.WriteLine(hexFrame.GetVertex(Vector2I.Zero, hexFrame.GetPolygonVertexCount(0)-2, 0));
        // Console.WriteLine(hexFrame.GetVertex(Vector2I.Zero, hexFrame.GetPolygonVertexCount(0)-1, 0));


        // Only assert on the rotation/sheering submatrix 
        Assert.That(() => frame.GetDual().GetDual().Transform.SubMatrix(0, 2, 0, 2).ToArray(),
            Is.EqualTo(frame.Transform.SubMatrix(0, 2, 0, 2).ToArray()).AsCollection.Within(1E-5));
        
        
    }
}