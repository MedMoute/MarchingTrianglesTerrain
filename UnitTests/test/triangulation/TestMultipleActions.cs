using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace UnitTests.triangulation;

public class TestMultipleActions
{
    private static Vector3 A = Vector3.Up;
    private static Vector3 B = Vector3.Back;
    private static Vector3 C = Vector3.Right;
    private Vector3[] tri = [A, B, C];

    private Triangulation t;

    private static readonly Func<Triangulation,TriangulationEditAction<int>> Displace0 = _=>new DisplaceEdgeAlongYAxis(0, 10, 20);
    private static readonly Func<Triangulation,TriangulationEditAction<int>> MovePoint0 = _=>new MovePointAlongYAxisAction(0, 100);
    private static readonly Func<Triangulation,TriangulationEditAction<int>> AddTriangle0 = t => new AddTriangleFan(
        (0,t.ToTriangleInfoList().Count==1?1:3), 
        new Vector3(
            A.X,
            t.ToTriangleInfoList().Count==1?50:100,
            A.Z));
    private static readonly Func<Triangulation,TriangulationEditAction<int>> SplitEdge0 = t=>new SplitEdgeAction(
        0,
        0,
        //Pick the correct endpoint, if a triangle fan was added, the sub edge is still (0,1),
        //but not if it was split (sub-edge will be (0,3))
        t.ToTriangleInfoList().Count==1 ?1 :( t
            .ToTriangleInfoList()
            .SelectMany(tInfo => tInfo.Points)
            .Count(p => !p.IsEqualApprox(A) && !p.IsEqualApprox(B) && !p.IsEqualApprox(C)) == 1) ? 1:3
        , 0.5f);


    private static List<Func<Triangulation,TriangulationEditAction<int>>> _actions = [Displace0,MovePoint0,AddTriangle0,SplitEdge0];
    
    [SetUp]
    public void Setup()
    {
        t = new Triangulation([A, B, C], true, true);
    }

    [Test]
    public void TestCanApplyAnyTypeOfActionOnSameEdge(
        [ValueSource(nameof(_actions))]Func<Triangulation,TriangulationEditAction<int>> action1, 
        [ValueSource(nameof(_actions))]Func<Triangulation,TriangulationEditAction<int>> action2)
    {
        Assert.DoesNotThrow(() =>
        {
            action1(t).Apply(t);
            action2(t).Apply(t);
        });
    }
}