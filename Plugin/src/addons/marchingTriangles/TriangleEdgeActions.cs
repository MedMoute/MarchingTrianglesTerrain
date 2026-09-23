using System;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public static class TriangleEdgeActions
{
    /// <summary>
    /// Edits the geometry of an edge of the cell's sub-triangle following a transformation defined by the provided algorithm
    /// </summary>
    public static void ProcessTriangleEdge(
        int edgeIdx,
        Triangulation triangles,
        GeometryMode algorithm)
    {
        switch (algorithm)
        {
            case GeometryMode.FlatHexagons:
                if (triangles._additionalHints == null || !triangles._additionalHints.ContainsKey(HexTerrainCell.AverageHeightHint))
                {
                    throw new InvalidOperationException("Missing hints entry for computing the cell average height");
                }

                marchingTriangles.TriangleEdgeActions.ProcessFlatHexagonEdge(edgeIdx, triangles);
                return;
            case GeometryMode.FlatTriangles:
                if (triangles._additionalHints == null
                    || !triangles._additionalHints.ContainsKey(HexTerrainCell.AverageHeightHint)
                    || !triangles._additionalHints.ContainsKey(HexTerrainCell.NextTriangleEdgeAvgHeight))
                {
                    throw new InvalidOperationException("Missing hints entry for" +
                                                        " computing the cell average" +
                                                        " height or the next triangle height");
                }
                marchingTriangles.TriangleEdgeActions.ProcessFlatTriangleEdge(edgeIdx, triangles);
                return;
            case GeometryMode.SmoothLinear:
                marchingTriangles.TriangleEdgeActions.ProcessLinearEdge(edgeIdx, triangles);
                return;
            case GeometryMode.Plateau:
                marchingTriangles.TriangleEdgeActions.ProcessPlateauEdge(edgeIdx, triangles);
                return;
            case GeometryMode.Foothill:
                marchingTriangles.TriangleEdgeActions.ProcessFoothillEdge(edgeIdx, triangles);
                return;
            case GeometryMode.BendingEdge:
                marchingTriangles.TriangleEdgeActions.ProcessBendingEdge(edgeIdx, triangles);
                return;
            default:
                throw new NotSupportedException();
        }
    }
}