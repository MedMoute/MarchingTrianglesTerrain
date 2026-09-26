using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class VertexConformalGeometryEdit
{
    private Dictionary<TriangulationEditAction<int>, Triangulation> actionToTriangulationHandle = new();

    private List<TriangulationEditAction<int>> _actions = new();

    public void RegisterAction(TriangulationEditAction<int> action, Triangulation triangulation)
    {
        actionToTriangulationHandle.Add(action, triangulation);
        _actions.Add(action);
    }

    public void ApplyTransformations()
    {
        foreach (var triangulationEditAction in _actions)
        {
            triangulationEditAction.Apply(actionToTriangulationHandle[triangulationEditAction]);
        }
    }

    /// <summary>
    /// Registers the local vertex operation required to perform the action upon application.
    /// </summary>
    public void RegisterLocalAction(
        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> appliedGeometryOperations,
        Dictionary<(int, HexTerrainCell), Tuple<(Triangulation, int), (Triangulation, int)?>> localTriangulations)
    {
        foreach (var kvp in appliedGeometryOperations)
        {
            var cell = kvp.Key;
            //First triangulation

            var target = localTriangulations[cell].Item1;
            var operationType = kvp.Value.Item1;

            var htCell = cell.Item2;
            var vertexInHtCellIdx = EngineUtils.mod(cell.Item1, HexTerrainCell.VertexCount);

            var tri = target.Item1;
            var vertexInTriIdx = target.Item2;
            RegisterActionDependingOnGeometry(
                operationType,
                htCell,
                vertexInHtCellIdx,
                vertexInTriIdx,
                tri,
                false);
            //Second triangulation, we reduce the index for the cell indexing
            //(Note that this is not used for cell center vertex processing)
            if (localTriangulations[cell].Item2.HasValue)
            {
                target = localTriangulations[cell].Item2.Value;
                operationType = kvp.Value.Item2.Value;

                tri = target.Item1;
                vertexInTriIdx = target.Item2;
                RegisterActionDependingOnGeometry(
                    operationType,
                    htCell,
                    vertexInHtCellIdx,
                    vertexInTriIdx,
                    tri, true);
            }
        }


        void RegisterActionDependingOnGeometry(
            GeometryMode operationType,
            HexTerrainCell htCell,
            int vertexInHtCellIdx,
            int vertexInTriIdx,
            Triangulation tri,
            bool secondTriangulationOfCell)
        {
            switch (operationType)
            {
                case GeometryMode.FlatHexagons:
                    ProcessVertexOperationsForFlatHexes(appliedGeometryOperations, htCell, vertexInHtCellIdx,
                        vertexInTriIdx, tri);
                    break;
                case GeometryMode.FlatTriangles:
                    ProcessVertexOperationsForFlatTriangles(appliedGeometryOperations, htCell, vertexInHtCellIdx,
                        vertexInTriIdx, tri, secondTriangulationOfCell);
                    break;
                case GeometryMode.SmoothLinear:
                    //NOOP
                    break;
                case GeometryMode.Foothill:
                //TODO
                //throw new NotImplementedException();
                case GeometryMode.Plateau:
                //TODO
                //throw new NotImplementedException();
                case GeometryMode.BendingEdge:
                //TODO
                //throw new NotImplementedException();
                case GeometryMode.FlatHexagonsNoFans:
                    ProcessVertexOperationsForFlatHexes(appliedGeometryOperations, htCell, vertexInHtCellIdx,
                        vertexInTriIdx, tri, false);
                    break;
                case GeometryMode.FlatTrianglesNoFans:
                    ProcessVertexOperationsForFlatTriangles(appliedGeometryOperations, htCell, vertexInHtCellIdx,
                        vertexInTriIdx, tri, secondTriangulationOfCell, false);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }

    private void ProcessVertexOperationsForFlatTriangles(
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

        RegisterAction(new MovePointAlongYAxisAction(vertexInTriIdx, height), tri);
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
                RegisterAction(
                    new AddTrianglesOnBorderEdge(vertexInTriIdx, new Vector3((float)pInit.X, height, (float)pInit.Y)),
                    tri);
            }
        }
    }

    private void ProcessVertexOperationsForFlatHexes(
        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> appliedGeometryOperations,
        HexTerrainCell htCell,
        int VertexInHtCellIdx,
        int VertexInTriIdx,
        Triangulation tri,
        bool ApplyFans = true)
    {
        var pInit = htCell.VertexPositionsInPlane[VertexInHtCellIdx];
        var height = htCell.AverageHeight;

        RegisterAction(new MovePointAlongYAxisAction(VertexInTriIdx, height), tri);

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
                RegisterAction(
                    new AddTrianglesOnBorderEdge(1, new Vector3((float)pInit.X, hOtherCell, (float)pInit.Y)),
                    tri);
            }
        }
    }
}

public class CopyOnlyGeometryEdit : VertexConformalGeometryEdit
{
    public CopyOnlyGeometryEdit(Vector3I coords, HexagonalTerrainChunk chunk)
    {
        throw new NotImplementedException();
    }

    public new void RegisterAction(TriangulationEditAction<int> action)
    {
        throw new NotSupportedException();
    }
}