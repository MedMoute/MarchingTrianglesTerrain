using System;
using System.Linq;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public interface TriangulationEditAction<out T>
{
    public T Apply(Triangulation triangulation);
}

public abstract class DelegatedTriangulationEditAction<T> : TriangulationEditAction<T>
{
    protected abstract T DoApply(Triangulation t);

    protected virtual void ValidateBefore(Triangulation t){}

    protected virtual void ValidateAfter(Triangulation t)
    {
        t.EnsureIntegrity();
        t.Debug(ToString());
    }
    public T Apply(Triangulation triangulation)
    {
        ValidateBefore(triangulation);
        T output = DoApply(triangulation);
        ValidateAfter(triangulation);
        return output;
    }
}