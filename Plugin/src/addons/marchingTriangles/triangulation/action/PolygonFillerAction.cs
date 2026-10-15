using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// Action that appends triangles to a given edge of a triangulation in order to reconstruct the surface of
/// a polygon implicitly defined by two edges. 
/// </summary>
/// This action does not support crossing points between edges. In that case use DegeneratePolygonFillerAction.
public class PolygonFillerAction : DelegatedTriangulationEditAction<int>
{
    private readonly List<Vector3> _e1;
    private readonly List<Vector3> _e2;

    private readonly int _edgeIndex;

    public PolygonFillerAction(List<(Vector3, int Value)> curEdge, List<(Vector3, int Value)> neighborEdge,
        int edgeIndex)
    {
        _edgeIndex = edgeIndex;
        _e1 = curEdge.Select(p => p.Item1).ToList();
        _e2 = neighborEdge.Select(p => p.Item1).Reverse().ToList();
    }

    protected override int DoApply(Triangulation triangulation)
    {
        //We use a front advancing algorithm to fill the polygon
        var c1 = 0;
        var c2 = 0;
        return DoFrontPropagation(triangulation, _e1,_e2,_edgeIndex,(0,_e1.Count), (0,_e2.Count));
    }

    public static int DoFrontPropagation(Triangulation triangulation,List<Vector3>_e1,List<Vector3>_e2, int _edgeIndex, (int s,int e)r1, (int s,int e)r2)
    {
        int c1 = r1.s;
        int c2 = r2.s;
        // We start the front by building the first triangle. We always choose the edited edge for that first step 
        var subEdgeIdx = triangulation.Edges[_edgeIndex].First!.Value;
        var newPoint = _e2[c2];
        var action = new AddTrianglesOnBorderSubEdge(_edgeIndex,subEdgeIdx, newPoint);
        action.Apply(triangulation);
        c1++;
        //
        while (!(c1 == r1.e-1 && c2 == r1.e-1))
        {
            //Pick which edge should advance by calculating angles
            (Vector3, Vector3) front = (_e1[c1], _e2[c2]);
            if (front.Item1.IsEqualApprox(front.Item2))
            {
                break;
            }
            //Compute the subedge index of the front in the triangulation
            //TODO : do not search but propagate it
            var p1 = triangulation.ReverseVertices[front.Item1];
            var p2 = triangulation.ReverseVertices[front.Item2];

            subEdgeIdx = triangulation.SubEdges[(p1, p2)];
            var frontVector = front.Item2 - front.Item1;
            int edgeToAdvance;
            if (c1 == _e1.Count-1)
            {
                edgeToAdvance = 2;
            }
            else if (c2 == _e2.Count-1)
            {
                edgeToAdvance = 1;
            }
            else
            {
                var advanceE1FrontVector = _e2[c2] - _e1[c1 + 1];
                var advanceE2FrontVector = _e2[c2 + 1] - _e1[c1];

                var advanceE1Angle = frontVector.AngleTo(advanceE1FrontVector);
                var advanceE2Angle = frontVector.AngleTo(advanceE2FrontVector);
                if (advanceE1Angle <= advanceE2Angle)
                {
                    edgeToAdvance = 1;
                }
                else
                {
                    edgeToAdvance = 2;
                }
            }

            //
            newPoint = edgeToAdvance == 1 ? _e1[c1 + 1] : _e2[c2 + 1];
            action = new AddTrianglesOnBorderSubEdge(_edgeIndex,subEdgeIdx, newPoint);
            action.Apply(triangulation);
            //
            if (edgeToAdvance == 1)
            {
                c1++;
            }
            else
            {
                c2++;
            }
        }
        return int.MaxValue;
    }
}