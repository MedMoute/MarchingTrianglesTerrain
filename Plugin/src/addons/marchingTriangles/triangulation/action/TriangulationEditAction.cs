using System;
using System.Linq;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public interface TriangulationEditAction
{
    public void Apply(Triangulation triangulation);
}

public abstract class DelegatedTriangulationEditAction : TriangulationEditAction
{
    protected abstract void DoApply(Triangulation t);

    protected virtual void ValidateBefore(Triangulation t){}

    protected virtual void ValidateAfter(Triangulation t)
    {
        t.EnsureIntegrity();
        
        t.Debug(ToString());
    }
    public void Apply(Triangulation triangulation)
    {
        ValidateBefore(triangulation);
        DoApply(triangulation);
        ValidateAfter(triangulation);
    }
}