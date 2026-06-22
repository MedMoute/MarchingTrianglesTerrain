using Godot;
using Localproto.addons.marchingTriangles.tiling;
using Localproto.addons.marchingTriangles.utils;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Spatial.Euclidean;
using MathNet.Spatial.Units;

#pragma warning disable NUnit2021
namespace Localproto.UnitTests;

public class DeltaTilingTests
{
    [Test]
    public void CanCreateDeltaTiling()
    {
        Assert.DoesNotThrow(() =>
        {
            var tilingSystem = new DoubleDeltaTileOrientationSystem(new Vector2D(0, 0), new Vector2D(1, 0));
        });

        Assert.Throws<ArgumentException>(() =>
        {
            var tilingSystem = new DoubleDeltaTileOrientationSystem(new Vector2D(0, 0), new Vector2D(0, 0));
        });
    }

    [Test]
    public void EnsureBasicCellVertices()
    {
        double eps = 1E-5;
        RegularUniformFrame tilingSystem = new DoubleDeltaTileOrientationSystem(
            new Vector2D(1d / 2, 1 / (2 * Math.Sqrt(3))),
            new Vector2D(1, 1 / Math.Sqrt(3)));

        List<Vector2D> expected =
        [
            new(0, 0),
            new(1, 0),
            new(3d / 2, Math.Sqrt(3) / 2),
            new(1d / 2, Math.Sqrt(3) / 2)
        ];

        var points = tilingSystem.GetVertexPositions(Vector2I.Zero).Select(v => v.Item1).ToList();

        foreach (var pos in points)
        {
            Assert.That(() => new List<Vector2D>() { pos },
                Is.SubsetOf(expected)
                    .Using<Vector2D, Vector2D>((u, v) => (u - v).Length < eps));
        }
    }

    [Test]
    // This tests ensures that the DualDelta Tiling "GetVertices" returns 6 entries with two having their
    // Position overlapping (same point, different triangle)
    public void GetSimpleVerticesHaveExpectedOverlap()
    {
        float eps = 1E-5f;
        List<Vector2D> seedVecs = [new(0, 0), new(1, 1)];
        var tilingSystem = new DoubleDeltaTileOrientationSystem(seedVecs[0], seedVecs[1]);
        List<Vector2I> checkedCells = [Vector2I.Zero];
        //Check on [0,0] cell for [[0,0];[1,1]] cell Seeding pair
        AssertGridVerticesOverlap(eps, tilingSystem, checkedCells);
    }

    [Test]
    public void GetVerticesHaveExpectedOverlapForRandomCells()
    {
        float eps = 1E-5f;
        List<Vector2D> seedVecs = [new(0, 0), new(1, 1)];
        RegularUniformFrame tilingSystem = new DoubleDeltaTileOrientationSystem(seedVecs[0], seedVecs[1]);

        // Check on random cells for [[0,0];[1,1]] cell Seeding pair
        Random rand = new Random(0);
        List<Vector2I> checkedCells = new List<Vector2I>();
        for (int i = 0; i < 200; i++)
        {
            // Bounded to 1M because we start having floating point precision beyond that
            checkedCells.Add(new Vector2I(rand.Next(1_000_000), rand.Next(1_000_000)));
        }

        AssertGridVerticesOverlap(eps, tilingSystem, checkedCells);
        AssertGridVerticesDistances(eps, tilingSystem, checkedCells);
    }

    private static void AssertGridVerticesOverlap(float eps, RegularUniformFrame tilingSystem,
        List<Vector2I> checkedCells)
    {
        foreach (var cell in checkedCells)
        {
            var points = tilingSystem.GetVertexPositions(cell);
            var setPoints = new HashSet<Vector2D>();
            foreach (var point in points)
            {
                if (!setPoints.Contains(point.Item1) && !setPoints.Any(p => (p - point.Item1).Length < eps))
                {
                    setPoints.Add(point.Item1);
                }
            }

            Assert.That(() => setPoints, Is.Not.Empty);
            Assert.That(() => setPoints.Count, Is.EqualTo(4));
        }
    }

