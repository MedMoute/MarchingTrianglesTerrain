using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;

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
    public void TestCanCreateAddTriangleFanAction()
    {
        //Standard usage
        Assert.DoesNotThrow(() =>
        {
            var action = new SplitEdgeAction(0,0, 1,0.5f);
        });

        // ---Bad edge indexes
        var e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(-1,0, 1, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Edge index must be between 0 and 2."));
        
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(3,0, 1, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Edge index must be between 0 and 2."));
        
        // ---Bad sub-edge indexes
        
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(1,-1, 1, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Vertex indexes must be strictly positive."));
        
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(2,0, -2, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Vertex indexes must be strictly positive."));
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(2,0, 0, 0.5f);
        });
        Assert.That(e, Has.Message.Contains("Starting and ending vertex indexes must be different from one another."));
        
        // ---Bad weight
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(1,1, 2, -0.5f);
        });
        Assert.That(e, Has.Message.Contains("Weight must be between 0 and 1 (included)."));
        
        e = Assert.Throws<ArgumentException>(() =>
        {
            var action = new SplitEdgeAction(2,0, 2, 1.5f);
        });
        Assert.That(e, Has.Message.Contains("Weight must be between 0 and 1 (included)."));
    }

    [Test]
    public void TestCanApplySplitEdgeAction()
    {
        Assert.DoesNotThrow(() =>
        {
            var action = new SplitEdgeAction(0,0, 1,0.5f);
            action.Apply(t);
        });
    }
}