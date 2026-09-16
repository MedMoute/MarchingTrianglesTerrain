using System;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public abstract class DelegatedTriangulationEditAction :
    TriangulationEditAction
{
    protected abstract Func<Triangulation, Triangulation> DelegateAction { get; }

    public Triangulation Apply(Triangulation triangulation)
    {
        var t = DelegateAction.Invoke(triangulation);
        t.Debug(ToString());
        return t;
    }
}

public interface TriangulationEditAction
{
    public Triangulation Apply(Triangulation triangulation);
}