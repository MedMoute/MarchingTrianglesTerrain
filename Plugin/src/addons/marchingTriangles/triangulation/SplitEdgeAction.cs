using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class SplitEdgeAction(Vector3 edgeStart, Vector3 edgeEnd, float weight) : DelegatedTriangulationEditAction
{
    protected override Func<Triangulation, Triangulation> DelegateAction
    {
        get
        {
            return t =>
            {
                //Find indexes
                var foundStart = t.ReverseVertices.TryGetValue(edgeStart, out var startIdx);
                var foundEnd = t.ReverseVertices.TryGetValue(edgeEnd, out var endIdx);
                if (!foundStart || !foundEnd)
                {
                    throw new Exception(
                        "Could not find one of the split end points inside the triangulation");
                }

                var edgeIdx = t.SubEdges.IndexOf((startIdx, endIdx));
                if (edgeIdx == -1)
                {
                    throw new Exception("Bad :(");
                }

                //find affected triangles 
                // var triangleIdx = t.TrianglesByEdges.Where(kvp => kvp.Value.Contains(edgeIdx)).Select(kvp => kvp.Key)
                //     .ToList();

                // Debug Check : ensure the triangle list  is consistent
                var triangleIdxByTriangles = t.TrianglesByVertices
                    .Where(kvp => kvp.Value.Contains(startIdx) && kvp.Value.Contains(endIdx))
                    .Select(kvp => kvp.Key).ToList();
                // if (!(triangleIdx.TrueForAll(triangleIdxByTriangles.Contains) &&
                //       triangleIdxByTriangles.TrueForAll(triangleIdx.Contains)))
                // {
                //     // Debug statement
                //     Console.WriteLine("TrianglesByEdges");
                //     // foreach (var triAsEdgeList in t.TrianglesByEdges)
                //     // {
                //     //     Console.WriteLine("T[" + triAsEdgeList.Key + "] "+ "Edges : "  + String.Join(",",triAsEdgeList.Value)+ " => Points : " + String.Join(",",triAsEdgeList.Value.Select(i => t.Edges.ElementAt(i).Key).ToList()) );
                //     // }
                //     Console.WriteLine("TrianglesByVertexes");
                //     foreach (var triAsVertList in t.TrianglesByVertices)
                //     {
                //         Console.WriteLine("T[" + triAsVertList.Key + "] " + "Points : " + String.Join(",",triAsVertList.Value) );
                //     }
                //
                //     throw new Exception(
                //         string.Format("Unexpected state : expected {0} and {1} to be equal",
                //             string.Join(',', triangleIdx),
                //             string.Join(',', triangleIdxByTriangles)));
                // }

                foreach (var removedTriangle in triangleIdxByTriangles)
                {
                    //remove the edge
                    if (!t.SubEdges.Remove((startIdx, endIdx)))
                    {
                        throw new Exception("Edge was unexpectedly not removed");
                    }

                    //Remove the split triangle
                    var points = t.TrianglesByVertices[removedTriangle];
                    //Find the index of the opposite vertex
                    var oppositePointIdx = new List<int>(points);
                    oppositePointIdx.Remove(startIdx);
                    oppositePointIdx.Remove(endIdx);

                    //  t.TrianglesByEdges.Remove(removedTriangle);
                    t.TrianglesByVertices.Remove(removedTriangle);
                    //Register a new point
                    int newIdx = t.Vertices.Count;
                    t.Vertices.Add(newIdx, edgeStart.Lerp(edgeEnd, weight));
                    t.ReverseVertices.Add(edgeStart.Lerp(edgeEnd, weight), newIdx);
                    //Add the new edges
                    t.SubEdges.Add((startIdx, newIdx), 1);
                    //Used by each of the new triangles
                    t.SubEdges.Add((newIdx, oppositePointIdx[0]), 2);
                    t.SubEdges.Add((newIdx, endIdx), 1);
                    //Add the new triangles
                    var newEdgeIdx = t.SubEdges.IndexOf((startIdx, newIdx));
                    var newSplitEdgeIdx = t.SubEdges.IndexOf((newIdx, oppositePointIdx[0]));
                    var keptEdge = t.SubEdges.IndexOf((oppositePointIdx[0], startIdx));

                    List<int> newEdges = [newEdgeIdx, newSplitEdgeIdx,keptEdge];

                    //Find the index that is no longer in this triangle
                    HashSet<int> vertexIndexesInTriangle = new();
                    foreach (var edge in newEdges)
                    {
                        var (i, _) = t.SubEdges.ElementAt(edge);
                        vertexIndexesInTriangle.Add(i.Item1);
                        vertexIndexesInTriangle.Add(i.Item2);
                    }

                    var missingVertices = new List<int>(points);
                    missingVertices.RemoveAll(vertexIndexesInTriangle.Contains);
                    if (missingVertices.Count != 1)
                    {
                        throw new Exception("Unexpected state");
                    }

                    var missingVertex = missingVertices[0];

                    var replacedVertexPosition = points.Find(idx => idx == missingVertex);
                    var editablePoints = new List<int>(points);
                    editablePoints[replacedVertexPosition] = newIdx;
                    t.TrianglesByVertices.Add(removedTriangle, editablePoints);
                    // In the edge indexing
                    var editableEdges = new List<int>();
                    for (int i = 0; i < 3; i++)
                    {
                        editableEdges.Add(t.SubEdges.IndexOf((editablePoints[i],editablePoints[EngineUtils.mod(i+1,3)])));
                    }
                    //t.TrianglesByEdges.Add(removedTriangle, editableEdges );

                    // Second triangle
                    newEdgeIdx = t.SubEdges.IndexOf((newIdx, endIdx));
                    newSplitEdgeIdx = t.SubEdges.IndexOf((newIdx, oppositePointIdx[0]));
                    keptEdge = t.SubEdges.IndexOf((oppositePointIdx[0], endIdx));
                    newEdges = [newEdgeIdx, keptEdge,newSplitEdgeIdx];

                    //Find the index that is no longer in this triangle
                    vertexIndexesInTriangle = new();
                    foreach (var edge in newEdges)
                    {
                        var (i, _) = t.SubEdges.ElementAt(edge);
                        vertexIndexesInTriangle.Add(i.Item1);
                        vertexIndexesInTriangle.Add(i.Item2);
                    }

                    missingVertices = new List<int>(points);
                    missingVertices.RemoveAll(vertexIndexesInTriangle.Contains);
                    if (missingVertices.Count != 1)
                    {
                        throw new Exception("Unexpected state");
                    }

                    missingVertex = missingVertices[0];

                    replacedVertexPosition = points.Find(idx => idx == missingVertex);
                    editablePoints = new List<int>(points);
                    editablePoints[replacedVertexPosition] = newIdx;
                    t.TrianglesByVertices.Add(t.TrianglesByVertices.Count, editablePoints);
                    // In the edge indexing
                    editableEdges = new List<int>();
                    for (int i = 0; i < 3; i++)
                    {
                        editableEdges.Add(t.SubEdges.IndexOf((editablePoints[i],editablePoints[EngineUtils.mod(i+1,3)])));
                    }
                    //t.TrianglesByEdges.Add(t.TrianglesByEdges.Count, editableEdges );
                    // Debug statement
                    // Console.WriteLine("T[" + t.TrianglesByEdges.Count + "] " + "Points : " + String.Join(",",editablePoints) +
                    //                   "|Edges : " + String.Join(",", newEdges) + " => " + String.Join(",",
                    //                       newEdges.Select(i => t.Edges.ElementAt(i).Key).ToList()));
                }

                return t;
            };
        }
    }
}