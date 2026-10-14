using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public static partial class GeometryModeActions
{
    public interface ModeProcessor
    {
        void Vertex(
            VertexGeometryEditor editor,
            HexTerrainCell cell,
            int cellSummit,
            Triangulation tri,
            int vertexIdx,
            List<HexTerrainCell> neighborCells,
            (float, float) parameters);
        
        void Edge(
            VertexGeometryEditor editor,
            HexTerrainCell cell,
            (int start, int end) edge,
            Triangulation tri,
            int vertexIdx,
            List<HexTerrainCell> neighborCells,
            (float,float) parameters);

    }
    
    private static bool IsStartPrimaryVertex(
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx)
    {
        Debug.Assert(cell.GetVertexData != null);
        var isStartPrimaryVertex = tri.SourceTriangle[edgeIdx].IsEqualApprox(edge.start == -1
            ? new Vector3((float)cell.CenterPosition.X, cell.AverageHeight, (float)cell.CenterPosition.Y)
            : new Vector3(
                (float)cell.VertexPositionsInPlane[edge.start].X,
                cell.GetVertexData(edge.start),
                (float)cell.VertexPositionsInPlane[edge.start].Y));
        return isStartPrimaryVertex;
    }

    internal static bool IsEdgeOrdered(
        HexTerrainCell cell,
        (int start, int end) edge,
        Triangulation tri,
        int edgeIdx)
    {
        Debug.Assert(cell.GetVertexData != null);
        var isEdgeOrdered = tri.SourceTriangle[(edgeIdx + 1) % 3].IsEqualApprox(edge.end == -1
            ? new Vector3((float)cell.CenterPosition.X, cell.AverageHeight, (float)cell.CenterPosition.Y)
            : new Vector3(
                (float)cell.VertexPositionsInPlane[edge.end].X,
                cell.GetVertexData(edge.end),
                (float)cell.VertexPositionsInPlane[edge.end].Y));
        return isEdgeOrdered;
    }
}