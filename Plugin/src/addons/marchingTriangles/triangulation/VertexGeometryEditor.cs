using System;
using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class VertexGeometryEditor
{
    private readonly Vector3I _vertex;

    private readonly Dictionary<ITriangulationEditAction<int>, Triangulation> _actionToTriangulationHandle = new();

    private readonly List<ITriangulationEditAction<int>> _actions = new();

    public VertexGeometryEditor(Vector3I vertex)
    {
        _vertex = vertex;
    }

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
    public void RegisterLocalVertexAction(Vector3I vertexIndex,
        (Triangulation? triangulation, int indexOfCellSummitInTri) triangleData,
        (GeometryMode, (float, float)) appliedGeometryOperations,
        Dictionary<Vector2I, HexTerrainCell> neighborCells,
        (Vector3I start,Vector3I end) edge)
    {
            var parameters = appliedGeometryOperations.Item2;
            var operationType = appliedGeometryOperations.Item1;

            var htCell = neighborCells[new Vector2I(vertexIndex.X,vertexIndex.Y)];

            if (triangleData.triangulation == null)
            {
                throw new ArgumentException("Triangulation is null");
            }
            switch (operationType)
            {
                case GeometryMode.FlatHexagons:
                   
                        GeometryModeActions.ProcessVertexOperationsForFlatHexes(this, neighborCells, htCell,
                            vertexIndex.Z,
                            triangleData.indexOfCellSummitInTri, triangleData.triangulation);
                    break;
                case GeometryMode.FlatTriangles:
                    GeometryModeActions.ProcessVertexOperationsForFlatTriangles(this, htCell,
                        vertexIndex.Z,
                        triangleData.indexOfCellSummitInTri,
                        triangleData.triangulation,
                        edge);
                    break;
                case GeometryMode.SmoothLinear:
                    //NOOP
                    break;
                case GeometryMode.Foothill:
                    GeometryModeActions.ProcessVertexOperationsForBendingEdge(this, parameters, htCell,
                        vertexIndex.Z,
                        triangleData.indexOfCellSummitInTri, triangleData.triangulation,edge,  false, true);
                    break;
                case GeometryMode.Plateau:
                    GeometryModeActions.ProcessVertexOperationsForBendingEdge(this, parameters, htCell,
                        vertexIndex.Z,
                        triangleData.indexOfCellSummitInTri, triangleData.triangulation,edge, true, false);
                    break;
                case GeometryMode.BendingEdge:
                    GeometryModeActions.ProcessVertexOperationsForBendingEdge(this, parameters, htCell,
                        vertexIndex.Z,
                        triangleData.indexOfCellSummitInTri, triangleData.triangulation,edge,  true, true);
                    break;
                //TODO
                //throw new NotImplementedException();
                case GeometryMode.FlatHexagonsNoFans:
                    GeometryModeActions.ProcessVertexOperationsForFlatHexes(this, neighborCells, htCell,
                        vertexIndex.Z,
                        triangleData.indexOfCellSummitInTri, triangleData.triangulation, false);
                    break;
                case GeometryMode.FlatTrianglesNoFans:
                    GeometryModeActions.ProcessVertexOperationsForFlatTriangles(this, htCell,
                        vertexIndex.Z,
                        triangleData.indexOfCellSummitInTri, triangleData.triangulation,  edge,false);
                    break;
                default:
                    throw new NotImplementedException();
            }
    }
}