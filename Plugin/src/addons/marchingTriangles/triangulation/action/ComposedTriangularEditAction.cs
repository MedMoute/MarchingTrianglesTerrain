using System;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

public class ComposedTriangularEditAction(Func<Triangulation,int> action) : ITriangulationEditAction<int>
{
    
    public int Apply(Triangulation triangulation)
    {
        return action(triangulation);
    }
}