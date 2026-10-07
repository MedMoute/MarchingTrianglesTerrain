using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
// ReSharper disable UnusedVariable

namespace UnitTests.test.triangulation;

public class TestMovePointAction
{
    private static readonly Vector3 A = Vector3.Up;
    private static readonly Vector3 B = Vector3.Back;
    private static readonly Vector3 C = Vector3.Right;
    private readonly Vector3[] _tri = [A, B, C];

    private Triangulation _t;


    [SetUp]
    public void Setup()
    {
        _t = new Triangulation(_tri, false, true);
    }

    [Test]
    public void TestCanCreateMovePointAction()
    {
        Assert.DoesNotThrow(() =>
        {
            var action = new MovePointAlongYAxisAction(0, 0);
        });

        var e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new MovePointAlongYAxisAction(-1, 0);
        });
        Assert.That(e, Has.Message.Contains("Illegal index"));
    }

    [Test]
    public void TestCannotApplyMovePointActionWithIllegalIndex()
    {
        var e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new MovePointAlongYAxisAction(3, 0);
            action.Apply(_t);
        });

        Assert.That(e, Has.Message.EqualTo("Could not find the point inside the triangulation."));
    }

    [Test]
    public void TestApplyMovePointAction()
    {
        int eIdx = 0;
        Assert.That(_t.ToTriangleInfoList()[0].Points[eIdx].Y, Is.EqualTo(_tri[eIdx].Y));

        Assert.DoesNotThrow(() =>
        {
            var action = new MovePointAlongYAxisAction(eIdx, 100);
            action.Apply(_t);
        });
        // No new triangle
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));
        Assert.That(_t.ToTriangleInfoList()[0].Points[eIdx].Y, Is.EqualTo(100));
        Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.All.True);
    }

    [Test]
    public void TestApplyMovePointActionTwiceOnSamePoint()
    {
        int eIdx = 0;
        Assert.That(_t.ToTriangleInfoList()[0].Points[eIdx].Y, Is.EqualTo(_tri[eIdx].Y));

        Assert.DoesNotThrow(() =>
        {
            var action = new MovePointAlongYAxisAction(eIdx, 100);
            action.Apply(_t);
            action = new MovePointAlongYAxisAction(eIdx, 200);
            action.Apply(_t);
        });
        // No new triangle
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));
        Assert.That(_t.ToTriangleInfoList()[0].Points[eIdx].Y, Is.EqualTo(200));
        Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.All.True);
    }

    [Test]
    public void TestApplyMovePointActionOnAllPoints()
    {
        for (int eIdx = 0; eIdx < 3; eIdx++)
        {
            Assert.DoesNotThrow(() =>
            {
                var action = new MovePointAlongYAxisAction(eIdx, eIdx * 10);
                action.Apply(_t);
            });
            // No new triangle
            Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));
            Assert.That(_t.ToTriangleInfoList()[0].Points[eIdx],
                Is.EqualTo(new Vector3(_tri[eIdx].X, eIdx * 10, _tri[eIdx].Z)));
            Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.All.True);
        }
    }
}