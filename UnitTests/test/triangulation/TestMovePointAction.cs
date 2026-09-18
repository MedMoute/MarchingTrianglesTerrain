using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;

namespace UnitTests.triangulation;

public class TestMovePointAction
{
    private static Vector3 A = Vector3.Up;
    private static Vector3 B = Vector3.Back;
    private static Vector3 C = Vector3.Right;
    private Vector3[] tri = [A, B, C];

    private Triangulation t;


    [SetUp]
    public void Setup()
    {
        t = new Triangulation([A, B, C],true,true);
    }
    
    [Test]
    public void TestCanCreateMovePointAction()
    {
        Assert.DoesNotThrow(() =>
        {
            var action = new MovePointAlongYAxisAction(0, 0);
        });
        
        var  e  =Assert.Throws<ArgumentException>(() =>
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
            action.Apply(t);
        });
        
        Assert.That(e,Has.Message.EqualTo("Could not find the point inside the triangulation."));
    }
    
    [Test]
    public void TestApplyMovePointAction()
    {
        int eIdx = 0;
        Assert.That(t.ToTriangleInfoList()[0].Points[eIdx].Y, Is.EqualTo(tri[eIdx].Y));

        Assert.DoesNotThrow(() =>
        {
            var action = new MovePointAlongYAxisAction(eIdx, 100);
            action.Apply(t);
        });
        // No new triangle
        Assert.That(t.ToTriangleInfoList().Count, Is.EqualTo(1));
        Assert.That(t.ToTriangleInfoList()[0].Points[eIdx].Y, Is.EqualTo(100));
    }
}