using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Action that Displaces a triangulation's edge along the Y axis.
/// The forceSnap parameter determines whether the points defined by the sub-edges of the
/// edges should
/// </summary>
/// <param name="forceSnap"></param>
public class DisplaceEdgeAlongYAxis : DelegatedTriangulationEditAction<int>
{
    private readonly int _edgeIdx;
    private readonly float _startPos;
    private readonly float _endPos;
    private readonly SnapMode _snap;

    public DisplaceEdgeAlongYAxis(int edgeIdx,
        float startPos,
        float endPos,
        SnapMode snap = SnapMode.SnapOnEdge)
    {
        _edgeIdx = edgeIdx;
        _startPos = startPos;
        _endPos = endPos;
        _snap = snap;
        if (edgeIdx < 0 || edgeIdx >= 3)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeIdx));
        }
    }

    protected override int DoApply(Triangulation t)
    {
        //Find the affected vertices
        LinkedList<int> affectedVertices = [];
        // Fetch first SubEdge's first vertex  and  last SubEdge's second vertex :
        var start = t.SubEdges.GetAt(t.Edges[_edgeIdx].First!.Value).Key.Item1;
        var end = t.SubEdges.GetAt(t.Edges[_edgeIdx].Last!.Value).Key.Item2;

        if (_snap == SnapMode.IgnoreSubEdges)
        {
            affectedVertices.AddFirst(start);
            affectedVertices.AddLast(end);
        }
        else
        {
            // fetch all the sub-edges
            var affectedEdges = t.Edges[_edgeIdx];

            foreach (var affectedEdge in affectedEdges)
            {
                affectedVertices.AddLast(t.SubEdges.GetAt(affectedEdge).Key.Item1);
                affectedVertices.AddLast(t.SubEdges.GetAt(affectedEdge).Key.Item2);
            }
        }

        // Apply the transformation 
        foreach (var affectedVertex in affectedVertices)
        {
            // Compute the displacement for each vertex
            float yDisplacement = ComputeDisplacement(t, affectedVertex, start, end, _startPos, _endPos, _snap);
            // Move the vertex accordingly 
            var action = new MovePointAlongYAxisAction(affectedVertex, yDisplacement);
            action.Apply(t);
        }

        return _edgeIdx;
    }

    private float ComputeDisplacement(Triangulation triangulation,
        int affectedVertex,
        int start,
        int end,
        float startPos,
        float endPos,
        SnapMode snapMode)
    {
        // Find the barycentric weight of the affected vertex on the [start,end] edge in the xOz plane
        var posP = triangulation.Vertices[affectedVertex];
        var posS = triangulation.Vertices[start];
        var posE = triangulation.Vertices[end];
        var pVec = new Vector2(posP.X - posS.X, posP.Z - posS.Z);
        var edge = new Vector2(posE.X - posS.X, posE.Z - posS.Z);
        var w = pVec.Length() / edge.Length();
        bool isBorderVtx = w == 0f || Math.Abs(w - 1f) < 1e-5;
        var initDiff = posP.Y - (posE.Y - posS.Y) * w;
        float height;
        if (snapMode == SnapMode.SnapOnEdge)
        {
            height = startPos + (endPos - startPos) * w;
        }
        else if (snapMode == SnapMode.IgnoreSubEdges)
        {
            height = isBorderVtx ? startPos + (endPos - startPos) * w : posP.Y;
        }
        else if (snapMode == SnapMode.PreserveSubVertexDistances)
        {
            height = startPos + (endPos - startPos) * w + (isBorderVtx ? 0 : initDiff);
        }
        else
        {
            throw new InvalidOperationException();
        }
        return height;
    }
}

/// <summary>
/// Enum of the DisplaceEdgeAction's behaviors
/// </summary>
public enum SnapMode
{
    /// <summary>
    /// The edge displacement is only applied on the first and last vertices of the Edge.
    /// </summary>
    IgnoreSubEdges,

    /// <summary>
    /// The displacement is applied on all sub vertices of the Edge.
    /// For intermediary vertices, the displacement height is "snapped" to the height of the segment
    /// created by the displaced extrema vertices .
    /// </summary>
    SnapOnEdge,

    /// <summary>
    /// The displacement is applied on all sub vertices of the Edge.
    /// For intermediary vertices, the distance to the edge is preserved.
    /// </summary>
    PreserveSubVertexDistances
}