    private static void AssertGridVerticesDistances(float eps, RegularUniformFrame tilingSystem,
        List<Vector2I> checkedCells)
    {
        foreach (var cell in checkedCells)
        {
            var points = tilingSystem.GetVertexPositions(cell);
            foreach (var point in points)
            {
                Assert.That(
                    () => (point.Item1 - tilingSystem.GetCellCentroid(cell, point.Item2)).Length,
                    Is.EqualTo(tilingSystem.TilingScale).Within(eps));
            }
        }
    }

    [Test]
    public void CheckDualDeltaTilingVertices()
    {
        // [0,0] Cell with [1,1] vector
        var seedVec = new Vector2D(1, 1);
        RegularUniformFrame tilingSystem = new DoubleDeltaTileOrientationSystem(new Vector2D(0, 0), seedVec);
        var cell = Vector2I.Zero;
        foreach (var vec in tilingSystem.GetVertexPositions(cell))
        {
            var center = tilingSystem.GetCellCentroid(cell, vec.Item2);

            Assert.That(
                () => (vec.Item1 - center).Length,
                Is.EqualTo(seedVec.Length).Within(1E-5));
        }
    }

    //Check that the get cell method applies
    // properly the (u,v) = cartesian->local((x,y)-(origin_cartesian))
    [Test]
    public void TestGetCell()
    {
        //
        var seedVec = new Vector2D(15, 12);
        RegularUniformFrame tilingSystem = new DoubleDeltaTileOrientationSystem(new Vector2D(0, 0), seedVec);

        Assert.That(() => tilingSystem.GetCell(seedVec), Is.EqualTo(new Vector2I(0, 0)));

        //Use the Terrain tiling
        tilingSystem = TerrainSettings.OrientationSystem;

        var u_expected = (Vector2D vec) => (int)Math.Floor(vec.X - vec.Y / Math.Sqrt(3));
        var v_expected = (Vector2D vec) => (int)Math.Floor(vec.Y / (Math.Sqrt(3) / 2));


        var rand = new Random();
        for (int i = 0; i < 50; i++)
        {
            var x = rand.NextDouble() * 1000;
            var y = rand.NextDouble() * 1000;
            var vec = new Vector2D(x, y);
            Assert.That(() => new Vector2I(u_expected(vec), v_expected(vec)), Is.EqualTo(tilingSystem.GetCell(vec)));
        }

        //Test for random tilings
        var c1x = rand.NextDouble();
        var c2x = rand.NextDouble();
        var c1y = rand.NextDouble();
        var c2y = rand.NextDouble();
        var c1 = new Vector2D(c1x, c1y);
        var c2 = new Vector2D(c2x, c2y);
        tilingSystem = new DoubleDeltaTileOrientationSystem(c1, c2);

        // Manually recreate the expected basis
        var j = c2 - c1;
        var origin = c1 - (j);
        var b_mid = Math.Sqrt(3) * j; // From construction + Pythagoras 
        var b0 = b_mid.Rotate(Angle.FromDegrees(-30));
        var b1 = b_mid.Rotate(Angle.FromDegrees(30));
        // Compute the inverse
        var denom = 1/(b0.X*b1.Y-b1.X*b0.Y);

        var ib0 = denom * new Vector2D(b1.Y, -b0.Y);
        var ib1 = denom * new Vector2D(-b1.X, b0.X);

        u_expected = vec => (int)Math.Floor( ib0.X * (vec-origin).X + ib1.X * (vec-origin).Y);

        v_expected = vec => (int)Math.Floor( ib0.Y * (vec-origin).X + ib1.Y * (vec-origin).Y);

        for (int i = 0; i < 50; i++)
        {
            var x = rand.NextDouble() * 1000;
            var y = rand.NextDouble() * 1000;
            var vec = new Vector2D(x, y);
            Assert.That(() => new Vector2I(u_expected(vec), v_expected(vec)), Is.EqualTo(tilingSystem.GetCell(vec)));
        }
    }


