using System;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// Abstraction of an application that follows these criterion:
/// <ul><li>
///  The sum of the local applications of the modification must also be a space homeomorphic to a disk
/// (e.g. the modifications of a list of triangles will also be a connected bounded surface provided
/// that the triangles were initially neighbors)</li>
/// <li> Transforms any Triangulation into a Triangulation</li>
/// </ul>
/// </summary>
/// <typeparam name="T"></typeparam>
public interface TriangulationEditAction<out T>
{
    public T Apply(Triangulation triangulation);
}

public abstract class DelegatedTriangulationEditAction<T> : TriangulationEditAction<T>
{
    protected abstract T DoApply(Triangulation t);

    protected virtual void ValidateBefore(Triangulation t)
    {
    }

    protected virtual void ValidateAfter(Triangulation t)
    {
        t.EnsureIntegrity();
        t.Debug(ToString());
    }

    public T Apply(Triangulation triangulation)
    {
        T output;
        // try
        // {
            ValidateBefore(triangulation);
        // }
        // catch (Exception e)
        // {
        //     triangulation.Debug("Exception Caught during pre Action Validation: " + e.Message, true);
        //     throw;
        // }

        try
        {
            output = DoApply(triangulation);
        }
        catch (Exception e)
        {
            triangulation.Debug("Exception Caught during doApply: " + e.Message, true);
            throw;
        }

        ValidateAfter(triangulation);
        return output;
    }
}