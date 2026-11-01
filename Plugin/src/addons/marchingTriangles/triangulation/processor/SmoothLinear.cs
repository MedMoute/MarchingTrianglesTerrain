using System.Collections.Generic;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class SmoothLinear : GeometryModeActions.ModeProcessor
{
    public static readonly SmoothLinear Processor = new();


    public void Edge(VertexGeometryEditor editor,
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx,
        List<HexTerrainCell> neighborCells,
        (float, float) parameters)
    {
        //NOOP
    }
}