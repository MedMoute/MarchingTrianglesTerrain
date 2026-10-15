using System;
using System.Collections.Generic;
using System.Linq;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
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


    public void ApplyAllTransformations()
    {
        foreach (var triangulationEditAction in _actions)
        {
            triangulationEditAction.Apply(_actionToTriangulationHandle[triangulationEditAction]);
        }
    }

    public void ApplyTransformations(Type type)
    {
        foreach (var triangulationEditAction in _actions.Where(a => a.GetType() == type))
        {
            triangulationEditAction.Apply(_actionToTriangulationHandle[triangulationEditAction]);
        }
    }

    public void RegisterLocalVertexAction(
        HexTerrainCell cell,
        int cellSummitIdx,
        Triangulation triangulation,
        int vertexTriIdx,
        List<HexTerrainCell> neighbors,
        (GeometryMode mode, (float, float) parameters) effectiveEdgeAction)
    {
        // Console.WriteLine($"     T[#{triangulation.Id};V{vertexTriIdx}] :" +
        //                   $" =>  <{effectiveEdgeAction.mode},{effectiveEdgeAction.parameters}>");

        effectiveEdgeAction.mode.Processor().Vertex(
            this,
            cell,
            cellSummitIdx,
            triangulation,
            vertexTriIdx,
            [.. neighbors.Where(c => !c.Equals(cell))],
            effectiveEdgeAction.parameters);
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
    /// <param name="effectiveAction"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void RegisterLocalEdgeAction(HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation triangulation,
        int edgeIndexInTri,
        List<HexTerrainCell> neighborCells,
        (GeometryMode mode, (float, float) parameters) effectiveAction)
    {
        // Console.WriteLine($"     T[#{triangulation.Id};E{edgeIndexInTri}] :" +
        //                   $" =>  <{effectiveAction.mode},{effectiveAction.parameters}>");
        effectiveAction.mode.Processor().Edge(
            this,
            cell,
            edge,
            triangulation,
            edgeIndexInTri,
            neighborCells,
            effectiveAction.parameters);
    }

    public void RegisterLocalActions(
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation triangulation,
        int edgeIdx,
        List<HexTerrainCell> neighborCells,
        (GeometryMode mode, (float, float) parameters) effectiveEdgeAction)
    {
        if (GeometryModeActions.IsEdgeOrdered(cell, edge, triangulation, edgeIdx))
        {
            int startVertexIdx = GetEdgeStartVertexIndex(cell, edge, triangulation, edgeIdx);
            RegisterLocalVertexAction(cell, edge.start, triangulation, startVertexIdx, neighborCells,
                effectiveEdgeAction);
        }
        else
        {
            int endVertexIdx = GetEdgeEndVertexIndex(cell, edge, triangulation, edgeIdx);
            RegisterLocalVertexAction(cell, edge.end, triangulation, endVertexIdx, neighborCells, effectiveEdgeAction);
        }

        RegisterLocalEdgeAction(cell, edge, triangulation, edgeIdx, neighborCells, effectiveEdgeAction);
    }

    public static int GetEdgeStartVertexIndex(
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx)
    {
        return GeometryModeActions.IsEdgeOrdered(cell, edge, tri, edgeIdx) ? edgeIdx : EngineUtils.Mod(edgeIdx + 1, 3);
    }

    public static int GetEdgeEndVertexIndex(
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx)
    {
        return GeometryModeActions.IsEdgeOrdered(cell, edge, tri, edgeIdx) ? EngineUtils.Mod(edgeIdx + 1, 3) : edgeIdx;
    }
}