    [Test]
    public void TestTransformFromRandomPoints()
    {
        var rand = new Random(0);

        //Check random tilings
        var c1x = rand.NextDouble();
        var c2x = rand.NextDouble();
        var c1y = rand.NextDouble();
        var c2y = rand.NextDouble();
        var c1 = new Vector2D(c1x, c1y);
        var c2 = new Vector2D(c2x, c2y);
        var tilingSystem = new DoubleDeltaTileOrientationSystem(c1, c2);

        // Manually recreate the expected basis
        var j = c2 - c1;
        var origin = c1 - (j);
        var b_mid = Math.Sqrt(3) * j;
        var b0 = b_mid.Rotate(Angle.FromDegrees(-30));
        var b1 = b_mid.Rotate(Angle.FromDegrees(30));

        Assert.That(() => Vector2D.OfVector(tilingSystem.Transform.Column(2)),
            Is.EqualTo(origin)
                .Using<Vector2D, Vector2D>((v1, v2) => (v1 - v2).Length < 1e-5));

        Assert.That(() => Vector2D.OfVector(tilingSystem.Transform.Column(0)),
            Is.EqualTo(b0)
                .Using<Vector2D, Vector2D>((v1, v2) => (v1 - v2).Length < 1e-5));

        Assert.That(() => Vector2D.OfVector(tilingSystem.Transform.Column(1)),
            Is.EqualTo(b1)
                .Using<Vector2D, Vector2D>((v1, v2) => (v1 - v2).Length < 1e-5));
    }

    [Test]
    public void TestTransformMatrices()
    {
        RegularUniformFrame tilingSystem = new DoubleDeltaTileOrientationSystem(
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
            RegularUniformFrame tilingSystem = new DoubleDeltaTileOrientationSystem(o, v);
            for (int j = 0; j < 10; j++)
            {
                var x = rand.NextDouble();
                var y = rand.NextDouble();
                var vec = new Vector2D(x, y);
                
                Assert.That(
                    ()=>tilingSystem.LocalToCartesian(tilingSystem.CartesianToLocal(vec)),
                    Is.EqualTo(vec).Using<Vector2D,Vector2D>((v1, v2)=>(v1-v2).Length<1e-5));
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
            RegularUniformFrame tilingSystem = new DoubleDeltaTileOrientationSystem(o, v);
            for (int j = 0; j < 10; j++)
            {
                var x = rand.NextDouble();
                var y = rand.NextDouble();
                var vec = new Vector2D(x, y);
                
                Assert.That(
                    ()=>tilingSystem.CartesianToLocal(tilingSystem.LocalToCartesian(vec)),
                    Is.EqualTo(vec).Using<Vector2D,Vector2D>((v1, v2)=>(v1-v2).Length<1e-5));
            }
        }
    }
    
    [Test]
    public void TestOffsetFrame()
    {
        RegularUniformFrame tilingSystem = new DoubleDeltaTileOrientationSystem(
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
        RegularUniformFrame tilingSystem = new DoubleDeltaTileOrientationSystem(
            new Vector2D(0, 0),
            new Vector2D(1, 0));
        Assert.That(() => (tilingSystem.Clone() as DoubleDeltaTileOrientationSystem).Transform.ToArray(),
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
            tilingSystem = new DoubleDeltaTileOrientationSystem(o, v);
            Assert.That(() => (tilingSystem.Clone() as DoubleDeltaTileOrientationSystem).Transform.ToArray(),
                Is.EqualTo(tilingSystem.Transform.ToArray()).AsCollection.Within(1E-5));
        }
    }

    [Test]
    public void EnsureDualOfOffsetIsOffsetOfDual()
    {
        RegularUniformFrame tilingSystem = TerrainSettings.OrientationSystem;

        var off = new Vector2D(10, -10);

        var offset = tilingSystem.OffsetBy(off);
        var dualOfOffset = offset.GetDual();
        var offsetOfDual = tilingSystem.GetDual().OffsetBy(off);

        Assert.That(() => dualOfOffset.Transform.ToArray(),
            Is.EqualTo(offsetOfDual.Transform.ToArray()).AsCollection.Within(1E-5));

    }
    
}