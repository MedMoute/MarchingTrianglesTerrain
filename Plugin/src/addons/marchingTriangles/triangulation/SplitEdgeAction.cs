using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class SplitEdgeAction(int startIdx, int endIdx, float weight) : DelegatedTriangulationEditAction
{
    protected override Triangulation doApply(Triangulation t)
    {
        var edgeIdx = t.SubEdges.IndexOf((startIdx, endIdx));
        if (edgeIdx == -1)
        {
            throw new Exception("The edge to split does not exist.");
        }

        //Find affected triangles list 
        var triangleIdxByTriangles = t.TrianglesByVertices
            .Where(kvp => kvp.Value.Contains(startIdx) && kvp.Value.Contains(endIdx))
            .Select(kvp => kvp.Key).ToList();
        // We process triangle by triangle
        foreach (var removedTriangle in triangleIdxByTriangles)
        {
        }
        return t;
    }
}