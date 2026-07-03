#pragma warning disable NUnit2021
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Spatial.Euclidean;
using MathNet.Spatial.Units;
using NUnit.Framework;

namespace UnitTests;

public class HexTileTests
{
    [Test]
    public void CanCreateHexTiling()
    {
        Assert.DoesNotThrow(() =>
        {
            var tilingSystem = new HexTileOrientationSystem(new Vector2D(0, 0), new Vector2D(1, 0));
        });

        Assert.Throws<ArgumentException>(() =>
        {
            var tilingSystem = new HexTileOrientationSystem(new Vector2D(0, 0), new Vector2D(0, 0));
        });
    }

    [Test]
    public void CheckBasicFlatTopCellsVertices()
    {
        double eps = 1E-5;
        RegularUniformFrame tilingSystem = new HexTileOrientationSystem(
            new Vector2D(0, 0),
            new Vector2D(1, 0)
        );

        // Check [0,0] cell
        List<Vector2D> expected =
        [
            new(1, 0),
            new(-1, 0),
            new(1d / 2, Math.Sqrt(3) / 2),
            new(-1d / 2, Math.Sqrt(3) / 2),
            new(1d / 2, -Math.Sqrt(3) / 2),
            new(-1d / 2, -Math.Sqrt(3) / 2)
        ];

        var points = tilingSystem.GetVertexPositions(Vector2I.Zero).Select(v => v.Item1).ToList();

        foreach (var pos in points)
        {
            Assert.That(() => pos,
                Is.AnyOf(expected).Using((Vector2D u, Vector2D v) => (u - v).Length < eps));
        }

        // Check [1,1] cell
        var expectedOffset = new Vector2D(3d / 2 + 3d / 2, -Math.Sqrt(3) / 2 + Math.Sqrt(3) / 2);

        expected =
        [
            new Vector2D(1, 0) + expectedOffset,
            new Vector2D(-1, 0) + expectedOffset,
            new Vector2D(1d / 2, Math.Sqrt(3) / 2) + expectedOffset,
            new Vector2D(-1d / 2, Math.Sqrt(3) / 2) + expectedOffset,
            new Vector2D(1d / 2, -Math.Sqrt(3) / 2) + expectedOffset,
            new Vector2D(-1d / 2, -Math.Sqrt(3) / 2) + expectedOffset
        ];

        points = tilingSystem.GetVertexPositions(Vector2I.One).Select(v => v.Item1).ToList();

        foreach (var pos in points)
        {
            Assert.That(() => pos,
                Is.AnyOf(expected).Using((Vector2D u, Vector2D v) => (u - v).Length < eps));
        }

        // Check relative angles :
        var center = tilingSystem.GetCellCentroid(Vector2I.One, 0);

        for (int i = 0; i < 6; i++)
        {
            var v1 = points[i] - center;
            var v2 = points[(i + 1) % 6] - center;
            Assert.That(() => v1.SignedAngleTo(v2).Radians, Is.EqualTo(Angle.FromDegrees(60).Radians).Within(1E-5));
        }
    }


