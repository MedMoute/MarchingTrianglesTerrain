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
        var setLevel = cell.AverageHeight;
        //Move the triangle along the edge
        v.RegisterAction(new DisplaceEdgeAlongYAxis(edgeIdx, setLevel, setLevel),triangles);

        if (edgeIdx != 1) return;
        //If outer edge (edgeIdx =1) add fans to match the previous height
        // The center of the cell is the first point of the triangulation as per
        // ComputeTriangleAndMask()
        AddEdgeFans(1, triangles, setLevel,v);
    }


    internal static void RegisterFlatTriangleEdgeProcessingActions(HexTerrainCell cell, int vertexIdx,Triangulation triangles, int edgeIdx,VertexConformalGeometryEdit v)
    {
        var setLevel = (triangles.SourceTriangle[1].Y + triangles.SourceTriangle[2].Y) / 2;
        var nextTriLevel = (float)triangles._additionalHints![HexTerrainCell.NextTriangleEdgeAvgHeight];
        var nextTriVertex = (Vector3)triangles._additionalHints![HexTerrainCell.NextTriangleVertexPos];
        //Move the triangle along the edge
        v.RegisterAction(new DisplaceEdgeAlongYAxis(
            edgeIdx,
            setLevel,
            setLevel),triangles);
        switch (edgeIdx)
        {
            // If right inner edge (edgeIdx =0)  :NOOP
            case 0:
                break;
            //If outer edge (edgeIdx =1) add fans to match the previous height
            // The center of the cell is the first point of the triangulation as per
            // ComputeTriangleAndMask()
            case 1:
                AddEdgeFans(edgeIdx, triangles, setLevel,v);
                break;
            // If right inner edge (edgeIdx =2) add fans to the level of the next triangle , hinted in the dictionary
            case 2:
            {
                //Vertex 2 at nextTriLevel
                var pos1 = new Vector3(
                    triangles.SourceTriangle[edgeIdx].X,
                    nextTriLevel,
                    triangles.SourceTriangle[edgeIdx].Z);
                //Vertex 0 at nextTriLevel
                var pos2 = new Vector3(
                    triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)].X,
                    nextTriLevel,
                    triangles.SourceTriangle[EngineUtils.mod(edgeIdx + 1, 3)].Z);

                var newPoint = new AddTrianglesOnBorderEdge(edgeIdx, pos1).Apply(triangles);
                new AddTrianglesOnBorderEdge(edgeIdx, pos2).Apply(triangles);
                
                //split the edge at the h=nexTriLevel junction to make a sure the mesh is conformal
                if (Math.Abs(setLevel - nextTriLevel) > 1e-5) //  No height changes => skip 
                {
                    //TODO handle the different cases(h_v<nh<h ; h_v<h<nh ; h<h_v<nh )
                    var pos1Idx = triangles.ReverseVertices[pos1];
                    //Vertex 2 at setLevel
                    var pos3 = new Vector3(
                        triangles.SourceTriangle[edgeIdx].X,
                        setLevel,
                        triangles.SourceTriangle[edgeIdx].Z);
                    var pos3Idx = triangles.ReverseVertices[pos3];
                    //The penultimate vertex of edge#1
                    int pos4Idx=triangles.Edges[EngineUtils.mod(edgeIdx-1,3)].Last.Previous.Value;
                    var pos4 = triangles.Vertices[pos4Idx];
                    new AddTriangleFan((pos1Idx, pos3Idx), pos4).Apply(triangles);
                    new AddTriangleFan((pos1Idx, pos3Idx), pos4).Apply(triangles);

                }
            }
                break;
        }
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
            v.RegisterAction(new ComposedTriangularEditAction(triangles => { 
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
                new AddTriangleFan((subEdge.Item2, newPoint), pos2).Apply(triangles);}
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