using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
// ReSharper disable UnusedVariable

namespace UnitTests.test.triangulation;

public class TestDisplaceEdge
{
    private static readonly Vector3 A = Vector3.Up;
    private static readonly Vector3 B = Vector3.Back;
    private static readonly Vector3 C = Vector3.Right;
    private Vector3[] _tri = [A, B, C];

    private Triangulation _t;


    [SetUp]
    public void Setup()
    {
        _t = new Triangulation(_tri, false, true);
    }

    [Test]
    public void TestCanCreateDisplaceEdgeAction([Values]SnapMode snapMode)
    {
        //Standard usage
        Assert.DoesNotThrow(() =>
        {
            var action = new DisplaceEdgeAlongYAxis(0, -1, 1,snapMode);
        });
        Assert.DoesNotThrow(() =>
        {
            var action = new DisplaceEdgeAlongYAxis(0, -1, 1,snapMode);
        });
        // Bad edge index
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var action = new DisplaceEdgeAlongYAxis(-1, -1, 1,snapMode);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var action = new DisplaceEdgeAlongYAxis(10, -1, 1,snapMode);
        });
    }


    [Test]
    public void TestCanApplyDisplaceEdgeAction([Values]SnapMode snapMode)
    {
        float h0 = -1f;
        float h1 = -5f;

        //Standard usage
        Assert.DoesNotThrow(() =>
        {
            var action = new DisplaceEdgeAlongYAxis(0, h0, h1,snapMode);
            action.Apply(_t);
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));
        Assert.That(_t.ToTriangleInfoList()[0].Points, Has.One.With.Matches<Vector3>(v=> Math.Abs(v.Y - h0) < 1e-5));
        Assert.That(_t.ToTriangleInfoList()[0].Points, Has.One.With.Matches<Vector3>(v=> Math.Abs(v.Y - h1) < 1e-5));
    }
}