using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
// ReSharper disable UnusedVariable
// ReSharper disable NotAccessedVariable

namespace UnitTests.test.triangulation;

public class TestSplitEdge
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
    public void TestCanCreateSplitEdgeAction()
    {
        //Standard usage
        Assert.DoesNotThrow(() =>
        {
            var action = new SplitSubEdgeAction(0, 0, 1, 0.5f);
        });

        // ---Bad edge indexes
        var e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitSubEdgeAction(-1, 0, 1, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Edge index must be between 0 and 2."));

        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitSubEdgeAction(3, 0, 1, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Edge index must be between 0 and 2."));

        // ---Bad sub-edge indexes

        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitSubEdgeAction(1, -1, 1, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Vertex indexes must be strictly positive."));

        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitSubEdgeAction(2, 0, -2, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Vertex indexes must be strictly positive."));
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitSubEdgeAction(2, 0, 0, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Starting and ending vertex indexes must be different from one another."));

        // ---Bad weight
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitSubEdgeAction(1, 1, 2, -0.5f);
        });
        Assert.That(e, Has.Message.Contains("Weight must strictly be between 0 and 1"));

        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitSubEdgeAction(2, 0, 2, 1.5f);
        });
        Assert.That(e, Has.Message.Contains("Weight must strictly be between 0 and 1"));
        
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitSubEdgeAction(2, 0, 2, 1f);
        });
        Assert.That(e, Has.Message.Contains("Weight must strictly be between 0 and 1"));
    }

    [Test]
    public void TestCanApplySplitEdgeAction()
    {
        int output;
        var expectedSplitPoint = A.Lerp(B, 0.5f);
        Assert.DoesNotThrow(() =>
        {
            var action = new SplitSubEdgeAction(0, 0, 1, 0.5f);
            output = action.Apply(_t);
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.That(_t.ToTriangleInfoList()[0].Points, Has.One.EqualTo(expectedSplitPoint));
        Assert.That(_t.ToTriangleInfoList()[1].Points, Has.One.EqualTo(expectedSplitPoint));

        Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.Exactly(2).True);
        Assert.That(_t.ToTriangleInfoList()[1].EdgeBorderFlags, Has.Exactly(2).True);
    }

    [Test]
    public void TestCanApplySplitEdgeActionTwice()
    {
        int output;
        var expectedSplitPoint0 = A.Lerp(B, 0.5f);
        var expectedSplitPoint1 = expectedSplitPoint0.Lerp(B, 0.5f);

        Assert.DoesNotThrow(() =>
        {
            var action = new SplitSubEdgeAction(0, 0, 1, 0.5f);
            output = action.Apply(_t);
            action = new SplitSubEdgeAction(0, output, 1, 0.5f);
            output = action.Apply(_t);
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(3));
        Assert.That(_t.ToTriangleInfoList()[0].Points, Has.One.EqualTo(expectedSplitPoint0));
        Assert.That(_t.ToTriangleInfoList()[1].Points, Has.One.EqualTo(expectedSplitPoint0));
        Assert.That(_t.ToTriangleInfoList()[1].Points, Has.One.EqualTo(expectedSplitPoint1));
        Assert.That(_t.ToTriangleInfoList()[2].Points, Has.One.EqualTo(expectedSplitPoint1));

        Assert.That(_t.ToTriangleInfoList()[0].EdgeBorderFlags, Has.Exactly(2).True);
        Assert.That(_t.ToTriangleInfoList()[1].EdgeBorderFlags, Has.One.True);
        Assert.That(_t.ToTriangleInfoList()[2].EdgeBorderFlags, Has.Exactly(2).True);
    }
    
    [Test]
    public void TestCanApplySplitEdgeOnTriangleWithFan()
    {
        var action = new AddTrianglesOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
        action.Apply(_t);

        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.DoesNotThrow(() =>
        {
            var action2 = new SplitSubEdgeAction(0, 0, 3, 0.5f);
            action2.Apply(_t);
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(3));
    }

    [Test]
    public void TestCanApplySplitEdgeOnAllEdges()
    {
        var expectedSplitPoint0 = A.Lerp(B, 0.5f);
        var expectedSplitPoint1 = B.Lerp(C, 0.5f);
        var expectedSplitPoint2 = C.Lerp(A, 0.5f);

        int output;
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 3; i++)
            {
                var action = new SplitSubEdgeAction(i, i, EngineUtils.Mod(i + 1, 3), 0.5f);
                output = action.Apply(_t);
            }
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(4));

        for (int i = 0; i < _t.ToTriangleInfoList().Count; i++)
        {
            //All triangles end up having the first split point as a vertex
            Assert.That(_t.ToTriangleInfoList()[i].Points, Has.One.EqualTo(expectedSplitPoint0));
            Assert.That(_t.ToTriangleInfoList()[i].Points,
                Has.One.EqualTo(expectedSplitPoint1).Or.EqualTo(expectedSplitPoint2));
        }
    }

    [Test]
    public void TestCanApplySplitEdgeOnAllEdgesTwice()
    {
        int output;
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 3; i++)
            {
                var action = new SplitSubEdgeAction(i, i, EngineUtils.Mod(i + 1, 3), 0.5f);
                output = action.Apply(_t);
                action = new SplitSubEdgeAction(i, output, EngineUtils.Mod(i + 1, 3), 0.5f);
                output = action.Apply(_t);
            }
        });
        Assert.That(_t.ToTriangleInfoList(), Has.Count.EqualTo(7));
    }

    //TODO : check operation on edge with invalid vertices fails properly
    [Test]
    public void TestCannotApplyActionWithInvalidParameters()
    {
        var e = Assert.Throws<InvalidOperationException>(() =>
        {
            var action = new SplitSubEdgeAction(0, 1, 3, 0.5f);
            action.Apply(_t);
        });
        Assert.That(e, Has.Message.EqualTo("The sub edge to split does not exist."));
        
        e = Assert.Throws<InvalidOperationException>(() =>
        {
            var action = new SplitSubEdgeAction(0, 1, 2, 0.5f);
            action.Apply(_t);
        });
        Assert.That(e, Has.Message.Contains("does not belong to the split Edge"));
    }
}