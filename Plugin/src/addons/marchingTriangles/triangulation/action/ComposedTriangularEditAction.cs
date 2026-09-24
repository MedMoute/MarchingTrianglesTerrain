using System;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

public class ComposedTriangularEditAction(Action< Triangulation> action) : TriangulationEditAction<int>
{
    
    public int Apply(Triangulation triangulation)
    {
        action(triangulation);
        return 0;
    }
}