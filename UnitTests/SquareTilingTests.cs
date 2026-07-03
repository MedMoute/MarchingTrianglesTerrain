#pragma warning disable NUnit2021
using System;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MathNet.Spatial.Euclidean;
using NUnit.Framework;

namespace UnitTests;

public class SquareTilingTests
{
    [Test]
    public void CanCreateSquareTiling()
    {
        Assert.DoesNotThrow(() =>
        {
            var tilingSystem = new SquareTileFrame(new Vector2D(0, 0), new Vector2D(1, 0));
        });

        Assert.Throws<ArgumentException>(() => new SquareTileFrame(new Vector2D(0, 0), new Vector2D(0, 0)));
    }

    [Test]
    public void CheckSquareTilingVertexLengths()
    {
        // [0,0] Cell with [1,1] vector
        var Vec1 = new Vector2D(1, 1);
        RegularUniformFrame tilingSystem = new SquareTileFrame(new Vector2D(0, 0), Vec1);
        foreach (var vec in tilingSystem.GetVertexPositions(Vector2I.Zero))
        {
            Assert.That(() => vec.Item1.Length, Is.EqualTo(Mathf.Sqrt2).Within(1E-5));
        }

        // [0,0] Cell with [1,0] vector
        tilingSystem = new SquareTileFrame(new Vector2D(0, 0), new Vector2D(1, 0));
        foreach (var vec in tilingSystem.GetVertexPositions(Vector2I.Zero))
        {
            Assert.That(() => vec.Item1.Length, Is.EqualTo(1f).Within(1E-5));
        }

        // Random cells with [1,1] vector
        var rand = new Random(10);
        for (int i = 0; i < 10; i++)
        {
            tilingSystem = new SquareTileFrame(new Vector2D(0, 0), new Vector2D(1, 1));

            var x1 = rand.Next(-10, 10);
            var y1 = rand.Next(-10, 10);
            var cell = new Vector2I(x1, y1);
            foreach (var vec in tilingSystem.GetVertexPositions(cell))
            {
                var pcenter = tilingSystem.LocalToCartesian.Invoke(new Vector2D(cell.X, cell.Y));
                Assert.That(() => pcenter, Is.EqualTo(tilingSystem.GetCellCentroid(cell, 0))
                    .Using<Vector2, Vector2>((o1, o2) =>
                        Math.Abs((o2 - o1).X) < 1E-5 && Math.Abs((o2 - o1).Y) < 1E-5));

                Assert.That(() => (vec.Item1 - pcenter).Length, Is.EqualTo(Mathf.Sqrt2).Within(1E-5));
            }
        }

        // Random cells with Random vector
        {
            Vec1 = new Vector2D(
                rand.NextDouble() * 20 - 10,
                rand.NextDouble() * 20 - 10);
            tilingSystem = new SquareTileFrame(new Vector2D(0, 0), Vec1);

            var x1 = rand.Next(-10, 10);
            var y1 = rand.Next(-10, 10);
            var cell = new Vector2I(x1, y1);
            foreach (var vec in tilingSystem.GetVertexPositions(cell))
            {
                var pcenter = Vector2D.OfVector(
                    tilingSystem.Transform.Column(0) * cell.X +
                    tilingSystem.Transform.Column(1) * cell.Y);

                Assert.That(() => pcenter, Is.EqualTo(tilingSystem.GetCellCentroid(cell, 0))
                    .Using<Vector2D, Vector2D>((o1, o2) =>
                        Math.Abs((o2 - o1).X) < 1E-5 && Math.Abs((o2 - o1).Y) < 1E-5));

                Assert.That(() => (vec.Item1 - pcenter).Length, Is.EqualTo(Vec1.Length).Within(1E-5));
            }
        }
    }

    [Test]
    public void EnsureDualHasVertexForCenter()
    {
        // [0,0] Case
        RegularUniformFrame tilingSystem = new SquareTileFrame(new Vector2D(0, 0), new Vector2D(1, 1));
        RegularUniformFrame dual = tilingSystem.GetDual();

        Assert.That(() => dual, Is.Not.Null);

        Assert.That(
            () => dual.GetCellCentroid(Vector2I.Zero, 0),
            Is.AnyOf(tilingSystem.GetVertexPositions(Vector2I.Zero).Select(p => p.Item1).ToList())
                .Using((Vector2D o1, Vector2D o2) => (o2 - o1).Length < 1E-5));

        Assert.That(
            () => tilingSystem.GetCellCentroid(Vector2I.Zero, 0),
            Is.AnyOf(dual.GetVertexPositions(Vector2I.Zero).Select(p => p.Item1).ToList())
                .Using((Vector2D o1, Vector2D o2) => (o2 - o1).Length < 1E-5));

        // Random cases
        var rand = new Random(10);
        for (int i = 0; i < 10; i++)
        {
            var x1 = rand.NextSingle() * 20 - 10;
            var y1 = rand.NextSingle() * 20 - 10;

            var x2 = rand.NextSingle() * 20 - 10;
            var y2 = rand.NextSingle() * 20 - 10;

            tilingSystem = new SquareTileFrame(new Vector2D(x1, y1), new Vector2D(x2, y2));
            dual = tilingSystem.GetDual();

            Assert.That(
                () => dual.GetCellCentroid(Vector2I.Zero, 0),
                Is.AnyOf(tilingSystem.GetVertexPositions(Vector2I.Zero).Select(p => p.Item1).ToList())
                    .Using((Vector2D o1, Vector2D o2) =>(o2 - o1).Length< 1E-5));

            Assert.That(
                () => tilingSystem.GetCellCentroid(Vector2I.Zero, 0),
                Is.AnyOf(dual.GetVertexPositions(Vector2I.Zero).Select(p => p.Item1).ToList())
                    .Using((Vector2D o1, Vector2D o2) =>(o2 - o1).Length < 1E-5));
        }
    }

    [Test]
    public void EnsureDualHasSameOrientation()
    {
        // [0;0] case
        RegularUniformFrame tilingSystem = new SquareTileFrame(new Vector2D(0, 0), new Vector2D(1, 1));
        RegularUniformFrame dual = tilingSystem.GetDual();
        Assert.That(() => (tilingSystem as SquareTileFrame)!.TilingAngle.Radians,
            Is.EqualTo((dual as SquareTileFrame)!.TilingAngle.Radians).Within(1E-5));

        // Random cases
        var rand = new Random(10);
        for (int i = 0; i < 10; i++)
        {
            var x1 = rand.NextSingle() * 20 - 10;
            var y1 = rand.NextSingle() * 20 - 10;

            var x2 = rand.NextSingle() * 20 - 10;
            var y2 = rand.NextSingle() * 20 - 10;

            tilingSystem = new SquareTileFrame(new Vector2D(x1, y1), new Vector2D(x2, y2));
            dual = tilingSystem.GetDual();

            Assert.That(() => (tilingSystem as SquareTileFrame)!.TilingAngle.Radians,
                Is.EqualTo((dual as SquareTileFrame)!.TilingAngle.Radians).Within(1E-5));
        }
    }
}