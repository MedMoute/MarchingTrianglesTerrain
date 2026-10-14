using System.Collections.Generic;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class BendingEdge : GeometryModeActions.ModeProcessor
{
    public static readonly BendingEdge Processor = new();

    private BendingEdge()
    {
    }

    public void Vertex(VertexGeometryEditor editor,
        HexTerrainCell cell,
        int cellSummit,
        Triangulation tri,
        int vertexIdx,
        List<HexTerrainCell> neighborCells,
        (float, float) parameters) //NOOP
    {
    }

    public void Edge(VertexGeometryEditor editor,
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx,
        List<HexTerrainCell> neighborCells,
        (float, float) parameters)
    {
        Plateau.Processor.Edge(editor, cell, edge, tri,edgeIdx,  neighborCells, parameters);
        Foothill.Processor.Edge(editor, cell, edge, tri,edgeIdx,  neighborCells, parameters);
    }
}