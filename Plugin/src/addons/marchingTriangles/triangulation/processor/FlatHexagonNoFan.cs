using System.Collections.Generic;
using System.Diagnostics;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class FlatHexagonNoFan : GeometryModeActions.ModeProcessor
{
    public static readonly FlatHexagonNoFan Processor = new();

    public void Edge(VertexGeometryEditor editor,
        HexTerrainCell cell,
        (int start, int end) edge, 
        Triangulation tri,
        int edgeIdx,
        List<HexTerrainCell> neighborCells, 
        (float, float) parameters)
    {
        Debug.Assert(cell.GetVertexData is not null);

        var height = cell.AverageHeight;
        var action = new MovePointAlongYAxisAction(edgeIdx, height);

        editor.RegisterAction(action, tri);
        var pInit = tri.SourceTriangle[edgeIdx];    }
}