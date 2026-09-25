using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class VertexConformalGeometryEdit
{
    private Dictionary<TriangulationEditAction<int>, Triangulation> actionToTriangulationHandle = new();

    private List<TriangulationEditAction<int>> _actions = new();

    public void RegisterVertexAction(TriangulationEditAction<int> action)
    {
        _actions.Add(action);
    }

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
    /// <param name="cell">the cell being edited and the vertex being moved</param>
    /// <param name="target">the triangulation upon which the operation will occur</param>
    /// <param name="operationType">the type of operation</param>
    /// <exception cref="NotImplementedException"></exception>
    public void RegisterLocalAction((int, HexTerrainCell) cell, (Triangulation, int) target, GeometryMode operationType)
    {
        switch (operationType)
        {
            case GeometryMode.FlatHexagons:
                var pInit = cell.Item2.VertexPositionsInPlane[cell.Item1];
                var heightInit = cell.Item2.GetVertexData(cell.Item1);
                var height = cell.Item2.AverageHeight;
                var pos = new Vector3((float)pInit.X, heightInit, (float)pInit.Y);
                Console.WriteLine($"Registering FlatHexagon move for cell vertex " +
                                  $"{cell.Item2.CellCoordsImplicit},{cell.Item1}," +
                                  $" editing Triangulation{target.Item1.GetHashCode()}" +
                                  $" on edge {target.Item2}");
                RegisterAction(new MovePointAlongYAxisAction(target.Item2, height), target.Item1);
                // If the action target edge is a cell border (e.g. target.Item2 ==1)
                // We find the other cell and add a fan from the vertex to the vertex @ other cell's height 
                if (target.Item2 == 1)
                {
                    //To find the other matching edge, we fetch the other point of the triangulation that is not
                    // the 
                }

                break;
            case GeometryMode.FlatTriangles:
                Console.WriteLine(
                    $"Registering FlatTriangle move for cell vertex {cell.Item2.CellCoordsImplicit},{cell.Item1}, editing Triangulation on edge {target.Item2}");
                break;
            case GeometryMode.SmoothLinear:
                break;
            case GeometryMode.Foothill:
                throw new NotImplementedException();
            case GeometryMode.Plateau:
                throw new NotImplementedException();
            case GeometryMode.BendingEdge:
                throw new NotImplementedException();
        }
    }

    public void RegisterLocalAction(
        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode>> appliedGeometryOperations,
        Dictionary<(int, HexTerrainCell), Tuple<(Triangulation, int), (Triangulation, int)>> localTriangulations)
    {
        foreach (var kvp in appliedGeometryOperations)
        {
            //First triangulation

            var cell = kvp.Key;
            var target = localTriangulations[cell].Item1;
            var operationType = kvp.Value.Item1;

            var htCell = cell.Item2;
            var VertexInHtCellIdx = cell.Item1;

            var tri = target.Item1;
            var VertexInTriIdx = target.Item2;
            RegisterActionDependingOnGeometry(
                operationType,
                htCell,
                VertexInHtCellIdx,
                VertexInTriIdx,
                tri,
                target,
                cell);
            //Second triangulation
            target = localTriangulations[cell].Item2;
            operationType = kvp.Value.Item2;

            tri = target.Item1;
            VertexInTriIdx = target.Item2;
            RegisterActionDependingOnGeometry(
                operationType,
                htCell,
                VertexInHtCellIdx,
                VertexInTriIdx,
                tri,
                target,
                cell);
        }

        void RegisterActionDependingOnGeometry(GeometryMode operationType, HexTerrainCell htCell, int VertexInHtCellIdx,
            int VertexInTriIdx, Triangulation tri, (Triangulation, int) target, (int, HexTerrainCell) cell)
        {
            switch (operationType)
            {
                case GeometryMode.FlatHexagons:
                    var pInit = htCell.VertexPositionsInPlane[VertexInHtCellIdx];
                    var height = htCell.AverageHeight;

                    RegisterAction(new MovePointAlongYAxisAction(VertexInTriIdx, height), tri);
                    // If the action target edge is a cell border (e.g. target.Item2 ==1)
                    // We find the other cell and add a fan from the vertex to the vertex @ other cell's height 
                    if (target.Item2 == 1)
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
                                t.Item2).Where(
                            oCell => oCell!=htCell 
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

                    break;
                case GeometryMode.FlatTriangles:
                    Console.WriteLine(
                        $"Registering FlatTriangle move for cell vertex {cell.Item2.CellCoordsImplicit},{cell.Item1}, editing Triangulation on edge {target.Item2}");
                    break;
                case GeometryMode.SmoothLinear:
                    break;
                case GeometryMode.Foothill:
                    throw new NotImplementedException();
                case GeometryMode.Plateau:
                    throw new NotImplementedException();
                case GeometryMode.BendingEdge:
                    throw new NotImplementedException();
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