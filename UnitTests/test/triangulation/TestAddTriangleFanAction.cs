using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace UnitTests.triangulation;

public class TestAddTriangleFanAction
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
        Assert.That(t.ToTriangleInfoList()[0].Points[eIdx].Y, Is.EqualTo(tri[eIdx].Y));

        var e = Assert.Throws<Exception>(() =>
        {
            var action = new AddTriangleFan((0, 1), B + Vector3.One);
            action.Apply(t);
        });
        Assert.That(e, Has.Message.Contains("The last action affected the convex hull area !!"));
    }

    [Test]
    public void TestApplyAddTriangleFanAction()
    {
        var edit = B + Vector3.Up;

        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(1));
        Assert.That(t.ToTriangleInfoList()[0].edgeBorderFlags, Has.Exactly(3).EqualTo(true));
        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 1), edit);
            action.Apply(t);
        });

        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.That(t.ToTriangleInfoList()[0].Points, Has.No.EqualTo(edit));
        Assert.That(t.ToTriangleInfoList()[1].Points, Has.Exactly(1).EqualTo(edit));

        Assert.That(t.ToTriangleInfoList()[0].edgeBorderFlags, Has.Exactly(2).EqualTo(true));
        Assert.That(t.ToTriangleInfoList()[1].edgeBorderFlags, Has.Exactly(2).EqualTo(true));
    }

    [Test]
    public void TestCannotApplyAddTriangleFanActionTwiceOnSameEdge()
    {
        var edit = B + Vector3.Up;

        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 1), edit);
            action.Apply(t);
        });
        edit += Vector3.Up;
        var e = Assert.Throws<NotSupportedException>(() =>
        {
            var action = new AddTriangleFan((0, 1), edit);
            action.Apply(t);
        });

        Assert.That(e,
            Has.Message.EqualTo(
                "Adding a triangle fan to an edge that is not on the manifold border is not supported."));
    }
    
    [Test]
    public void TestApplyAddTriangleFanActionOnAddedTriangle()
    {
        var edit = B + Vector3.Up;

        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(1));
        Assert.That(t.ToTriangleInfoList()[0].edgeBorderFlags, Has.Exactly(3).EqualTo(true));
        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 1), edit);
            action.Apply(t);
        });

        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.That(t.ToTriangleInfoList()[0].Points, Has.No.EqualTo(edit));
        Assert.That(t.ToTriangleInfoList()[1].Points, Has.Exactly(1).EqualTo(edit));

        Assert.That(t.ToTriangleInfoList()[0].edgeBorderFlags, Has.Exactly(2).EqualTo(true));
        Assert.That(t.ToTriangleInfoList()[1].edgeBorderFlags, Has.Exactly(2).EqualTo(true));

        Assert.DoesNotThrow(() =>
        {
            var action = new AddTriangleFan((0, 3), edit+Vector3.Up);
            action.Apply(t);
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(3));
        Assert.That(t.ToTriangleInfoList()[0].Points, Has.No.EqualTo(edit));
        Assert.That(t.ToTriangleInfoList()[1].Points, Has.Exactly(1).EqualTo(edit));
        
        Assert.That(t.ToTriangleInfoList()[2].Points, Has.Exactly(1).EqualTo(edit));
        Assert.That(t.ToTriangleInfoList()[2].Points, Has.Exactly(1).EqualTo(edit+Vector3.Up));

        Assert.That(t.ToTriangleInfoList()[0].edgeBorderFlags, Has.Exactly(2).EqualTo(true));
        Assert.That(t.ToTriangleInfoList()[1].edgeBorderFlags, Has.Exactly(1).EqualTo(true));
        Assert.That(t.ToTriangleInfoList()[2].edgeBorderFlags, Has.Exactly(2).EqualTo(true));


    }
}