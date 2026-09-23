using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Godot;
using Godot.Collections;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Action that splits a triangulation's sub-edge at a given
/// point defined by a weight between the sub-edge's starting and ending vertices.
/// The provided starting indexes must be registered as part of an edge's border  
/// </summary>
public class SplitEdgeAction : DelegatedTriangulationEditAction<int>
{
    /// <summary>
    /// The index of the split edge. Must be between 0 and 2.
    /// </summary>
    private readonly int _edgeIdx;

    /// <summary>
    /// The split sub-edge's starting vertex index
    /// </summary>
    private readonly int _startIdx;

    /// <summary>
    /// The split sub-edge's ending vertex index
    /// </summary>
    private readonly int _endIdx;

    /// <summary>
    /// The barycentric weight that defines the split position
    /// </summary>
    private readonly float _weight;

    public SplitEdgeAction(int edgeIdx, int startIdx, int endIdx, float weight)
    {
        _edgeIdx = edgeIdx;
        _startIdx = startIdx;
        _endIdx = endIdx;
        _weight = weight;

        if (_weight is <= 0 or >= 1)
        {
            throw new ArgumentException("Weight must strictly be between 0 and 1.");
        }

        if (_edgeIdx is < 0 or > 2)
        {
            throw new ArgumentException("Edge index must be between 0 and 2.");
        }

        if (startIdx < 0 || endIdx < 0)
        {
            throw new ArgumentException("Vertex indexes must be strictly positive.");
        }

        if (startIdx == endIdx)
        {
            throw new ArgumentException("Starting and ending vertex indexes must be different from one another.");
        }
    }

    protected override void ValidateBefore(Triangulation t)
    {
    }

    protected override int DoApply(Triangulation t)
    {
        Console.WriteLine($"Split {_startIdx} => {_endIdx} @{_weight}");
        var subEdgeIdx = t.SubEdges.IndexOf((startIdx: _startIdx, endIdx: _endIdx));
        //Register a dictionary of the edited edges with their initial positions and their edited positions
        var editedSubEdges =
            new System.Collections.Generic.Dictionary<(int, int), Tuple<int, int?>>(UnorderedTupleComparer.Instance);
        var idx = t.Vertices.Count;

        
        if (subEdgeIdx == -1)
        {
            throw new InvalidOperationException("The sub edge to split does not exist.");
        }

        if (!t.Edges[_edgeIdx].Contains(subEdgeIdx))
        {
            throw new InvalidOperationException(String.Format("The split SubEdge {0} does not belong to the split Edge {1}.",subEdgeIdx,_edgeIdx));

        }

        //Find affected triangles list 
        var triangleIdxByTriangles = t.TrianglesByVertices
            .Where(kvp => kvp.Value.Contains(_startIdx) && kvp.Value.Contains(_endIdx))
            .Select(kvp => kvp.Key).ToList();
        // We process triangle by triangle :
        // For each triangle impacted by the removal of the edge :
        // We remove it from the list of triangles, and remove an occurence of all if its edges in the Edge dictionary
        // Then a new point is defined and 2 triangles are created.
        // The entries in the sub-edge list are matching the removed edges are replaced with the two new edges

        if (triangleIdxByTriangles.Count == 0)
        {
            throw new ConstraintException(
                "There should always be a triangle affected by an Edge Split operation." +
                " Something is wrong here.");
        }

        foreach (var affectedTriangle in triangleIdxByTriangles)
        {
            var removedVertexes = t.TrianglesByVertices[affectedTriangle];
            // we obtain the vertex opposite to the removed edge
            var oppositeVertexList = new List<int>(removedVertexes);
            oppositeVertexList.RemoveAll(idx => idx == _startIdx || idx == _endIdx);
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
                    //register the edge removal and the indexes.
                    //We actually "remove" the SubEdges now by setting their usages to 0, this is done to preserve indexing order
                    // in the SubEdges dictionary.
                    var removedIdx = t.SubEdges.IndexOf(removedEdge);
                    editedSubEdges.Add(removedEdge, new Tuple<int, int?>(removedIdx, null));
                    t.SubEdges.SetAt(removedIdx,0);

                }
            }

