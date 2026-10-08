using System;
using System.Collections.Generic;
using Godot;
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
        Dictionary<(int, Vector2I,Triangulation, int), (GeometryMode,(float,float))> appliedGeometryOperations,
        Dictionary<Vector2I, HexTerrainCell> neighborCells)
    {
        foreach (var kvp in appliedGeometryOperations)
        {
            var cellAndVertexIndexKey = kvp.Key;

            var target = kvp.Key;
            var parameters = appliedGeometryOperations[cellAndVertexIndexKey].Item2;
            var operationType = kvp.Value.Item1;

            var htCell = neighborCells[cellAndVertexIndexKey.Item2];
            var vertexInHtCellIdx = EngineUtils.Mod(cellAndVertexIndexKey.Item1, HexTerrainCell.VertexCount);

            var tri = target.Item3;
            var vertexInTriIdx = target.Item4;
            RegisterVertexActionDependingOnGeometry(neighborCells,
                operationType,
                parameters,
                htCell,
                vertexInHtCellIdx,
                vertexInTriIdx,
                tri,
                false);

        }


        void RegisterVertexActionDependingOnGeometry(
            Dictionary<Vector2I, HexTerrainCell> neighborCells,
            GeometryMode operationType,
            (float,float) operationParameters,
            HexTerrainCell htCell,
            int vertexInHtCellIdx,
            int vertexInTriIdx,
            Triangulation tri,
            bool secondTriangulationOfCell)
        {
            switch (operationType)
            {
                case GeometryMode.FlatHexagons:
                    GeometryModeActions.ProcessVertexOperationsForFlatHexes(this, neighborCells, htCell,
                        vertexInHtCellIdx,
                        vertexInTriIdx, tri);
                    break;
                case GeometryMode.FlatTriangles:
                    GeometryModeActions.ProcessVertexOperationsForFlatTriangles(this, htCell,
                        vertexInHtCellIdx,
                        vertexInTriIdx, tri, secondTriangulationOfCell);
                    break;
                case GeometryMode.SmoothLinear:
                    //NOOP
                    break;
                case GeometryMode.Foothill:
                    GeometryModeActions.ProcessVertexOperationsForBendingEdge(this, operationParameters, htCell,
                        vertexInHtCellIdx,
                        vertexInTriIdx, tri, secondTriangulationOfCell, false, true);
                    break;
                case GeometryMode.Plateau:
                    GeometryModeActions.ProcessVertexOperationsForBendingEdge(this,  operationParameters,htCell,
                        vertexInHtCellIdx,
                        vertexInTriIdx, tri, secondTriangulationOfCell, true, false);
                    break;
                case GeometryMode.BendingEdge:
                    GeometryModeActions.ProcessVertexOperationsForBendingEdge(this, operationParameters, htCell,
                        vertexInHtCellIdx,
                        vertexInTriIdx, tri, secondTriangulationOfCell, true, true);
                    break;
                //TODO
                //throw new NotImplementedException();
                case GeometryMode.FlatHexagonsNoFans:
                    GeometryModeActions.ProcessVertexOperationsForFlatHexes(this,neighborCells, htCell,
                        vertexInHtCellIdx,
                        vertexInTriIdx, tri, false);
                    break;
                case GeometryMode.FlatTrianglesNoFans:
                    GeometryModeActions.ProcessVertexOperationsForFlatTriangles(this,  htCell,
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
        base.RegisterAction(action, null!);
    }
}