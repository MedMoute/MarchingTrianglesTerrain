using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace UnitTests.test.triangulation;

internal class TestMultipleActions
{
    private static readonly Vector3 A = Vector3.Up;
    private static readonly Vector3 B = Vector3.Back;
    private static readonly Vector3 C = Vector3.Right;
    private readonly Vector3[] _tri = [A, B, C];

    private Triangulation _t;

    private static readonly Func<Triangulation, ITriangulationEditAction<int>> Displace0 = _ =>
        new DisplaceEdgeAlongYAxis(0, 30, 200);

    private static readonly Func<Triangulation, ITriangulationEditAction<int>> MovePoint0 = _ =>
        new MovePointAlongYAxisAction(0, 100);

    private static readonly Func<Triangulation, ITriangulationEditAction<int>> AddTriangle0 = t => new AddTriangleFan(
        (0, t.ToTriangleInfoList().Count == 1 ? 1 : 3),
        new Vector3(
            A.X,
            t.ToTriangleInfoList().Count == 1 ? 50 : 100,
            A.Z));

    private static readonly Func<Triangulation, ITriangulationEditAction<int>> SplitEdge0 = t => new SplitSubEdgeAction(
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


    internal class TestData
    {
        internal readonly Func<Triangulation, ITriangulationEditAction<int>> Action;
        private readonly string _className;

        internal TestData(Func<Triangulation, ITriangulationEditAction<int>> action, string className)
        {
            Action = action;
            _className = className;
        }

        public override string ToString()
        {
            return _className;
        }
    }

    protected static List<TestData> ActionList =
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
        _t = new Triangulation(_tri, false, true);
    }

    [Test]
    public void TestCanApplyAnyTypeOfActionOnSameEdge(
        [ValueSource(nameof(ActionList))] TestData action1,
        [ValueSource(nameof(ActionList))] TestData action2)
    {
        Assert.DoesNotThrow(() =>
        {
            action1.Action(_t).Apply(_t);
            action2.Action(_t).Apply(_t);
        });
    }
}