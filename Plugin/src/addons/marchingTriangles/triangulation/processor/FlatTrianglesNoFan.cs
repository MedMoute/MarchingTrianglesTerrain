using System.Collections.Generic;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class FlatTrianglesNoFan : GeometryModeActions.ModeProcessor
{
    public static readonly FlatTrianglesNoFan Processor = new();

    private FlatTrianglesNoFan()
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
        FlatTriangles.Processor.Vertex(editor, cell, cellSummit, tri, vertexIdx, neighborCells, parameters);
    }

    public void Edge(VertexGeometryEditor editor,
        HexTerrainCell cell,
        (int start, int end) edge, 
        Triangulation tri,
        int edgeIdx,
        List<HexTerrainCell> neighborCells, 
        (float, float) parameters) //NOOP
    { }
}