    //Check that the get cell method applies
    // properly the (u,v) = cartesian->local((x,y)-(origin_cartesian))
    [Test]
    public void TestGetCell()
    {
        //Use the Terrain tiling's dual
        var tilingSystem = TerrainSettings.OrientationSystem.GetDual();

        // Manually recreate the expected basis
        var o = ((DoubleDeltaTileOrientationSystem)TerrainSettings.OrientationSystem).DualSeeds.Item1;
        var v = ((DoubleDeltaTileOrientationSystem)TerrainSettings.OrientationSystem).DualSeeds.Item2;

        var bv = v - o;
        var rv = bv.Rotate(Angle.FromRadians(-Math.PI / 3));
        var b0 = bv + rv;
        var b1 = b0.Rotate(Angle.FromRadians(Math.PI / 3));
        // Compute the inverse
        var denom = 1 / (b0.X * b1.Y - b1.X * b0.Y);

        var ib0 = denom * new Vector2D(b1.Y, -b0.Y);
        var ib1 = denom * new Vector2D(-b1.X, b0.X);

        var getFracHexCoords = (Vector2D vCart) =>
        {
            var x = new Vector2D(
                ib0.X * (vCart - o).X + ib1.X * (vCart - o).Y,
                ib0.Y * (vCart - o).X + ib1.Y * (vCart - o).Y);
            return x;
        };

        var uvExpected = (Vector2D vecCart) => HexagonGrid.CubeRound(getFracHexCoords(vecCart));

        // Simple points :

        // Start with the local grid origin
        var vec = o;
        Assert.That(() => uvExpected(vec), Is.EqualTo(tilingSystem.GetCell(vec)));
        Assert.That(() => uvExpected(vec), Is.EqualTo(new Vector2I(0, 0)));
        // Check the cartesian origin , expected to be 1,1 by construction
        uvExpected(new Vector2D(0, 0));

        Assert.That(() => tilingSystem.GetCell(new Vector2D(0, 0)), Is.EqualTo(new Vector2I(1, -1)));

        //Random points
        var rand = new Random();
        for (int i = 0; i < 50; i++)
        {
            var x = rand.NextDouble() * 1000;
            var y = rand.NextDouble() * 1000;
            vec = new Vector2D(x, y);
            Assert.That(() => uvExpected(vec), Is.EqualTo(tilingSystem.GetCell(vec)));
        }

        var seedVec = new Vector2D(15, 12);
        tilingSystem = new HexTileOrientationSystem(new Vector2D(0, 0), seedVec);


        // Tiling origin
        Assert.That(() => tilingSystem.GetCell(new Vector2D(0, 0)), Is.EqualTo(new Vector2I(0, 0)));
        // Inside the first cell from a tiny amount
        Assert.That(() => tilingSystem.GetCell(seedVec * 0.99), Is.EqualTo(new Vector2I(0, 0)));
        // On the edge 
        Assert.That(() => tilingSystem.GetCell(seedVec * 1.01), Is.EqualTo(new Vector2I(1, 0)));

        //Test for random tilings
        var ox = rand.NextDouble();
        var oy = rand.NextDouble();
        var vx = rand.NextDouble();
        var vy = rand.NextDouble();
        o = new Vector2D(ox, oy);
        v = new Vector2D(vx, vy);
        tilingSystem = new HexTileOrientationSystem(o, v);

        // Manually recreate the expected basis
        bv = v - o;
        rv = bv.Rotate(Angle.FromRadians(-Math.PI / 3));
        b0 = bv + rv;
        b1 = b0.Rotate(Angle.FromRadians(Math.PI / 3));
        // Compute the inverse
        denom = 1 / (b0.X * b1.Y - b1.X * b0.Y);

        ib0 = denom * new Vector2D(b1.Y, -b0.Y);
        ib1 = denom * new Vector2D(-b1.X, b0.X);

        uvExpected = (Vector2D vec) => HexagonGrid.CubeRound(new Vector2D(ib0.X * (vec - o).X + ib1.X * (vec - o).Y,
            ib0.Y * (vec - o).X + ib1.Y * (vec - o).Y));


        for (int i = 0; i < 50; i++)
        {
            var x = rand.NextDouble() * 1000;
            var y = rand.NextDouble() * 1000;
            vec = new Vector2D(x, y);
            Assert.That(() => uvExpected(vec), Is.EqualTo(tilingSystem.GetCell(vec)));
        }
    }

    [Test]
    public void TestTransformMatrix()
    {
        RegularUniformFrame tilingSystem = new HexTileOrientationSystem(
            new Vector2D(0, 0),
            new Vector2D(1, 0)
        );

        Assert.That(tilingSystem.GetTileArea, Is.EqualTo(
            tilingSystem.Transform.SubMatrix(0, 2, 0, 2).Determinant()).Within(1e-5));

        Assert.That(
            () => (
                tilingSystem.Transform.SubMatrix(0, 2, 0, 2) *
                tilingSystem.TransformInverse.SubMatrix(0, 2, 0, 2)
            ).ToArray()
            , Is.EqualTo(Matrix<double>.Build.DenseDiagonal(2, 1).ToArray()).AsCollection.Within(1e-5));
    }

