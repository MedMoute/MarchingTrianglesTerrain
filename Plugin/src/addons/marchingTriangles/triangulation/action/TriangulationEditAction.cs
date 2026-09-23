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
        T output;
        try
        {
            ValidateBefore(triangulation);
        }
        catch (Exception e)
        {
            triangulation.Debug("Exception Caught during pre Action Validation: " + e.Message, true);
            throw;
        }

        try
        {
            output = DoApply(triangulation);
        }
        catch (Exception e)
        {
            triangulation.Debug("Exception Caught during doApply: " + e.Message,true);
            throw;
        }

        ValidateAfter(triangulation);
        return output;
    }
}