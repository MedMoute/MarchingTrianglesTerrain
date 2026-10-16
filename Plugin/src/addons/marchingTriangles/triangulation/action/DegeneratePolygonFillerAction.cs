using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

/// <summary>
/// Action that appends triangles to a given edge of a triangulation in order to reconstruct the surface of
/// a polygon implicitly defined by two edges. 
/// </summary>
public class DegeneratePolygonFillerAction : DelegatedTriangulationEditAction<int>
{
    private readonly List<(Vector3, int Value)> _curEdge;
    private readonly List<(Vector3, int Value)> _neighborEdge;
    private readonly int _edgeIndex;
    private readonly int _crossingPointsCount;
    private readonly List<(Vector3, (int Value, float weight) intersectionRepresentation)> _crossingPoints;

    private bool _unsupportedOpFlag = false;
    
    public DegeneratePolygonFillerAction(
        List<(Vector3, int Value)> curEdge,
        int edge,
        List<(Vector3, int Value)> neighborEdge,
        List<(Vector3, (int Value, float weight) intersectionRepresentation)> crossingPoints)
    {
        _curEdge = curEdge;
        _neighborEdge = neighborEdge;
        neighborEdge.Reverse();
        _edgeIndex = edge;
        _crossingPoints = crossingPoints;
        _crossingPointsCount = _crossingPoints.Count;
    }

    protected override void ValidateBefore(Triangulation t)
    {
        base.ValidateBefore(t);
        if (_crossingPointsCount == 0 )
        {
            throw new Exception(
                "There are no edge crossing points for this triangulation." +
                " You should use PolygonFillerAction here instead");
        }

        if (_crossingPointsCount == 1) //If there is a single crossing point,
                                  //it must be at one (or both) edge's extremity otherwise it's a knot point (not  supported  )
        {
            if (!(_curEdge.Select(p => p.Item1).First().IsEqualApprox(_crossingPoints[0].Item1) ||
                  _curEdge.Select(p => p.Item1).Last().IsEqualApprox(_crossingPoints[0].Item1) ||
                  _neighborEdge.Select(p => p.Item1).First().IsEqualApprox(_crossingPoints[0].Item1) ||
                  _neighborEdge.Select(p => p.Item1).Last().IsEqualApprox(_crossingPoints[0].Item1)))
            {
                _unsupportedOpFlag = true;
                Console.WriteLine("Knot point not supported.");
            }
        }
    }

    protected override int DoApply(Triangulation t)
    {
        if (_unsupportedOpFlag) return int.MinValue;

        //Get the indexes of the crossing vertices in both edges
        List<int> idx1 = new(); 
        List<int> idx2 = new();

        
        _crossingPoints.ForEach(p=>  idx1.Add(
            _curEdge.FindIndex(edgeVertex => edgeVertex.Item1.IsEqualApprox(p.Item1))));
        _crossingPoints.ForEach(p=>  idx2.Add(
            _neighborEdge.FindIndex(edgeVertex => edgeVertex.Item1.IsEqualApprox(p.Item1))));

        if (idx1.Count == 1 && idx2.Count == 1)
        {
            idx1.Add(idx1.Min() != 0 ? 0 : _curEdge.Count);
            idx2.Add(idx2.Min() != 0 ? 0 : _neighborEdge.Count);
        }
        //We obtain the bounding indexes of the front propagation
        idx1.Sort();
        idx2.Sort();
        if (idx1.Count != 2 || idx2.Count != 2) throw new Exception();
        
        //Execute the front propagation algo on the range values
        PolygonFillerAction.DoFrontPropagation(
            t,
            _curEdge.Select(v=> v.Item1).ToList(),
            _neighborEdge.Select(v=> v.Item1).ToList(),
            _edgeIndex,
            (idx1.First(),idx1.Last()),
            (idx2.First(),idx2.Last()));
        

        return int.MaxValue;
    }
}