using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public static class GeometryModeActions
{
    public static void ProcessVertexOperationsForFlatHexes(
        VertexConformalGeometryEdit editor,
        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> appliedGeometryOperations,
        HexTerrainCell htCell,
        int VertexInHtCellIdx,
        int VertexInTriIdx,
        Triangulation tri,
        bool ApplyFans = true)
    {
        var pInit = htCell.VertexPositionsInPlane[VertexInHtCellIdx];
        var height = htCell.AverageHeight;

        editor.RegisterAction(new MovePointAlongYAxisAction(VertexInTriIdx, height), tri);

        if (ApplyFans)
        {
            // If the action target edge is a cell border (e.g. target.Item2 ==1)
            // We find the other cell and add a fan from the vertex to the vertex @ other cell's height 
            if (VertexInTriIdx == 1)
            {
                //To find the other matching edge, we fetch the other point of the triangulation that is not
                // the cell center
                Vector3 otherPos = tri.SourceTriangle.Where(pos =>
                {
                    return !new Vector2(pos.X, pos.Z).IsEqualApprox(new Vector2((float)htCell.CenterPosition.X,
                               (float)htCell.CenterPosition.Y)) &&
                           !new Vector2(pos.X, pos.Z).IsEqualApprox(new Vector2((float)pInit.X,
                               (float)pInit.Y));
                }).First();
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

        var height = secondTriangulationFlag
            ? htCell.GetEdgeAvgHeight(EngineUtils.mod(vertexInHtCellIdx - 1, HexTerrainCell.VertexCount))
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
                    var hOtherCell = otherCell.GetEdgeAvgHeight(idx);
                    // TODO : FIXME 
                    // RegisterAction(
                    //     new AddTrianglesOnBorderEdge(vertexInTriIdx,
                    //         new Vector3((float)pInit.X, hOtherCell, (float)pInit.Y)),
                    //     tri);
                }
            }
            else //The action is on an internal edge of the cell, we essentially do
                //the same thing except we dont need to find either the index
                //(0=> VertexInHtCellIdx-1; 2=>VertexInHtCellIdx-1), or  the cell
            {
                height = vertexInTriIdx == 0
                    ? htCell.GetEdgeAvgHeight(EngineUtils.mod(vertexInHtCellIdx - 1, HexTerrainCell.VertexCount))
                    : htCell.GetEdgeAvgHeight(vertexInHtCellIdx);
                editor.RegisterAction(
                    new AddTrianglesOnBorderEdge(vertexInTriIdx, new Vector3((float)pInit.X, height, (float)pInit.Y)),
                    tri);
            }
        }
    }
}