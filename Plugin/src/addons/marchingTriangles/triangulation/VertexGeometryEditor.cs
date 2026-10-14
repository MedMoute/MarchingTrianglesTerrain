using System;
using System.Collections.Generic;
using System.Linq;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class VertexGeometryEditor
{
    private readonly Dictionary<ITriangulationEditAction<int>, Triangulation> _actionToTriangulationHandle = new();

    private readonly List<ITriangulationEditAction<int>> _actions = new();


    public void RegisterAction(ITriangulationEditAction<int> action, Triangulation triangulation)
    {
        _actionToTriangulationHandle.Add(action, triangulation);
        _actions.Add(action);
    }

    /// <summary>
    /// Registers the contribution of a given triangle vertex index (indexing within the triangulation) 
    /// ,along a given edge and a given triangulation local vertex operations for a provided sub-cell triangulation,
    /// given an applied operation for that.
    /// The collected operations are required to perform the GeometryMode's action upon application.
    /// </summary>
    /// <param name="cell"></param>
    /// <param name="edge">The cell summit indexes of the edge</param>
    /// <param name="triangulation">the triangulation being modified</param>
    /// <param name="edgeIndexInTri">the index of the triangulation edge on which the registered operations will occur</param>
    /// <param name="neighborCells"></param>
    /// <param name="appliedGeometryOperation"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void RegisterLocalEdgeAction(HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation triangulation,
        int edgeIndexInTri,
        List<HexTerrainCell> neighborCells,
        (GeometryMode mode, (float, float) parameters) appliedGeometryOperation)
    {
        //Ensure the local operation is correct
        PreconditionChecks();
        
        switch (appliedGeometryOperation.mode)
        {
            case GeometryMode.FlatHexagons:
                GeometryModeActions.ProcessVertexOperationsForFlatHexes(
                    this,
                    cell,
                    edge,
                    triangulation,
                    edgeIndexInTri,
                    [.. neighborCells.Where(c => !c.Equals(cell))]);
                break;
            case GeometryMode.FlatTriangles:
                GeometryModeActions.ProcessVertexOperationsForFlatTriangles(
                    this,
                    cell,
                    edge,
                    triangulation,
                    edgeIndexInTri,
                    [.. neighborCells.Where(c => !c.Equals(cell))]);
                break;
            case GeometryMode.SmoothLinear:
                //NOOP
                break;
            case GeometryMode.Foothill:
                GeometryModeActions.ProcessEdgeOperationsForBendingEdge(
                    this,
                    cell,
                    edge,
                    triangulation,
                    edgeIndexInTri,
                    appliedGeometryOperation.parameters,
                    false,
                    true);
                break;
            case GeometryMode.Plateau:
                GeometryModeActions.ProcessEdgeOperationsForBendingEdge(
                    this,
                    cell,
                    edge,
                    triangulation,
                    edgeIndexInTri,
                    appliedGeometryOperation.parameters,
                    true,
                    false);
                break;
            case GeometryMode.BendingEdge:
                GeometryModeActions.ProcessEdgeOperationsForBendingEdge(
                    this,
                    cell,
                    edge,
                    triangulation,
                    edgeIndexInTri,
                    appliedGeometryOperation.parameters,
                    true,
                    true);
                break;
            case GeometryMode.FlatHexagonsNoFans:
                GeometryModeActions.ProcessVertexOperationsForFlatHexes(
                    this,
                    cell,
                    edge,
                    triangulation,
                    edgeIndexInTri,
                    [.. neighborCells.Where(c => !c.Equals(cell))],
                    applyFans: false);
                break;
            case GeometryMode.FlatTrianglesNoFans:
                GeometryModeActions.ProcessVertexOperationsForFlatTriangles(
                    this,
                    cell,
                    edge,
                    triangulation,
                    edgeIndexInTri,
                    [.. neighborCells.Where(c => !c.Equals(cell))],
                    applyFans: false);
                break;
            default:
                throw new NotImplementedException();
        }
        
        void PreconditionChecks()
        {
            //Data sanity checks
        
            // 1. nullcheck Condition
            if (triangulation == null)
            {
                throw new Exception();
            }
            // 2. Edge state
            // 2.1 (2 different)
            if (edge.start == edge.end)
            {
                throw new ArgumentException(nameof(edge));
            }
        }
    }

    public void ApplyTransformations()
    {
        foreach (var triangulationEditAction in _actions)
        {
            triangulationEditAction.Apply(_actionToTriangulationHandle[triangulationEditAction]);
        }
    }
}