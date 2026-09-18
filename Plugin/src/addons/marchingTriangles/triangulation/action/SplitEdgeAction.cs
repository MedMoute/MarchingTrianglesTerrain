using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class SplitEdgeAction(int edgeIdx ,int startIdx, int endIdx, float weight) : DelegatedTriangulationEditAction
{
    protected override void ValidateBefore(Triangulation t)
    {
        if (weight is < 0 or > 1)
        {
            throw new Exception("Weight must be between 0 and 1.");
        }
    }

    protected override void DoApply(Triangulation t)
    {
        var subEdgeIdx = t.SubEdges.IndexOf((startIdx, endIdx));
        if (subEdgeIdx == -1)
        {
            throw new Exception("The edge to split does not exist.");
        }

        //Find affected triangles list 
        var triangleIdxByTriangles = t.TrianglesByVertices
            .Where(kvp => kvp.Value.Contains(startIdx) && kvp.Value.Contains(endIdx))
            .Select(kvp => kvp.Key).ToList();
        // We process triangle by triangle :
        // For each triangle impacted by the removal of the edge :
        // We remove it from the list of triangles, and remove an occurence of all if its edges in the Edge dictionary
        // Then a new point is defined and 2 triangles are created.
        // The entries in the sub-edge list are matching the removed edges are replaced with the two new edges

        foreach (var affectedTriangle in triangleIdxByTriangles)
        {
            var removedVertexes = t.TrianglesByVertices[affectedTriangle];
            // we obtain the vertex opposite to the removed edge
            var oppositeVertexList = new List<int>(removedVertexes);
            oppositeVertexList.RemoveAll(idx => idx == startIdx || idx == endIdx);
            if (oppositeVertexList.Count != 1)
            {
                throw new Exception("Unexpected state");
            }

            var oppositeVertex = oppositeVertexList[0];
            // We remove it from the list of triangles
            t.TrianglesByVertices.Remove(affectedTriangle);
            // And remove an occurence of all if its edges in the Edge dictionary
            for (int i = 0; i < removedVertexes.Count; i++)
            {
                var removedEdge = (removedVertexes[i], removedVertexes[EngineUtils.mod(i + 1, 3)]);
                var subEdgeExists = t.SubEdges.TryGetValue(removedEdge, out int count);
                if (!subEdgeExists)
                {
                    throw new Exception(String.Format("The affected edge {0} does not exist in the subEdge dictionary.",
                        removedEdge));
                }

                //Remove the edge entry if no longer needed, decrement it otherwise
                if (count > 1)
                {
                    t.SubEdges[removedEdge] = count - 1;
                }
                else
                {
                    t.SubEdges.Remove(removedEdge);
                }
            }

            // Add the new point defined by the barycentric weight
            Vector3 newVertex = t.Vertices[startIdx].Lerp(t.Vertices[endIdx], weight);
            //Register it in the vertex dictionary and the reverse lookup dictionary
            var idx = t.Vertices.Count;
            t.Vertices.Add(idx, newVertex);
            t.ReverseVertices.Add(newVertex, idx);
            // We create two triangles 
            // T1  =[oppo,s,newPoint] , slotted in the removed triangle index
            // T2 = [oppo,newPoint,e] , added at the dictionary's end

            // For each triangle,
            // we insert the implicit Edges in the SubEdge dictionary or bump their count
            List<int> indexes = [oppositeVertex, startIdx, idx];
            t.TrianglesByVertices.Add(affectedTriangle,indexes);
            for (var i = 0; i < 3; i++)
            {
                var implicitEdge = (indexes[i], indexes[EngineUtils.mod(i + 1,3)]); 
                t.SubEdges.TryAdd(implicitEdge, 0);
                t.SubEdges[implicitEdge]++;
            }
            
            indexes = [oppositeVertex,idx,endIdx];
            t.TrianglesByVertices.Add(t.TrianglesByVertices.Count,[oppositeVertex,idx,endIdx]);
            for (var i = 0; i < 3; i++)
            {
                var implicitEdge = (indexes[i], indexes[EngineUtils.mod(i + 1,3)]); 
                t.SubEdges.TryAdd(implicitEdge, 0);
                t.SubEdges[implicitEdge]++;
            }

            //We edit the edge list of the edge that was split 
            var subEdgeList = t.Edges[edgeIdx];
            var firstSubEdge = t.SubEdges.IndexOf((startIdx, idx));
            var secondSubEdge = t.SubEdges.IndexOf((idx,endIdx));
            if (firstSubEdge == -1 || secondSubEdge == -1)
            {
                throw new Exception("Bad state");
            }

            var node = subEdgeList.Find(subEdgeIdx);
            subEdgeList.AddAfter(node ?? throw new InvalidOperationException(), secondSubEdge);
            subEdgeList.AddAfter(node , firstSubEdge);
            subEdgeList.Remove(node);
        }
    }
}