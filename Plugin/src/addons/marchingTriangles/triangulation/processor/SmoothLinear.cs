using System.Collections.Generic;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class SmoothLinear : GeometryModeActions.ModeProcessor
{
    public static readonly SmoothLinear Processor = new();

    private SmoothLinear()
    {
    }

    public void Vertex(VertexGeometryEditor editor,
        HexTerrainCell cell,
        int cellSummit,
        Triangulation tri,
        int vertexIdx,
        List<HexTerrainCell> neighborCells,
        (float, float) parameters)
    {
        //NOOP
    }

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