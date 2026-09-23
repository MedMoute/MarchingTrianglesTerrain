using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;

namespace UnitTests.triangulation;

public class TestAddTriangleOnBorderEdgeAction
{
    private static Vector3 A = Vector3.Up;
    private static Vector3 B = Vector3.Back;
    private static Vector3 C = Vector3.Right;
    private Vector3[] tri = [A, B, C];

    private Triangulation t;

    [SetUp]
    public void Setup()
    {
        t = new Triangulation([A, B, C], true, true);
    }

    [Test]
    public void TestCanCreateAddTriangleAction()
    {
        Assert.DoesNotThrow(() =>
        {
            var action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
        });

        var e = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var action = new AddTrianglesOnBorderEdge(-1, new Vector3(A.X, A.Y + 10, A.Z));
        });
        Assert.That(e, Has.Message.Contains("Should have a value between 0 and 2"));
    }

    [Test]
    public void TestCanApplyAddTriangleAction()
    {
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(1));

        Assert.DoesNotThrow(() =>
        {
            var action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action.Apply(t);
        });

        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(2));

        //Invalid edge
        var e = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var action = new AddTrianglesOnBorderEdge(-1, new Vector3(A.X, A.Y + 10, A.Z));
        });
        Assert.That(e, Has.Message.Contains("Should have a value between 0 and 2"));
    }

    [Test]
    public void TestCanApplyAddTriangleActionOnSplitEdge()
    {
        var action = new SplitEdgeAction(0, 0, 1, 0.5f);
        action.Apply(t);
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.DoesNotThrow(() =>
        {
            var action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action.Apply(t);
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(4));
    }

    [Test]
    public void TestCanApplyAddTriangleActionOnAllEdges()
    {
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 3; i++)
            {
                var action = new AddTrianglesOnBorderEdge(i, new Vector3(tri[i].X, tri[i].Y + 10, tri[i].Z));
                action.Apply(t);
            }
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(4));

    }

    [Test]
    public void TestCanApplyAddSameTriangleActionTwiceAsNoop()
    {
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(1));

       Assert.DoesNotThrow(() =>
        {
            var action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action.Apply(t);
            action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action.Apply(t);
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(2));

    }
    [Test]
    public void TestCanApplyAddDiffTriangleActionTwice()
    {
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(1));

        Assert.DoesNotThrow(() =>
        {
            var action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action.Apply(t);
            action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 20, A.Z));
            action.Apply(t);
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(3));

    }
}