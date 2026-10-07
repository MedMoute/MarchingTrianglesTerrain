using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
// ReSharper disable UnusedVariable

namespace UnitTests.test.triangulation;

public class TestAddTriangleOnBorderEdgeAction
{
    private static readonly Vector3 A = Vector3.Up;
    private static readonly Vector3 B = Vector3.Back;
    private static readonly Vector3 C = Vector3.Right;
    private readonly Vector3[] _tri = [A, B, C];

    private Triangulation _t;

    [SetUp]
    public void Setup()
    {
        _t = new Triangulation([A, B, C], false, true);
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
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));

        Assert.DoesNotThrow(() =>
        {
            var action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action.Apply(_t);
        });

        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(2));

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
        var action = new SplitSubEdgeAction(0, 0, 1, 0.5f);
        action.Apply(_t);
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.DoesNotThrow(() =>
        {
            var action2 = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action2.Apply(_t);
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(3));
    }

    [Test]
    public void TestCanApplyAddTriangleActionOnAllEdges()
    {
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 3; i++)
            {
                var action = new AddTrianglesOnBorderEdge(i, new Vector3(_tri[i].X, _tri[i].Y + 10, _tri[i].Z));
                action.Apply(_t);
            }
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(4));

    }

    [Test]
    public void TestCanApplyAddSameTriangleActionTwiceAsNoop()
    {
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));

       Assert.DoesNotThrow(() =>
        {
            var action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action.Apply(_t);
            action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action.Apply(_t);
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(2));

    }
    [Test]
    public void TestCanApplyAddDiffTriangleActionTwice()
    {
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));

        Assert.DoesNotThrow(() =>
        {
            var action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
            action.Apply(_t);
            action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 20, A.Z));
            action.Apply(_t);
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(3));

    }
}