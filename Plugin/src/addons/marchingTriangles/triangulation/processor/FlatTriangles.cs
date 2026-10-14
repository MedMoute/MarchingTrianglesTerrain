using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class FlatTriangles : GeometryModeActions.ModeProcessor
{
    public static readonly FlatTriangles Processor = new();

    private FlatTriangles()
    { }

    public void Vertex(VertexGeometryEditor editor,
        HexTerrainCell cell, 
        int cellSummit,
        Triangulation tri,
        int vertexIdx,
        List<HexTerrainCell> neighborCells, 
        (float, float) parameters)
    {

        var posIndex = cellSummit ;
        //The subcell can be generally computed this way :
        var heightIndex = EngineUtils.Mod(posIndex - vertexIdx / 2, HexTerrainCell.VertexCount);
        //offset : when processing the vertex 2, we're on the second edge
        //so we apply a -1 offset to get the correct index
        
        var height = cell.GetEdgeAvgHeight!(heightIndex);
        var action = new MovePointAlongYAxisAction(vertexIdx, height);
        editor.RegisterAction(action, tri);
        //Console.WriteLine($"Move T[#{tri.Id};{edgeIdx}]=>{height}");

    }

    public void Edge(VertexGeometryEditor editor,
        HexTerrainCell cell,
        (int start, int end) edge, 
        Triangulation tri,
        int edgeIdx,
        List<HexTerrainCell> neighborCells, 
        (float, float) parameters)
    {
        if (edgeIdx != 1) //The action is on an internal edge of the cell, we essentially do
            //the same thing except we don't need to find either the index
            //(0=> VertexInHtCellIdx-1; 2=>VertexInHtCellIdx-1), or  the cell
        {        
            var pInit = tri.SourceTriangle[edgeIdx];
            var posIndex = edge.start ;
            var heightIndex = EngineUtils.Mod(posIndex - edgeIdx / 2, HexTerrainCell.VertexCount);
            var height = edgeIdx == 0
                ? cell.GetEdgeAvgHeight!(EngineUtils.Mod(heightIndex - 1, HexTerrainCell.VertexCount))
                : cell.GetEdgeAvgHeight!(EngineUtils.Mod(heightIndex + 1, HexTerrainCell.VertexCount));
            
            editor.RegisterAction(
                new AddTrianglesOnBorderEdge(edgeIdx, new Vector3(pInit.X, height, pInit.Z)),
                tri);
        }
        else // External edge : same behaviour as FlatHexagons 
        {
            FlatHexagons.Processor.Edge(editor, cell, edge, tri, edgeIdx, neighborCells, parameters);
        }
    }
}