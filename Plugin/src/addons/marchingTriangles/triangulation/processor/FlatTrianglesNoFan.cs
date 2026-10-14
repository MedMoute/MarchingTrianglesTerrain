using System;
using System.Collections.Generic;
using System.Diagnostics;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

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
        var posIndex = edge.start == -1 ? edge.end : 
            GeometryModeActions.IsEdgeOrdered(cell, edge, tri, edgeIdx) ?
                edge.start : 
                edge.end;
        //The subcell can be generally computed this way :
        var heightIndex = EngineUtils.Mod(posIndex - edgeIdx / 2, HexTerrainCell.VertexCount);
        //offset : when processing the vertex 2, we're on the second edge
        //so we apply a -1 offset to get the correct index
        var pInit = tri.SourceTriangle[edgeIdx];

        Debug.Assert(cell.GetEdgeAvgHeight != null);
        var height = cell.GetEdgeAvgHeight(heightIndex);
        Console.WriteLine($"Move T[#{tri.Id};{edgeIdx}]=>{height}");

        var action = new MovePointAlongYAxisAction(edgeIdx, height);
        editor.RegisterAction(action, tri);    }
}