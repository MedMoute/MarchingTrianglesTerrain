using System.Collections.Generic;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class FlatHexagonNoFan : GeometryModeActions.ModeProcessor
{
    public static readonly FlatHexagonNoFan Processor = new();

    private FlatHexagonNoFan()
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
        FlatHexagons.Processor.Vertex(editor, cell, cellSummit, tri, vertexIdx, neighborCells, parameters);
    }

    public void Edge(VertexGeometryEditor editor,
        HexTerrainCell cell,
        (int start, int end) edge, 
        Triangulation tri,
        int edgeIdx,
        List<HexTerrainCell> neighborCells, 
        (float, float) parameters)
    {
        //no action on edges 
    }
}