using System;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public abstract class DelegatedTriangulationEditAction :
    TriangulationEditAction
{
    protected abstract Triangulation doApply(Triangulation t);

    public Triangulation Apply(Triangulation triangulation)
    {
        var t = doApply(triangulation);
        t.Debug(ToString());
        return t;
    }
}

public interface TriangulationEditAction
{
    public Triangulation Apply(Triangulation triangulation);
}