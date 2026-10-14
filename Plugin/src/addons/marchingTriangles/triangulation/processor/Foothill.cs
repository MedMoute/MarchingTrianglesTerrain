using System;
using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class Foothill : GeometryModeActions.ModeProcessor
{
    public static readonly Foothill Processor = new();

    private Foothill()
    {
    }

    public void Vertex(VertexGeometryEditor editor,
        HexTerrainCell cell,
        int cellSummit,
        Triangulation tri,
        int vertexIdx,
        List<HexTerrainCell> neighborCells,
        (float, float) parameters) //NOOP
    {
    }

    public void Edge(VertexGeometryEditor editor,
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx,
        List<HexTerrainCell> neighborCells,
        (float, float) parameters)
    {
        if (parameters.Item1 is < 0 or > 1 ||
            parameters.Item2 is < 0 or > 1)
        {
            throw new ArgumentException("parameters must be between 0 and 1.");
        }

        if (parameters.Item1 == 0 || parameters.Item2 == 0)
        {
            return;
        }

        var p1 = tri.Vertices[edgeIdx];
        var p2 = tri.Vertices[EngineUtils.Mod(edgeIdx + 1, 3)];

        var invert = GeometryModeActions.IsEdgeOrdered(cell, edge, tri, edgeIdx);
        var value = invert
            ? Mathf.Lerp(p2.Y, p1.Y, parameters.Item2)
            : Mathf.Lerp(p1.Y, p2.Y, parameters.Item2);
        var weight = invert ? parameters.Item1 : 1 - parameters.Item1;

        if (edgeIdx == 1) return;

        float splitWeight = (1 - weight);
        editor.RegisterAction(new ComposedTriangularEditAction(triangulation =>
            {
                var newPt = new SplitSubEdgeAction(
                        edgeIdx,
                        edgeIdx,
                        EngineUtils.Mod(edgeIdx + 1, 3),
                        splitWeight)
                    .Apply(triangulation);

                new MovePointAlongYAxisAction(newPt, value).Apply(triangulation);


                return newPt;
            }
        ), tri);
    }
}