            //We actually "remove" the SubEdges now by setting it to -1, this is done to preserve indexing


            // Add the new point defined by the barycentric weight
            Vector3 newVertex = t.Vertices[_startIdx].Lerp(t.Vertices[_endIdx], _weight);
            //Register it in the vertex dictionary and the reverse lookup dictionary
            t.Vertices.TryAdd(idx, newVertex);
            t.ReverseVertices.TryAdd(newVertex, idx);
            // We create two triangles 
            // T1  =[oppo,s,newPoint] , slotted in the removed triangle index
            // T2 = [oppo,newPoint,e] , added at the dictionary's end

            // For each of the two new triangles,
            // we insert the implicit Edges in the SubEdge dictionary or bump their count
            List<int> indexes = [oppositeVertex, _startIdx, idx];
            t.TrianglesByVertices.Add(affectedTriangle, indexes);
            for (var i = 0; i < 3; i++)
            {
                var implicitEdge = (indexes[i], indexes[EngineUtils.mod(i + 1, 3)]);
                t.SubEdges.TryAdd(implicitEdge, 0);
                t.SubEdges[implicitEdge]++;
                //If the sub-edge was previously removed, register its new index
                var wasRemoved = editedSubEdges.TryGetValue(implicitEdge, out var indexChanges);
                if (wasRemoved)
                {
                    editedSubEdges.Remove(implicitEdge);
                    editedSubEdges.Add(implicitEdge,
                        new Tuple<int, int?>(indexChanges.Item1, t.SubEdges.IndexOf(implicitEdge)));
                }
            }

            indexes = [oppositeVertex, idx, _endIdx];
            t.TrianglesByVertices.Add(t.TrianglesByVertices.Count, [oppositeVertex, idx, _endIdx]);
            for (var i = 0; i < 3; i++)
            {
                var implicitEdge = (indexes[i], indexes[EngineUtils.mod(i + 1, 3)]);
                t.SubEdges.TryAdd(implicitEdge, 0);
                t.SubEdges[implicitEdge]++;
                //If the sub-edge was previously removed, register its new index
                var wasRemoved = editedSubEdges.TryGetValue(implicitEdge, out var indexChanges);
                if (wasRemoved)
                {
                    editedSubEdges.Remove(implicitEdge);
                    editedSubEdges.Add(implicitEdge,
                        new Tuple<int, int?>(indexChanges.Item1, t.SubEdges.IndexOf(implicitEdge)));
                }
            }

            //We replace the Edited Values in the Edges List
            foreach (var editedSubEdge in editedSubEdges.Where(kvp => kvp.Value.Item2 != null))
            {
                for (int i = 0; i < 3; i++)
                {
                    var find = t.Edges[i].Find(editedSubEdge.Value.Item1);
                    if (find != null)
                    {
                        // ReSharper disable once PossibleInvalidOperationException
                        find.Value = editedSubEdge.Value.Item2.Value;
                    }
                }
            }



        }
        //We edit the Edges list for the edge that was split 
        var subEdgeList = t.Edges[_edgeIdx];
        var firstSubEdge = t.SubEdges.IndexOf((startIdx: _startIdx, idx));
        var secondSubEdge = t.SubEdges.IndexOf((idx, endIdx: _endIdx));
        if (firstSubEdge == -1 || secondSubEdge == -1)
        {
            throw new Exception("Bad state");
        }

        var node = subEdgeList.Find(subEdgeIdx);

        subEdgeList.AddAfter(node ?? throw new InvalidOperationException(), secondSubEdge);
        subEdgeList.AddAfter(node, firstSubEdge);
        subEdgeList.Remove(node);
        return idx;
    }
}