using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public static class GeometryModeActions
{
    /// <summary>
    /// Registers the operations resulting in flattening the cell contents.
    /// </summary>
    /// <param name="editor"></param>
    /// <param name="cell"></param>
    /// <param name="edge"></param>
    /// <param name="tri"></param>
    /// <param name="vertexIdx"></param>
    /// <param name="neighborCells"> neighbor cells of the edge that are NOT the processed cell</param>
    /// <param name="applyFans"></param>
    public static void ProcessVertexOperationsForFlatHexes(
        VertexGeometryEditor editor,
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int vertexIdx,
        List<HexTerrainCell> neighborCells,
        bool applyFans = true)
    {
        Debug.Assert(cell.GetVertexData is not null);
        //Check whether the start or the end of the edge matches the edited point in the triangulation
        var isStartPrimaryVertex = IsStartPrimaryVertex(cell, edge, tri, vertexIdx);

        if (!isStartPrimaryVertex) // If that is not the case, nothing to do here
        {
            return;
        }

        var height = cell.AverageHeight;
        var pInit = tri.SourceTriangle[vertexIdx];

        //Console.WriteLine($"Move T[#{tri.Id};{vertexIdx}]=>{height}");

        var action = new MovePointAlongYAxisAction(vertexIdx, height);
        editor.RegisterAction(action, tri);

        if (applyFans)
        {
            if (vertexIdx != 1) // Cell interior edge, nothing to add since all triangles are set at AvgHeight
            {
                return;
            }

            if (neighborCells.Count == 0) // No neighbor edge, the edge is on the terrain border
            {
                return;
            }

            var otherCell = neighborCells.First();
            var hOtherCell = otherCell.AverageHeight;
            editor.RegisterAction(
                new AddTrianglesOnBorderEdge(vertexIdx, new Vector3(pInit.X, hOtherCell, pInit.Z)),
                tri);
        }
    }


    /// <summary>
    /// Registers the operations an operation resulting in flattening the sub-cell contents.
    /// </summary>
    /// <param name="editor"></param>
    /// <param name="cell"></param>
    /// <param name="edge"></param>
    /// <param name="tri"></param>
    /// <param name="edgeIdx"></param>
    /// <param name="neighborCells"> neighbor cells of the edge that are NOT the processed cell</param>
    /// <param name="applyFans"></param>
    public static void ProcessVertexOperationsForFlatTriangles(
        VertexGeometryEditor editor,
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx,
        List<HexTerrainCell> neighborCells,
        bool applyFans = true)
    {
        Debug.Assert(cell.GetVertexData is not null);
        //Check whether the start or the end of the edge matches the edited point in the triangulation
        var isStartPrimaryVertex = IsStartPrimaryVertex(cell, edge, tri, edgeIdx);

        if (!isStartPrimaryVertex)
        {
            // If that is not the case, nothing to do here
            return;
        }

        var posIndex = edge.start == -1 ? edge.end : edge.start;
        //The subcell can be generally computed this way :
        var heightIndex = EngineUtils.Mod(posIndex - edgeIdx / 2, HexTerrainCell.VertexCount);
        //offset : when processing the vertex 2, we're on the second edge
        //so we apply a -1 offset to get the correct index
        var pInit = tri.SourceTriangle[edgeIdx];

        Debug.Assert(cell.GetEdgeAvgHeight != null);
        var height = cell.GetEdgeAvgHeight(heightIndex);
        //Console.WriteLine($"Move T[#{tri.Id};{edgeIdx}]=>{height}");

        var action = new MovePointAlongYAxisAction(edgeIdx, height);
        editor.RegisterAction(action, tri);

        if (applyFans)
        {
            if (edgeIdx != 1) //The action is on an internal edge of the cell, we essentially do
                //the same thing except we don't need to find either the index
                //(0=> VertexInHtCellIdx-1; 2=>VertexInHtCellIdx-1), or  the cell
            {
                height = edgeIdx == 0
                    ? cell.GetEdgeAvgHeight(EngineUtils.Mod(heightIndex - 1, HexTerrainCell.VertexCount))
                    : cell.GetEdgeAvgHeight(EngineUtils.Mod(heightIndex + 1, HexTerrainCell.VertexCount));
                
                //Console.WriteLine($"Add tri T[#{tri.Id};{edgeIdx}](pos {pInit})=>{height}");

                editor.RegisterAction(
                    new AddTrianglesOnBorderEdge(edgeIdx, new Vector3(pInit.X, height, pInit.Z)),
                    tri);
            }
            else
            {
                if (neighborCells.Count == 0) // No neighbor edge, the edge is on the terrain border
                {
                    return;
                }

                var otherCell = neighborCells.First();
                //The opposite cell along an edge should have the index of the edge +3 (opposite)
                heightIndex = EngineUtils.Mod(heightIndex + 3,HexTerrainCell.VertexCount);
                Debug.Assert(otherCell.GetEdgeAvgHeight != null);
                var hOtherCell = otherCell.GetEdgeAvgHeight(heightIndex);
                editor.RegisterAction(
                    new AddTrianglesOnBorderEdge(edgeIdx, new Vector3(pInit.X, hOtherCell, pInit.Z)),
                    tri);
            }
        }


    }

    public static void ProcessEdgeOperationsForBendingEdge(VertexGeometryEditor vertexGeometryEditor,
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx,
        (float, float) parameters, bool applyStartEdit, bool applyEndEdit)
    {
        
        Debug.Assert(cell.GetVertexData is not null);
        //Check whether the start or the end of the edge matches the edited point in the triangulation
        var isStartPrimaryVertex = IsStartPrimaryVertex(cell, edge, tri, edgeIdx);

        if (!isStartPrimaryVertex)
        {
            // If that is not the case, nothing to do here
            return;
        }
        if (parameters.Item1 is < 0 or > 1 ||
            parameters.Item2 is < 0 or > 1)
        {
            throw new ArgumentException("parameters must be between 0 and 1.");
        }

        if (parameters.Item1 == 0 || parameters.Item2 == 0)
        {
            return;
        }
        var p1 = tri.Vertices[edgeIdx];
        var p2 = tri.Vertices[EngineUtils.Mod(edgeIdx + 1, 3)];

        var invert = IsEdgeOrdered(cell, edge, tri, edgeIdx);
        var value = invert
            ? Mathf.Lerp(p2.Y, p1.Y, parameters.Item2)
            : Mathf.Lerp(p1.Y, p2.Y, parameters.Item2);
        var weight = invert ? parameters.Item1 : 1 - parameters.Item1;

        if (applyStartEdit && edgeIdx != 1)
        {
            vertexGeometryEditor.RegisterAction(new ComposedTriangularEditAction(triangulation =>
                {
                    var newPt = new SplitSubEdgeAction(
                            edgeIdx,
                            edgeIdx,
                            EngineUtils.Mod(edgeIdx + 1, 3),
                            weight)
                        .Apply(triangulation);

                    new MovePointAlongYAxisAction(newPt, value).Apply(triangulation);

                    if (applyEndEdit)
                    {
                        if (weight < 0.5)
                        {
                            newPt = new SplitSubEdgeAction(
                                    edgeIdx,
                                    newPt,
                                    EngineUtils.Mod(edgeIdx + 1, 3),
                                    (1 - 2 * weight) / (1 - weight))
                                .Apply(triangulation);
                        }
                        else
                        {
                            newPt = new SplitSubEdgeAction(
                                    edgeIdx,
                                    edgeIdx,
                                    newPt,
                                    (1 - weight) / weight)
                                .Apply(triangulation);
                        }

                        new MovePointAlongYAxisAction(newPt, value).Apply(triangulation);
                    }

                    return newPt;
                }
            ), tri);
        }
        else if (applyEndEdit && edgeIdx != 1)
        {
            float splitWeight = (1 - weight);
            vertexGeometryEditor.RegisterAction(new ComposedTriangularEditAction(triangulation =>
                {
                    var newPt = new SplitSubEdgeAction(
                            edgeIdx,
                            edgeIdx,
                            EngineUtils.Mod(edgeIdx + 1, 3),
                            splitWeight)
                        .Apply(triangulation);

                    new MovePointAlongYAxisAction(newPt, value).Apply(triangulation);


                    return newPt;
                }
            ), tri);
        }
    }

    private static bool IsStartPrimaryVertex(
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx)
    {
        Debug.Assert(cell.GetVertexData != null);
        var isStartPrimaryVertex = tri.SourceTriangle[edgeIdx].IsEqualApprox(edge.start == -1
            ? new Vector3((float)cell.CenterPosition.X, cell.AverageHeight, (float)cell.CenterPosition.Y)
            : new Vector3(
                (float)cell.VertexPositionsInPlane[edge.start].X,
                cell.GetVertexData(edge.start),
                (float)cell.VertexPositionsInPlane[edge.start].Y));
        return isStartPrimaryVertex;
    }

    private static bool IsEdgeOrdered(
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx)
    {
        Debug.Assert(cell.GetVertexData != null);
        var isEdgeOrdered = tri.SourceTriangle[(edgeIdx + 1) % 3].IsEqualApprox(edge.end == -1
            ? new Vector3((float)cell.CenterPosition.X, cell.AverageHeight, (float)cell.CenterPosition.Y)
            : new Vector3(
                (float)cell.VertexPositionsInPlane[edge.end].X,
                cell.GetVertexData(edge.end),
                (float)cell.VertexPositionsInPlane[edge.end].Y));
        return isEdgeOrdered;
    }
}