    [Test]
    public void TestCartesianToLocalToCartesianIsIdentity()
    {
        //Test with random tilings
        for (int i = 0; i < 10; i++)
        {
            var rand = new Random();
            var ox = rand.NextDouble();
            var vx = rand.NextDouble();
            var oy = rand.NextDouble();
            var vy = rand.NextDouble();
            var o = new Vector2D(ox, oy);
            var v = new Vector2D(vx, vy);
            RegularUniformFrame tilingSystem = new HexTileOrientationSystem(o, v);
            for (int j = 0; j < 10; j++)
            {
                var x = rand.NextDouble();
                var y = rand.NextDouble();
                var vec = new Vector2D(x, y);

                Assert.That(
                    () => tilingSystem.LocalToCartesian(tilingSystem.CartesianToLocal(vec)),
                    Is.EqualTo(vec).Using<Vector2D, Vector2D>((v1, v2) => (v1 - v2).Length < 1e-5));
            }
        }
    }

    [Test]
    public void TestLocalToCartesianToLocalIsIdentity()
    {
        //Test with random tilings
        for (int i = 0; i < 10; i++)
        {
            var rand = new Random();
            var ox = rand.NextDouble();
            var vx = rand.NextDouble();
            var oy = rand.NextDouble();
            var vy = rand.NextDouble();
            var o = new Vector2D(ox, oy);
            var v = new Vector2D(vx, vy);
            RegularUniformFrame tilingSystem = new HexTileOrientationSystem(o, v);
            for (int j = 0; j < 10; j++)
            {
                var x = rand.NextDouble();
                var y = rand.NextDouble();
                var vec = new Vector2D(x, y);

                Assert.That(
                    () => tilingSystem.CartesianToLocal(tilingSystem.LocalToCartesian(vec)),
                    Is.EqualTo(vec).Using<Vector2D, Vector2D>((v1, v2) => (v1 - v2).Length < 1e-5));
            }
        }
    }

    [Test]
    public void TestOffsetFrame()
    {
        RegularUniformFrame tilingSystem = new HexTileOrientationSystem(
            new Vector2D(0, 0),
            new Vector2D(1, 0));

        var off = new Vector2D(1, 1);

        var offset = tilingSystem.OffsetBy(off);

        // Check that the square submatrix is unchanged
        Assert.That(() => tilingSystem.Transform.SubMatrix(0, 2, 0, 2).ToArray(),
            Is.EqualTo(offset.Transform.SubMatrix(0, 2, 0, 2).ToArray()).AsCollection.Within(1E-5));
        // Check that the origin is the sum of the non-offset frame origin + the offset
        Assert.That(() => offset.Transform.Column(2).ToArray(),
            Is.EqualTo((Vector2D.OfVector(tilingSystem.Transform.Column(2)) + off).ToVector().ToArray())
                .AsCollection.Within(1E-5));
        
        //Random offsets
        var rand = new Random();
        for (int i = 0; i < 10; i++)
        {
            var ox = rand.NextDouble();
            var oy = rand.NextDouble();
            off = new Vector2D(ox, oy); 
            offset = tilingSystem.OffsetBy(off);
            // Check that the square submatrix is unchanged
            Assert.That(() => tilingSystem.Transform.SubMatrix(0, 2, 0, 2).ToArray(),
                Is.EqualTo(offset.Transform.SubMatrix(0, 2, 0, 2).ToArray()).AsCollection.Within(1E-5));
            // Check that the origin is the sum of the non-offset frame origin + the offset
            Assert.That(() => offset.Transform.Column(2).ToArray(),
                Is.EqualTo((Vector2D.OfVector(tilingSystem.Transform.Column(2)) + off).ToVector().ToArray())
                    .AsCollection.Within(1E-5));
        }
    }

    [Test]
    public void TestClone()
    {
        RegularUniformFrame tilingSystem = new HexTileOrientationSystem(
            new Vector2D(0, 0),
            new Vector2D(1, 0));
        Assert.That(() => (tilingSystem.Clone() as HexTileOrientationSystem).Transform.ToArray(),
            Is.EqualTo(tilingSystem.Transform.ToArray()).AsCollection.Within(1E-5));

        //Random clones : 
        //Test with random tilings
        var rand = new Random();
        for (int i = 0; i < 10; i++)
        {
            var ox = rand.NextDouble();
            var vx = rand.NextDouble();
            var oy = rand.NextDouble();
            var vy = rand.NextDouble();
            var o = new Vector2D(ox, oy);
            var v = new Vector2D(vx, vy);
            tilingSystem = new HexTileOrientationSystem(o, v);
            Assert.That(() => (tilingSystem.Clone() as HexTileOrientationSystem).Transform.ToArray(),
                Is.EqualTo(tilingSystem.Transform.ToArray()).AsCollection.Within(1E-5));
        }
    }
}