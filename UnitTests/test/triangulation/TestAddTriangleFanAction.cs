using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
// ReSharper disable UnusedVariable

namespace UnitTests.test.triangulation;

public class TestAddTriangleFanAction
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
    public void TestCanCreateAddTriangleFanAction()
    {
        //Standard usage
        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 1), B + Vector3.Up);
        });

        //Standard usage with incorrect displacement, will not throw at creation, but at execution
        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 1), B + Vector3.One);
        });

        //Bad index
        var e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new AddTriangleFan((0, -1), B + Vector3.Up);
        });
        Assert.That(e, Has.Message.Contains("Illegal index in "));
    }

    [Test]
    public void TestApplyIncorrectAddTriangleFanAction()
    {
        int eIdx = 0;
        Assert.That(_t.ToTriangleInfoList()[0].Points[eIdx].Y, Is.EqualTo(_tri[eIdx].Y));

        var e = Assert.Throws<Exception>(() =>
        {
            var action = new AddTriangleFan((0, 1), B + Vector3.One);
            action.Apply(_t);
        });
        Assert.That(e, Has.Message.Contains("The last action affected the convex hull area !!"));
    }

    [Test]
    public void TestApplyAddTriangleFanAction()
    {
        var edit = B + Vector3.Up;

        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));
        Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.Exactly(3).EqualTo(true));
        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 1), edit);
            action.Apply(_t);
        });

        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.That(_t.ToTriangleInfoList()[0].Points, Has.No.EqualTo(edit));
        Assert.That(_t.ToTriangleInfoList()[1].Points, Has.Exactly(1).EqualTo(edit));

        Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.Exactly(2).EqualTo(true));
        Assert.That(_t.ToTriangleInfoList()[1].EdgeBorderFlags, Has.Exactly(2).EqualTo(true));
    }

    [Test]
    public void TestCannotApplyAddTriangleFanActionTwiceOnSameEdge()
    {
        var edit = B + Vector3.Up;

        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 1), edit);
            action.Apply(_t);
        });
        edit += Vector3.Up;
        var e = Assert.Throws<NotSupportedException>(() =>
        {
            var action = new AddTriangleFan((0, 1), edit);
            action.Apply(_t);
        });

        Assert.That(e,
            Has.Message.EqualTo(
                "Adding a triangle fan to an edge that is not on the manifold border is not supported."));
    }
    
    [Test]
    public void TestApplyAddTriangleFanActionOnAddedTriangle()
    {
        var edit = B + Vector3.Up;

        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));
        Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.Exactly(3).EqualTo(true));
        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 1), edit);
            action.Apply(_t);
        });

        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.That(_t.ToTriangleInfoList()[0].Points, Has.No.EqualTo(edit));
        Assert.That(_t.ToTriangleInfoList()[1].Points, Has.Exactly(1).EqualTo(edit));

        Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.Exactly(2).EqualTo(true));
        Assert.That(_t.ToTriangleInfoList()[1].EdgeBorderFlags, Has.Exactly(2).EqualTo(true));

        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 3), edit+Vector3.Up);
            action.Apply(_t);
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(3));
        Assert.That(_t.ToTriangleInfoList()[0].Points, Has.No.EqualTo(edit));
        Assert.That(_t.ToTriangleInfoList()[1].Points, Has.Exactly(1).EqualTo(edit));
        
        Assert.That(_t.ToTriangleInfoList()[2].Points, Has.Exactly(1).EqualTo(edit));
        Assert.That(_t.ToTriangleInfoList()[2].Points, Has.Exactly(1).EqualTo(edit+Vector3.Up));

        Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.Exactly(2).EqualTo(true));
        Assert.That(_t.ToTriangleInfoList()[1].EdgeBorderFlags, Has.Exactly(1).EqualTo(true));
        Assert.That(_t.ToTriangleInfoList()[2].EdgeBorderFlags, Has.Exactly(2).EqualTo(true));
        
    }
    
    [Test]
    public void TestCanApplyAddTriangleFanActionOnExistingPointAsNoop()
    {
        var edit = B;  
        
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));
        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 1), edit);
            action.Apply(_t);
        });

        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(1));
    }
}