using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public static class GeometryModeActions
{
    public static void ProcessVertexOperationsForFlatHexes(
        VertexConformalGeometryEdit editor,
        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> appliedGeometryOperations,
        HexTerrainCell htCell,
        int vertexInHtCellIdx,
        int vertexInTriIdx,
        Triangulation tri,
        bool applyFans = true)
    {
        var pInit = htCell.VertexPositionsInPlane[vertexInHtCellIdx];
        var height = htCell.AverageHeight;

        editor.RegisterAction(new MovePointAlongYAxisAction(vertexInTriIdx, height), tri);

        if (applyFans)
        {
            // If the action target edge is a cell border (e.g. target.Item2 ==1)
            // We find the other cell and add a fan from the vertex to the vertex @ other cell's height 
            if (vertexInTriIdx == 1)
            {
                //To find the other matching edge, we fetch the other point of the triangulation that is not
                // the cell center
                Vector3 otherPos = tri.SourceTriangle.First(pos => !new Vector2(pos.X, pos.Z).IsEqualApprox(new Vector2(
                                                                       (float)htCell.CenterPosition.X,
                                                                       (float)htCell.CenterPosition.Y)) &&
                                                                   !new Vector2(pos.X, pos.Z).IsEqualApprox(new Vector2(
                                                                       (float)pInit.X,
                                                                       (float)pInit.Y)));
                //We find the other cell containing that point
                List<HexTerrainCell> otherCells = appliedGeometryOperations.Keys.Select(t =>
                    t.Item2).Where(oCell => oCell != htCell
                                            && oCell.VertexPositionsInPlane.Any(v =>
                                                v.Equals(new Vector2D(otherPos.X, otherPos.Z), 1e-5))).ToList();
                if (otherCells.Count > 1)
                {
                    throw new Exception("There should be no more another cell matching the predicate");
                }

                if (otherCells.Count < 1)
                {
                    // //TODO cross chunk fetch
                    // // Debug statement
                    // Console.WriteLine($"No other cell @X={pInit.X};Z={pInit.Y}");
                    return;
                }

                var otherCell = otherCells[0];
                var hOtherCell = otherCell.AverageHeight;
                editor.RegisterAction(
                    new AddTrianglesOnBorderEdge(1, new Vector3((float)pInit.X, hOtherCell, (float)pInit.Y)),
                    tri);
            }
        }
    }

    public static void ProcessVertexOperationsForFlatTriangles(
        VertexConformalGeometryEdit editor,
        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> appliedGeometryOperations,
        HexTerrainCell htCell,
        int vertexInHtCellIdx,
        int vertexInTriIdx,
        Triangulation tri,
        bool secondTriangulationFlag,
        bool applyFans = true)
    {
        var pInit = vertexInTriIdx == 0
            ? htCell.CenterPosition
            : //Cell center point, we cant rely on the cell
            htCell.VertexPositionsInPlane[vertexInHtCellIdx]; //Usual vertex

        if (htCell.GetEdgeAvgHeight == null)
        {
            return;
        }

        var height = secondTriangulationFlag
            ? htCell.GetEdgeAvgHeight(EngineUtils.Mod(vertexInHtCellIdx - 1, HexTerrainCell.VertexCount))
            : htCell.GetEdgeAvgHeight(vertexInHtCellIdx);

        editor.RegisterAction(new MovePointAlongYAxisAction(vertexInTriIdx, height), tri);
        // If the action target edge is a cell border (e.g. targetEdge ==1)
        // We find the other cell and add a fan from the vertex to the vertex @ other cell's height 
        if (applyFans)
        {
            if (vertexInTriIdx == 1)
            {
                //To find the other matching edge, we fetch the other point of the triangulation that is not
                // the cell center
                Vector3 otherPos = tri.SourceTriangle.First(pos =>
                    !new Vector2(pos.X, pos.Z).IsEqualApprox(
                        new Vector2((float)htCell.CenterPosition.X, (float)htCell.CenterPosition.Y)) &&
                    !new Vector2(pos.X, pos.Z).IsEqualApprox(
                        new Vector2((float)pInit.X, (float)pInit.Y)));
                //We find the other cell containing that point
                List<HexTerrainCell> otherCells = appliedGeometryOperations.Keys.Select(t =>
                    t.Item2).Where(oCell => oCell != htCell
                                            && oCell.VertexPositionsInPlane.Any(v =>
                                                v.Equals(new Vector2D(otherPos.X, otherPos.Z), 1e-5))).ToList();
                if (otherCells.Count > 1)
                {
                    throw new Exception("There should be no more another cell matching the predicate");
                }

                if (otherCells.Count < 1)
                {
                    return;
                }

                var otherCell = otherCells[0];
                // We fetch the index of the vertex in that cell
                int idx = otherCell.VertexPositionsInPlane.FindIndex(v => v.Equals(pInit, 1e-5));
                if (otherCell.GetEdgeAvgHeight != null)
                {
                    // ReSharper disable once UnusedVariable
                    var hOtherCell = otherCell.GetEdgeAvgHeight(idx);
                    // TODO : FIXME 
                    // RegisterAction(
                    //     new AddTrianglesOnBorderEdge(vertexInTriIdx,
                    //         new Vector3((float)pInit.X, hOtherCell, (float)pInit.Y)),
                    //     tri);
                }
            }
            else //The action is on an internal edge of the cell, we essentially do
                //the same thing except we don't need to find either the index
                //(0=> VertexInHtCellIdx-1; 2=>VertexInHtCellIdx-1), or  the cell
            {
                height = vertexInTriIdx == 0
                    ? htCell.GetEdgeAvgHeight(EngineUtils.Mod(vertexInHtCellIdx - 1, HexTerrainCell.VertexCount))
                    : htCell.GetEdgeAvgHeight(vertexInHtCellIdx);
                editor.RegisterAction(
                    new AddTrianglesOnBorderEdge(vertexInTriIdx, new Vector3((float)pInit.X, height, (float)pInit.Y)),
                    tri);
            }
        }
    }

    public static void ProcessVertexOperationsForBendingEdge(
        VertexConformalGeometryEdit editor,
        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> appliedGeometryOperations,
        (float, float) operationParameters,
        HexTerrainCell htCell,
        int vertexInHtCellIdx,
        int vertexInTriIdx,
        Triangulation tri,
        bool secondTriangulationOfCell, bool applyOnEdgeStart, bool applyOnEdgeEnd)
    {
        if (operationParameters.Item1 is < 0 or > 1 ||
            operationParameters.Item2 is < 0 or > 1)
        {
            throw new ArgumentException("operationParameters must be between 0 and 1.");
        }

        if (operationParameters.Item1 == 0 || operationParameters.Item2 == 0)
        {
            return;
        }

        var p1 = tri.Vertices[vertexInTriIdx];
        var p2 = tri.Vertices[EngineUtils.Mod(vertexInTriIdx + 1, 3)];

        var invert = secondTriangulationOfCell;
        var value = invert
            ? Mathf.Lerp(p2.Y, p1.Y, operationParameters.Item2)
            : Mathf.Lerp(p1.Y, p2.Y, operationParameters.Item2);
        var weight = invert ? operationParameters.Item1 : 1 - operationParameters.Item1;

        if (applyOnEdgeStart && vertexInTriIdx != 1)
        {
            editor.RegisterAction(new ComposedTriangularEditAction(triangulation =>
                {
                    var newPt = new SplitSubEdgeAction(
                            vertexInTriIdx,
                            vertexInTriIdx,
                            EngineUtils.Mod(vertexInTriIdx + 1, 3),
                            weight)
                        .Apply(triangulation);

                    new MovePointAlongYAxisAction(newPt, value).Apply(triangulation);

                    if (applyOnEdgeEnd)
                    {
                        if (weight < 0.5)
                        {
                            newPt = new SplitSubEdgeAction(
                                    vertexInTriIdx,
                                    newPt,
                                    EngineUtils.Mod(vertexInTriIdx + 1, 3),
                                    (1 - 2 * weight) / (1 - weight))
                                .Apply(triangulation);
                        }else
                        {
                            newPt = new SplitSubEdgeAction(
                                    vertexInTriIdx,
                                    vertexInTriIdx,
                                    newPt,
                                    (1-weight) /weight)
                                .Apply(triangulation);
                        }

                        new MovePointAlongYAxisAction(newPt, value).Apply(triangulation);
                    }

                    return newPt;
                }
            ), tri);
        }
        else if (applyOnEdgeEnd && vertexInTriIdx != 1)
        {
            float splitWeight = (1 - weight);
            editor.RegisterAction(new ComposedTriangularEditAction(triangulation =>
                {
                    var newPt = new SplitSubEdgeAction(
                            vertexInTriIdx,
                            vertexInTriIdx,
                            EngineUtils.Mod(vertexInTriIdx + 1, 3),
                            splitWeight)
                        .Apply(triangulation);

                    new MovePointAlongYAxisAction(newPt, value).Apply(triangulation);


                    return newPt;
                }
            ), tri);
        }
    }
}