using System;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Utility class for processing triangle edges using the Triangulation framework. 
/// </summary>
internal static class TriangleEdgeActions
{
    internal static void RegisterFlatHexagonEdgeProcessingActions(HexTerrainCell cell, int vertexIdx,Triangulation triangles, int edgeIdx,VertexConformalGeometryEdit v)
    {
        Console.WriteLine($"Registering FlatHexagon move for cell vertex {cell.CellCoordsImplicit},{vertexIdx}, editing Triangulation{triangles.GetHashCode()} on edge {edgeIdx}");
        var pInit = cell.VertexPositionsInPlane[vertexIdx];
        var height = cell.AverageHeight;
        var pos = new Vector3((float)pInit.X, height, (float)pInit.Y); 
        v.RegisterAction(new AddTrianglesOnBorderEdge(edgeIdx, pos),triangles);
    }


    internal static void RegisterFlatTriangleEdgeProcessingActions(
        HexTerrainCell cell,
        int vertexIdx,
        Triangulation triangles,
        int edgeIdx,VertexConformalGeometryEdit v)
    {
        Console.WriteLine($"Registering FlatTriangle move for cell vertex {cell.CellCoordsImplicit},{vertexIdx}, editing Triangulation on edge {edgeIdx}");
    }

    private static void AddEdgeFans(int edgeIdx, Triangulation triangles, float setLevel, VertexConformalGeometryEdit v)
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
            if (Math.Abs(triangles.SourceTriangle[edgeIdx].Y - setLevel) > 1e-5)
            {
                v.RegisterAction(new AddTrianglesOnBorderEdge(edgeIdx, triangles.SourceTriangle[edgeIdx]),triangles);
            }

            if (Math.Abs(triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)].Y - setLevel) > 1e-5)
            {
                v.RegisterAction(new AddTrianglesOnBorderEdge(edgeIdx, triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)]),triangles);
            }
        }
        else
        {
            v.RegisterAction(
                new ComposedTriangularEditAction(triangles => { 
                var subEdge = triangles.SubEdges.ElementAt(triangles.Edges[edgeIdx].First!.Value).Key;
                var yStart = Math.Abs(triangles.SourceTriangle[edgeIdx].Y - setLevel);
                var yEnd = Math.Abs(triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)].Y - setLevel);

                var newPoint = new SplitSubEdgeAction(
                        1,
                        subEdge.Item1,
                        subEdge.Item2,
                        yStart / (yStart + yEnd)).Apply(triangles);
                var pos1 = triangles.SourceTriangle[edgeIdx];
                var pos2 = triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)];
               /* new AddTriangleFan((subEdge.Item1, newPoint), pos1).Apply(triangles);
                return new AddTriangleFan((subEdge.Item2, newPoint), pos2).Apply(triangles);*/
               return newPoint;
                    }
                ),triangles);
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