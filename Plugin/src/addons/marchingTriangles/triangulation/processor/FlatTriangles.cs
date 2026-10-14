using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class FlatTriangles : GeometryModeActions.ModeProcessor
{
    public static readonly FlatTriangles Processor = new();

    private FlatTriangles()
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
            GeometryModeActions.IsEdgeOrdered(cell, edge, tri, edgeIdx) ? edge.start :
            edge.end;
        //The subcell can be generally computed this way :
        var heightIndex = EngineUtils.Mod(posIndex - edgeIdx / 2, HexTerrainCell.VertexCount);
        //offset : when processing the vertex 2, we're on the second edge
        //so we apply a -1 offset to get the correct index
        var pInit = tri.SourceTriangle[edgeIdx];

        Debug.Assert(cell.GetEdgeAvgHeight != null);
        var height = cell.GetEdgeAvgHeight(heightIndex);
        Console.WriteLine($"[Triangles] Move T[#{tri.Id};{edgeIdx}]=>{height}");

        var action = new MovePointAlongYAxisAction(edgeIdx, height);
        editor.RegisterAction(action, tri);

        if (edgeIdx != 1) //The action is on an internal edge of the cell, we essentially do
            //the same thing except we don't need to find either the index
            //(0=> VertexInHtCellIdx-1; 2=>VertexInHtCellIdx-1), or  the cell
        {
            height = edgeIdx == 0
                ? cell.GetEdgeAvgHeight(EngineUtils.Mod(heightIndex - 1, HexTerrainCell.VertexCount))
                : cell.GetEdgeAvgHeight(EngineUtils.Mod(heightIndex + 1, HexTerrainCell.VertexCount));

            var newPointPos = new Vector3(pInit.X, height, pInit.Z);
            editor.RegisterAction(
                new AddTrianglesOnBorderEdge(edgeIdx, newPointPos),
                tri);
            //Console.WriteLine($"[Triangles] Add Tri [#{tri.Id};{edgeIdx}]=>@{newPointPos}");
        }
        else // External edge : same behavior as FlatHexagons 
        {
            if (neighborCells.Count != 0)
            {
                var otherCell = neighborCells.First();
                var hOtherCell =
                    otherCell.GetEdgeAvgHeight!(EngineUtils.Mod(heightIndex + 3, HexTerrainCell.VertexCount));
                
                var newPointPos = new Vector3(pInit.X, hOtherCell, pInit.Z);

                //Console.WriteLine($"[Triangles] Add Tri [#{tri.Id};{edgeIdx}]=>@{newPointPos}");

                editor.RegisterAction(
                    new AddTrianglesOnBorderEdge(edgeIdx, newPointPos),
                    tri);
            }
        }
    }
}