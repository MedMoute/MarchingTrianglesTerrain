using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class VertexConformalGeometryEdit
{
    private Dictionary<TriangulationEditAction<int>, Triangulation> actionToTriangulationHandle = new();

    private List<TriangulationEditAction<int>> _actions = new();

    public void RegisterVertexAction(TriangulationEditAction<int> action)
    {
        _actions.Add(action);
    }

    public void RegisterAction(TriangulationEditAction<int> action,Triangulation triangulation)
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

    public void RegisterEdgeAction((int, HexTerrainCell) cell, (Triangulation, int) edge, GeometryMode operationType)
    {
        switch (operationType)
        {
            case GeometryMode.FlatHexagons:
                TriangleEdgeActions.RegisterFlatHexagonEdgeProcessingActions(cell.Item2,cell.Item1,edge.Item1,edge.Item2,this);
                break;
            case GeometryMode.FlatTriangles:
                TriangleEdgeActions.RegisterFlatHexagonEdgeProcessingActions(cell.Item2,cell.Item1,edge.Item1,edge.Item2,this);
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