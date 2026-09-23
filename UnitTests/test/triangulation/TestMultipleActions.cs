using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
using NUnit.Framework.Internal;

namespace UnitTests.triangulation;

internal class TestMultipleActions
{
    private static Vector3 A = Vector3.Up;
    private static Vector3 B = Vector3.Back;
    private static Vector3 C = Vector3.Right;
    private Vector3[] tri = [A, B, C];

    private Triangulation t;

    private static readonly Func<Triangulation, TriangulationEditAction<int>> Displace0 = _ =>
        new DisplaceEdgeAlongYAxis(0, 30, 200);

    private static readonly Func<Triangulation, TriangulationEditAction<int>> MovePoint0 = _ =>
        new MovePointAlongYAxisAction(0, 100);

    private static readonly Func<Triangulation, TriangulationEditAction<int>> AddTriangle0 = t => new AddTriangleFan(
        (0, t.ToTriangleInfoList().Count == 1 ? 1 : 3),
        new Vector3(
            A.X,
            t.ToTriangleInfoList().Count == 1 ? 50 : 100,
            A.Z));

    private static readonly Func<Triangulation, TriangulationEditAction<int>> SplitEdge0 = t => new SplitSubEdgeAction(
        0,
        0,
        //Pick the correct endpoint, if a triangle fan was added, the sub edge is still (0,1),
        //but not if it was split (sub-edge will be (0,3))
        t.ToTriangleInfoList().Count == 1 ? 1 :
        (t
            .ToTriangleInfoList()
            .SelectMany(tInfo => tInfo.Points)
            .Count(p => !p.IsEqualApprox(A) && !p.IsEqualApprox(B) && !p.IsEqualApprox(C)) == 1) ? 1 : 3
        , 0.5f);

    private static readonly Func<Triangulation, TriangulationEditAction<int>> AddTriOnEdge0 = t =>
        new AddTrianglesOnBorderEdge(
            0,
            new Vector3(
                A.X,
                t.ToTriangleInfoList().Count == 1 ? 5 : 8,
                A.Z));


    internal class TestData
    {
        internal readonly Func<Triangulation, TriangulationEditAction<int>> _action;
        private readonly string _className;

        internal TestData(Func<Triangulation, TriangulationEditAction<int>> action, string className)
        {
            _action = action;
            _className = className;
        }

        public override string ToString()
        {
            return _className;
        }
    }

    protected static List<TestData> _actionList =
    [
        new(Displace0, nameof(DisplaceEdgeAlongYAxis)),
        new(MovePoint0, nameof(MovePointAlongYAxisAction)),
        new(AddTriangle0, nameof(AddTriangleFan)),
        new(SplitEdge0, nameof(SplitSubEdgeAction))
        // TODO  Fix this
        /*,
        new(AddTriOnEdge0, nameof(AddTrianglesOnBorderEdge))*/
    ];

    [SetUp]
    public void Setup()
    {
        t = new Triangulation([A, B, C], true, true);
    }

    [Test]
    public void TestCanApplyAnyTypeOfActionOnSameEdge(
        [ValueSource(nameof(_actionList))] TestData action1,
        [ValueSource(nameof(_actionList))] TestData action2)
    {
        Assert.DoesNotThrow(() =>
        {
            action1._action(t).Apply(t);
            action2._action(t).Apply(t);
        });
    }
}