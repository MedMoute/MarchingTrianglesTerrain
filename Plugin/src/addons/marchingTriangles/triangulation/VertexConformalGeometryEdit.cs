using System;
using System.Collections.Generic;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class VertexConformalGeometryEdit
{
    private readonly Dictionary<ITriangulationEditAction<int>, Triangulation> _actionToTriangulationHandle = new();

    private readonly List<ITriangulationEditAction<int>> _actions = new();

    public void RegisterAction(ITriangulationEditAction<int> action, Triangulation triangulation)
    {
        _actionToTriangulationHandle.Add(action, triangulation);
        _actions.Add(action);
    }

    public void ApplyTransformations()
    {
        foreach (var triangulationEditAction in _actions)
        {
            triangulationEditAction.Apply(_actionToTriangulationHandle[triangulationEditAction]);
        }
    }

    /// <summary>
    /// Registers the local vertex operations required to perform the GeometryMode's induced action upon application.
    /// </summary>
    public void RegisterLocalVertexAction(
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
            var vertexInHtCellIdx = EngineUtils.Mod(cell.Item1, HexTerrainCell.VertexCount);

            var tri = target.Item1;
            var vertexInTriIdx = target.Item2;
            RegisterVertexActionDependingOnGeometry(
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
                target = localTriangulations[cell].Item2!.Value;
                operationType = kvp.Value.Item2!.Value;

                tri = target.Item1;
                vertexInTriIdx = target.Item2;
                RegisterVertexActionDependingOnGeometry(
                    operationType,
                    htCell,
                    vertexInHtCellIdx,
                    vertexInTriIdx,
                    tri, true);
            }
        }


        void RegisterVertexActionDependingOnGeometry(
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
                    GeometryModeActions.ProcessVertexOperationsForFlatHexes(this, appliedGeometryOperations, htCell,
                        vertexInHtCellIdx,
                        vertexInTriIdx, tri);
                    break;
                case GeometryMode.FlatTriangles:
                    GeometryModeActions.ProcessVertexOperationsForFlatTriangles(this, appliedGeometryOperations, htCell,
                        vertexInHtCellIdx,
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
                    GeometryModeActions.ProcessVertexOperationsForFlatHexes(this, appliedGeometryOperations, htCell,
                        vertexInHtCellIdx,
                        vertexInTriIdx, tri, false);
                    break;
                case GeometryMode.FlatTrianglesNoFans:
                    GeometryModeActions.ProcessVertexOperationsForFlatTriangles(this, appliedGeometryOperations, htCell,
                        vertexInHtCellIdx,
                        vertexInTriIdx, tri, secondTriangulationOfCell, false);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

public class CopyOnlyGeometryEdit : VertexConformalGeometryEdit
{
    public CopyOnlyGeometryEdit()
    {
        throw new NotImplementedException();
    }

    public void RegisterAction(ITriangulationEditAction<int> action)
    {
        base.RegisterAction(action,null!);
    }
}