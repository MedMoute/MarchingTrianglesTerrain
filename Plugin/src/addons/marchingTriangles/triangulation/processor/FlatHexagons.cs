using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class FlatHexagons : GeometryModeActions.ModeProcessor
{
    public static readonly FlatHexagons Processor = new();


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
        var pInit = tri.SourceTriangle[edgeIdx];

        if (edgeIdx != 1) // Cell interior edge, nothing to add since all triangles are set at AvgHeight
        {
            return;
        }

        if (neighborCells.Count == 0) // No neighbor edge, the edge is on the terrain border
        {
            return;
        }

        var otherCell = neighborCells.First();
        var hOtherCell = otherCell.AverageHeight;
        var newPointPos = new Vector3(pInit.X, hOtherCell, pInit.Z);

        //Console.WriteLine($"[Hexagons] Add Tri [#{tri.Id};{edgeIdx}]=>@{newPointPos}");

        editor.RegisterAction(
            new AddTrianglesOnBorderEdge(edgeIdx, newPointPos),
            tri);
    }
}