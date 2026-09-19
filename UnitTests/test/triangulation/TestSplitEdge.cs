using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace UnitTests.triangulation;

public class TestSplitEdge
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
    public void TestCanCreateSplitEdgeAction()
    {
        //Standard usage
        Assert.DoesNotThrow(() =>
        {
            var action = new SplitEdgeAction(0, 0, 1, 0.5f);
        });

        // ---Bad edge indexes
        var e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(-1, 0, 1, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Edge index must be between 0 and 2."));

        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(3, 0, 1, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Edge index must be between 0 and 2."));

        // ---Bad sub-edge indexes

        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(1, -1, 1, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Vertex indexes must be strictly positive."));

        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(2, 0, -2, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Vertex indexes must be strictly positive."));
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(2, 0, 0, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Starting and ending vertex indexes must be different from one another."));

        // ---Bad weight
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(1, 1, 2, -0.5f);
        });
        Assert.That(e, Has.Message.Contains("Weight must strictly be between 0 and 1"));

        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(2, 0, 2, 1.5f);
        });
        Assert.That(e, Has.Message.Contains("Weight must strictly be between 0 and 1"));
        
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(2, 0, 2, 1f);
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
            var action = new SplitEdgeAction(0, 0, 1, 0.5f);
            output = action.Apply(t);
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.That(t.ToTriangleInfoList()[0].Points, Has.One.EqualTo(expectedSplitPoint));
        Assert.That(t.ToTriangleInfoList()[1].Points, Has.One.EqualTo(expectedSplitPoint));

        Assert.That(t.ToTriangleInfoList()[0].edgeBorderFlags, Has.Exactly(2).True);
        Assert.That(t.ToTriangleInfoList()[1].edgeBorderFlags, Has.Exactly(2).True);
    }

    [Test]
    public void TestCanApplySplitEdgeActionTwice()
    {
        int output;
        var expectedSplitPoint_0 = A.Lerp(B, 0.5f);
        var expectedSplitPoint_1 = expectedSplitPoint_0.Lerp(B, 0.5f);

        Assert.DoesNotThrow(() =>
        {
            var action = new SplitEdgeAction(0, 0, 1, 0.5f);
            output = action.Apply(t);
            action = new SplitEdgeAction(0, output, 1, 0.5f);
            output = action.Apply(t);
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(3));
        Assert.That(t.ToTriangleInfoList()[0].Points, Has.One.EqualTo(expectedSplitPoint_0));
        Assert.That(t.ToTriangleInfoList()[1].Points, Has.One.EqualTo(expectedSplitPoint_0));
        Assert.That(t.ToTriangleInfoList()[1].Points, Has.One.EqualTo(expectedSplitPoint_1));
        Assert.That(t.ToTriangleInfoList()[2].Points, Has.One.EqualTo(expectedSplitPoint_1));

        Assert.That(t.ToTriangleInfoList()[0].edgeBorderFlags, Has.Exactly(2).True);
        Assert.That(t.ToTriangleInfoList()[1].edgeBorderFlags, Has.One.True);
        Assert.That(t.ToTriangleInfoList()[2].edgeBorderFlags, Has.Exactly(2).True);
    }
    
    [Test]
    public void TestCanApplySplitEdgeOnTriangleWithFan()
    {
        var action = new AddTriangleOnBorderEdge(0, new Vector3(A.X, A.Y + 10, A.Z));
        action.Apply(t);

        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(2));
        Assert.DoesNotThrow(() =>
        {
            var action = new SplitEdgeAction(0, 0, 1, 0.5f);
            action.Apply(t);
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(4));
    }

    [Test]
    public void TestCanApplySplitEdgeOnAllEdges()
    {
        var expectedSplitPoint_0 = A.Lerp(B, 0.5f);
        var expectedSplitPoint_1 = B.Lerp(C, 0.5f);
        var expectedSplitPoint_2 = C.Lerp(A, 0.5f);

        int output;
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 3; i++)
            {
                var action = new SplitEdgeAction(i, i, EngineUtils.mod(i + 1, 3), 0.5f);
                output = action.Apply(t);
            }
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(4));

        for (int i = 0; i < t.ToTriangleInfoList().Count; i++)
        {
            //All triangles end up having the first split point as a vertex
            Assert.That(t.ToTriangleInfoList()[i].Points, Has.One.EqualTo(expectedSplitPoint_0));
            Assert.That(t.ToTriangleInfoList()[i].Points,
                Has.One.EqualTo(expectedSplitPoint_1).Or.EqualTo(expectedSplitPoint_2));
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
                var action = new SplitEdgeAction(i, i, EngineUtils.mod(i + 1, 3), 0.5f);
                output = action.Apply(t);
                action = new SplitEdgeAction(i, output, EngineUtils.mod(i + 1, 3), 0.5f);
                output = action.Apply(t);
            }
        });
        Assert.That(t.ToTriangleInfoList(), Has.Count.EqualTo(7));
    }

    //TODO : check operation on edge with invalid vertices fails properly
    [Test]
    public void TestCannotApplyActionWithInvalidParameters()
    {
        var e = Assert.Throws<InvalidOperationException>(() =>
        {
            var action = new SplitEdgeAction(0, 1, 3, 0.5f);
            action.Apply(t);
        });
        Assert.That(e, Has.Message.EqualTo("The sub edge to split does not exist."));
        
        e = Assert.Throws<InvalidOperationException>(() =>
        {
            var action = new SplitEdgeAction(0, 1, 2, 0.5f);
            action.Apply(t);
        });
        Assert.That(e, Has.Message.Contains("does not belong to the split Edge"));
    }
}