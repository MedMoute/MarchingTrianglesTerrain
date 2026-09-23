using System;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Utility class for processing triangle edges using the Triangulation framework. 
/// </summary>
internal static class TriangleEdgeActions
{
    internal static void ProcessFlatHexagonEdge(int edgeIdx, Triangulation triangles)
    {
        var setLevel = (float)triangles._additionalHints![HexTerrainCell.AverageHeightHint];
        //Move the triangle along the edge
        new DisplaceEdgeAlongYAxis(edgeIdx, setLevel, setLevel).Apply(triangles);

        if (edgeIdx != 1) return;
        //If outer edge (edgeIdx =1) add fans to match the previous height
        // The center of the cell is the first point of the triangulation as per
        // ComputeTriangleAndMask()
        AddEdgeFans(1, triangles, setLevel);
    }


    internal static void ProcessFlatTriangleEdge(int edgeIdx, Triangulation triangles)
    {
        var setLevel = (triangles.SourceTriangle[1].Y + triangles.SourceTriangle[2].Y) / 2;
        var nextTriLevel = (float)triangles._additionalHints![HexTerrainCell.NextTriangleEdgeAvgHeight];
        var nextTriVertex = (Vector3)triangles._additionalHints![HexTerrainCell.NextTriangleVertexPos];
        //Move the triangle along the edge
        new DisplaceEdgeAlongYAxis(
            edgeIdx,
            setLevel,
            setLevel).Apply(triangles);
        switch (edgeIdx)
        {
            // If right inner edge (edgeIdx =0)  :NOOP
            case 0:
                break;
            //If outer edge (edgeIdx =1) add fans to match the previous height
            // The center of the cell is the first point of the triangulation as per
            // ComputeTriangleAndMask()
            case 1:
                AddEdgeFans(edgeIdx, triangles, setLevel);
                break;
            // If right inner edge (edgeIdx =2) add fans to the level of the next triangle , hinted in the dictionary
            case 2:
            {
                var pos1 = new Vector3(
                    triangles.SourceTriangle[edgeIdx].X,
                    nextTriLevel,
                    triangles.SourceTriangle[edgeIdx].Z);
                
                var pos2 = new Vector3(
                    triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, (int)3)].X,
                    nextTriLevel,
                    triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, (int)3)].Z);

                var newPoint = new AddTrianglesOnBorderEdge(edgeIdx, pos1).Apply(triangles);
                new AddTrianglesOnBorderEdge(edgeIdx, pos2).Apply(triangles);
                var hstart = Math.Abs(triangles.SourceTriangle[edgeIdx].Y - nextTriLevel);
                var hend = Math.Abs(nextTriVertex.Y - nextTriLevel);

                var pos3 = triangles.SourceTriangle[edgeIdx].Lerp(nextTriVertex, hstart / (hstart + hend));
                new AddTriangleFan((edgeIdx, newPoint), pos3).Apply(triangles);
            }
                break;
        }
    }

    private static void AddEdgeFans(int edgeIdx, Triangulation triangles, float setLevel)
    {
        if (triangles.Edges[edgeIdx].Count > 1)
        {
            throw new NotSupportedException("TODO : support Adding fans when the edge itself is already split");
        }

        //If both initial points are over or under the setLevel create 
        if (setLevel <= triangles.SourceTriangle[edgeIdx].Y &&
            setLevel <= triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)].Y ||
            setLevel >= triangles.SourceTriangle[edgeIdx].Y &&
            setLevel >= triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)].Y)
        {
            new AddTrianglesOnBorderEdge(edgeIdx, triangles.SourceTriangle[edgeIdx]).Apply(triangles);
            new AddTrianglesOnBorderEdge(edgeIdx, triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)]).Apply(triangles);
        }
        else
        {
            var subEdge = triangles.SubEdges.ElementAt(triangles.Edges[edgeIdx].First!.Value).Key;
            var yStart = Math.Abs(triangles.SourceTriangle[edgeIdx].Y - setLevel);
            var yEnd = Math.Abs(triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)].Y - setLevel);

            var newPoint =
                new SplitSubEdgeAction(
                    1,
                    subEdge.Item1,
                    subEdge.Item2,
                    yStart / (yStart + yEnd)).Apply(triangles);
            var pos1 = triangles.SourceTriangle[edgeIdx];
            var pos2 = triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)];
            new AddTriangleFan((subEdge.Item1, newPoint), pos1).Apply(triangles);
            new AddTriangleFan((subEdge.Item2, newPoint), pos2).Apply(triangles);
        }
    }


    internal static void ProcessLinearEdge(int _0, Triangulation _1)
    {
        //NOOP
    }

    internal static void ProcessPlateauEdge(int edgeIdx, Triangulation triangles)
    {
        //TODO
    }

    internal static void ProcessFoothillEdge(int edgeIdx, Triangulation triangles)
    {
        //TODO
    }

    internal static void ProcessBendingEdge(int edgeIdx, Triangulation triangles)
    {
        //TODO
    }
}