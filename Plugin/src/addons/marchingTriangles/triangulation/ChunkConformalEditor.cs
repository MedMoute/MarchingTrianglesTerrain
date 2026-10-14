using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class ChunkConformalEditor
{
    /// <summary>
    /// The chunk on which the editor works
    /// </summary>
    private readonly HexagonalTerrainChunk _chunk;

#pragma warning disable CS0169 // Field is never used //TODO
    private readonly Func<Vector2I, Vector2I, HexagonalTerrainChunk?>? _neighborChunkProvider;
#pragma warning restore CS0169 // Field is never used

    ///
    /// Dictionaries of the vertex editors indexed by the dual indexing.
    /// Entries with Z = -1 are the editors of the cell centers
    ///
    private readonly Dictionary<Vector3I, VertexGeometryEditor> _cellVertexEditors = new();

    private readonly Dictionary<Vector3I, List<Vector3I>> _dataPointToCellSummitsMapping = new();

    private readonly Dictionary<Vector3I, List<Vector3I>> _dataPointNeighborsAsCellSummit = new();

    private readonly OrderedDictionary<(Vector3I, Vector3I), List<(int, int)>> _stitchedEdges =
        new(UnorderedV3TupleComparer.Instance);

    private readonly Dictionary<(Vector3I edgeStart, Vector3I edgeEnd),
        Dictionary<(int triId, int triIdx), (GeometryMode mode, (float, float) parameters)>> _collectedEdgeRequests =
        new();


    private readonly HashSet<(Vector3I start, Vector3I end)> _externalEdges = [];


    private readonly Dictionary<Vector3I, List<Vector3I>> _cellSummitsToDataPointMapping = new();


    // Dictionaries on cell indexes (hex summit grid)
    private readonly Dictionary<Vector2I, HexTerrainCell> _cells = new();
    private readonly Dictionary<Vector2I, Triangulation?[]?> _triangulationsPerCell = new();

    private readonly Dictionary<int, Triangulation> _triangulationsById = new();


    public ChunkConformalEditor(HexagonalTerrainChunk underlying)
    {
        _chunk = underlying;
        PrepareMappings();
    }

    public HexagonalTerrainChunk Chunk => _chunk;

    /// <summary>
    /// Pre-computation of the chunks relation mappings between cells from the HexGrid space and triangulations
    /// on the data grid
    /// </summary>
    private void PrepareMappings()
    {
        //TODO support cross chunk neighbors
        foreach (var cell in _chunk.TerrainDualGrid.CompleteCells)
        {
            if (!_cells.TryAdd(cell.CellCoordsImplicit, cell))
            {
                throw new InvalidOperationException($"The _cells dictionary already had the entry {cell}");
            }

            foreach (var cellIdxToVertex in cell.DualCellsMapping)
            {
                // Build vertexToCellMapping
                var exists =
                    _dataPointToCellSummitsMapping.TryGetValue(cellIdxToVertex.Value, out var vertexToCellList);
                if (!exists)
                {
                    vertexToCellList = [];
                    //Create the array holding the cells touching the vertex
                    _dataPointToCellSummitsMapping.Add(cellIdxToVertex.Value, vertexToCellList);
                }

                _dataPointToCellSummitsMapping[cellIdxToVertex.Value]
                    .Add(new Vector3I(cell.CellCoordsImplicit.X, cell.CellCoordsImplicit.Y, cellIdxToVertex.Key));
                if (_dataPointToCellSummitsMapping[cellIdxToVertex.Value].Count > 3)
                {
                    throw new InvalidOperationException("A vertex should only have up to 3 cells as neighbors");
                }

                // Build reverse mapping
                Vector3I cellSummitIndex = new Vector3I(cell.CellCoordsImplicit.X, cell.CellCoordsImplicit.Y,
                    cellIdxToVertex.Key);
                exists = _cellSummitsToDataPointMapping.TryGetValue(cellSummitIndex, out var cellSummitsToVertexList);
                if (!exists)
                {
                    cellSummitsToVertexList = [];
                    _cellSummitsToDataPointMapping.Add(cellSummitIndex, cellSummitsToVertexList);
                }

                _cellSummitsToDataPointMapping[cellSummitIndex].Add(cellIdxToVertex.Value);
                if (_cellSummitsToDataPointMapping[cellSummitIndex].Count > 6)
                {
                    throw new InvalidOperationException("");
                }

                //Build triangulations mapping
                exists = _triangulationsPerCell.TryGetValue(cell.CellCoordsImplicit, out var triangulationArray);
                if (!exists)
                {
                    triangulationArray = new Triangulation?[6];
                    _triangulationsPerCell.Add(cell.CellCoordsImplicit, triangulationArray);
                }

                var a2D = cell.CenterPosition;
                var b2D = cell.VertexPositionsInPlane[cellIdxToVertex.Key];
                var c2D = cell.VertexPositionsInPlane[
                    EngineUtils.Mod(cellIdxToVertex.Key + 1, HexTerrainCell.VertexCount)];

                var a = new Vector3(
                    (float)a2D.X,
                    cell.AverageHeight,
                    (float)a2D.Y);

                var b = new Vector3(
                    (float)b2D.X,
                    cell.GetVertexData!(cellIdxToVertex.Key),
                    (float)b2D.Y);

                var c = new Vector3(
                    (float)c2D.X,
                    cell.GetVertexData!(EngineUtils.Mod(cellIdxToVertex.Key + 1, HexTerrainCell.VertexCount)),
                    (float)c2D.Y);

                var tri = new Triangulation([a, b, c], false, true);

                triangulationArray![cellIdxToVertex.Key] ??= tri;
                _triangulationsById.Add(tri.Id, tri);


                //Build data point neighbor maps as we know the cell summits (cellIdxToVertex-1) , (cellIdxToVertex+1)
                //and are neighbors of the current cell summit,
                exists = _dataPointNeighborsAsCellSummit.TryGetValue(cellIdxToVertex.Value, out var dataPointNeighbors);
                if (!exists || dataPointNeighbors == null)
                {
                    dataPointNeighbors = new List<Vector3I>();
                    _dataPointNeighborsAsCellSummit.Add(cellIdxToVertex.Value, dataPointNeighbors);
                }

                var previous = new Vector3I(
                    cell.CellCoordsImplicit.X,
                    cell.CellCoordsImplicit.Y,
                    EngineUtils.Mod(cellIdxToVertex.Key - 1, HexTerrainCell.VertexCount));
                var next = new Vector3I(
                    cell.CellCoordsImplicit.X,
                    cell.CellCoordsImplicit.Y,
                    EngineUtils.Mod(cellIdxToVertex.Key + 1, HexTerrainCell.VertexCount));

                dataPointNeighbors.Add(previous);
                dataPointNeighbors.Add(next);

                _externalEdges.Add((cellSummitIndex, previous));
                _externalEdges.Add((cellSummitIndex, next));
            }
        }

        if (true) //DEBUG CONSISTENCY CHECK
        {
            foreach (var keyValuePair in _dataPointToCellSummitsMapping)
            {
                var pos = _chunk.DataGrid.OrientationSystem.GetCellCentroid(keyValuePair.Key);
                if (keyValuePair.Value.Any(t =>
                        (_cells[new Vector2I(t.X, t.Y)].VertexPositionsInPlane[t.Z] - pos).Length > 1e-5))
                {
                    throw new Exception(
                        $"Inconsistent mapping data at [{keyValuePair.Key}]=>[{String.Join(";", keyValuePair.Value)}]");
                }
            }
        }
    }


    /// <summary>
    /// Returns the dictionary of mesh triangles, indexed by the cell it's originating from.  
    /// </summary>
    /// <returns></returns>
    public Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> GetTriangleInfos()
    {
        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();
        foreach (var triangulation in _triangulationsPerCell)
        {
            if (triangulation.Value == null)
            {
                continue;
            }

            // Find the cell
            var cell = _cells[triangulation.Key];
            output.TryAdd(cell, []);
            foreach (var tuple in triangulation.Value)
            {
                if (tuple == null)
                {
                    continue;
                }

                //Process the triangulation and fetch the triangles
                output[cell].AddRange(tuple.ToTriangleInfoList());
            }
        }

        return output;
    }

    public void ApplyGeometryOperations()
    {
        foreach (var geometryEdit in _cellVertexEditors.Values)
        {
            geometryEdit.ApplyTransformations();
        }
    }

    //Collects the geometry behaviour of all the vertices of the full cells of the chunk 
    public void CollectAllOperationsRequests()
    {
        //Enumerate the external edges
        var internalEdges = new HashSet<(Vector3I start, Vector3I end)>();

        //We instantiate all the cell summit editors
        foreach (var vertex in _cellSummitsToDataPointMapping.Keys)
        {
            _cellVertexEditors.Add(vertex, new VertexGeometryEditor());
        }

        //Cell centers are indexed with their cell index and -1 for Z value.
        foreach (var cell in _cells.Values)
        {
            var centerVertex = new Vector3I(
                cell.CellCoordsImplicit.X,
                cell.CellCoordsImplicit.Y,
                -1);
            _cellVertexEditors.Add(centerVertex, new VertexGeometryEditor());
            //Use the cell iteration to add the iternal cells to the edges
            for (int i = 0; i < HexTerrainCell.VertexCount; i++)
            {
                internalEdges.Add((centerVertex,
                    new Vector3I(cell.CellCoordsImplicit.X, cell.CellCoordsImplicit.Y, i)));
            }
        }


        //We obtain all the edges
        var edges = internalEdges.Concat(_externalEdges);

        foreach (var edge in edges)
        {
            var eStart = edge.start;
            var eEnd = edge.end;

            HashSet<Triangulation?> edgeTris = new();

            var cellStart = _cells[new Vector2I(eStart.X, eStart.Y)];
            var cellEnd = _cells[new Vector2I(eEnd.X, eEnd.Y)];

            var startPos = eStart.Z == -1
                ? new Vector2(
                    (float)cellStart.CenterPosition.X,
                    (float)cellStart.CenterPosition.Y)
                : new Vector2(
                    (float)cellStart.VertexPositionsInPlane[eStart.Z].X,
                    (float)cellStart.VertexPositionsInPlane[eStart.Z].Y);

            var endPos = eEnd.Z == -1
                ? new Vector2(
                    (float)cellEnd.CenterPosition.X,
                    (float)cellEnd.CenterPosition.Y)
                : new Vector2(
                    (float)cellEnd.VertexPositionsInPlane[eEnd.Z].X,
                    (float)cellEnd.VertexPositionsInPlane[eEnd.Z].Y);

            if (eStart.Z != -1)
            {
                for (int offset = 0; offset < 2; offset++)
                {
                    var tri = _triangulationsPerCell[new Vector2I(eStart.X, eStart.Y)]?[EngineUtils
                        .Mod(eStart.Z - offset, HexTerrainCell.VertexCount)];
                    if (tri != null &&
                        tri.SourceTriangle.Select(v => new Vector2(v.X, v.Z))
                            .Any(v => v.Equals(startPos)) &&
                        tri.SourceTriangle.Select(v => new Vector2(v.X, v.Z))
                            .Any(v => v.Equals(endPos)))
                    {
                        edgeTris.Add(_triangulationsPerCell[new Vector2I(eStart.X, eStart.Y)]?[EngineUtils
                            .Mod(eStart.Z - offset, HexTerrainCell.VertexCount)]);
                    }
                }
            }

            if (eEnd.Z != -1)
            {
                for (int offset = 0; offset < 2; offset++)
                {
                    var tri = _triangulationsPerCell[new Vector2I(eEnd.X, eEnd.Y)]?[EngineUtils
                        .Mod(eEnd.Z - offset, HexTerrainCell.VertexCount)];
                    if (tri != null &&
                        tri.SourceTriangle.Select(v => new Vector2(v.X, v.Z))
                            .Any(v => v.Equals(startPos)) &&
                        tri.SourceTriangle.Select(v => new Vector2(v.X, v.Z))
                            .Any(v => v.Equals(endPos)))
                    {
                        edgeTris.Add(_triangulationsPerCell[new Vector2I(eEnd.X, eEnd.Y)]?[EngineUtils
                            .Mod(eEnd.Z - offset, HexTerrainCell.VertexCount)]);
                    }
                }
            }

            foreach (var triangulation in edgeTris.Where(tri => tri != null))
            {
                //Register the local edition of both points on the edge
                var indexOfCellSummitInTri = GetCellSummitIndexInTriangulation(
                    eStart,
                    triangulation,
                    cellStart);

                var request = GetCellSummitGeometryRequest(
                    new Vector2I(eStart.X, eStart.Y),
                    triangulation,
                    indexOfCellSummitInTri);

                var edgeData = new Dictionary<(int, int), (GeometryMode, (float, float))>();

                Debug.Assert(triangulation != null, nameof(marchingTriangles.triangulation) + " != null");
                var nextPointInTri = triangulation.SourceTriangle[(indexOfCellSummitInTri + 1) % 3];
                bool sameOrdered = endPos.IsEqualApprox(new Vector2(nextPointInTri.X, nextPointInTri.Z));

                var success = _collectedEdgeRequests.TryAdd(edge, edgeData);
                if (!success)
                {
                    edgeData = _collectedEdgeRequests[edge];
                }

                int RevertTriIndex(int idx)
                {
                    switch (idx)
                    {
                        case 0: return 2;
                        case 1: return 1;
                        case 2: return 0;
                        default: throw new NotSupportedException();
                    }
                }

                Debug.Assert(triangulation != null, nameof(triangulation) + " != null");
                edgeData.Add((
                        triangulation.Id,
                        sameOrdered ? indexOfCellSummitInTri : RevertTriIndex(indexOfCellSummitInTri)),
                    request);
            }
        }
    }

    private static int GetCellSummitIndexInTriangulation(Vector3I cellSummit,
        Triangulation? triangulation,
        HexTerrainCell cell)
    {
        if (triangulation == null)
        {
            throw new ArgumentException();
        }

        int indexOfCellSummitInTri;

        if (cellSummit.Z == -1)
        {
            indexOfCellSummitInTri = 0;
        }
        else
        {
            var posIdx1InTriangulation = triangulation.SourceTriangle[1];
            var pos1 = new Vector2(posIdx1InTriangulation.X, posIdx1InTriangulation.Z);

            var posCellSummit = cell.VertexPositionsInPlane[cellSummit.Z];
            var posVertex = new Vector2((float)posCellSummit.X, (float)posCellSummit.Y);

            if (posVertex.IsEqualApprox(pos1))
            {
                indexOfCellSummitInTri = 1;
            }
            else
            {
                indexOfCellSummitInTri = 2;
            }
        }

        Debug.Assert(cell.GetVertexData != null);
        Vector3 position = cellSummit.Z == -1
            ? new Vector3((float)cell.CenterPosition.X,
                cell.AverageHeight,
                (float)cell.CenterPosition.Y)
            : new Vector3(
                (float)cell.VertexPositionsInPlane[cellSummit.Z].X,
                cell.GetVertexData(cellSummit.Z),
                (float)cell.VertexPositionsInPlane[cellSummit.Z].Y);
        if (!triangulation.SourceTriangle[indexOfCellSummitInTri].IsEqualApprox(position))
        {
            Console.WriteLine("Shoould have found" + position + "in" + String.Join(",", triangulation.SourceTriangle));
            throw new ConstraintException("Algo is wrong");
        }

        return indexOfCellSummitInTri;
    }

    private (GeometryMode, (float, float)) GetCellSummitGeometryRequest(Vector2I cellIdx,
        Triangulation? triangulation,
        int triangleIndex)
    {
        if (triangulation is null)
        {
            {
                throw new ArgumentException("missing triangulation", nameof(triangulation));
            }
        }

        var cell = _cells[cellIdx];

        var requestedGeometryMode =
            cell.GeometryModesOverride ?? _chunk.DefaultGeometryModes ?? throw new ConstraintException();
        var requestedParameters =
            cell.ParametersOverride ?? _chunk.GeometryModeParameters ?? throw new ConstraintException();


        var mask1 = triangulation.ComputeMask(_chunk.DefaultThreshold);
        bool t1IsOverThreshold = (mask1 & (1 << triangleIndex)) != 0;

        return t1IsOverThreshold
            ? (requestedGeometryMode.Item1, requestedParameters.Item1)
            : (requestedGeometryMode.Item2, requestedParameters.Item2);
    }

    public void CollectBorderOperationRequests()
    {
        //throw new NotImplementedException();
    }

    /// <summary>
    /// Registers all the geometry operations based of the processed edge requests. 
    /// </summary>
    public void RegisterOperations()
    {
        foreach (var edgeRequestDico in _collectedEdgeRequests)
        {
            var edge = edgeRequestDico.Key;
            var editionRequests = edgeRequestDico.Value;

            //Console.WriteLine($"[Register] : {edge.edgeStart}=>{edge.edgeEnd}");

            //Fetch all the other cells that are on the same edge
            var neighborCells = new List<Vector2I>();
            var startNeighborCells = new List<Vector2I>();
            var endNeighborCells = new List<Vector2I>();
            if (!(edge.edgeStart.Z == -1 || edge.edgeEnd.Z == -1)) //if Z=-1 => cell interior, no neighbor
            {
                _cellSummitsToDataPointMapping.TryGetValue(edge.edgeStart, out var pointsOfStart);
                _dataPointNeighborsAsCellSummit.TryGetValue(
                    (pointsOfStart ?? throw new InvalidOperationException()).First(), out var startNeighborSubCells);
                startNeighborCells.AddRange((startNeighborSubCells ?? throw new InvalidOperationException())
                    .Select(v => new Vector2I(v.X, v.Y)).Distinct());

                _cellSummitsToDataPointMapping.TryGetValue(edge.edgeEnd, out var pointsOfEnd);
                _dataPointNeighborsAsCellSummit.TryGetValue(
                    (pointsOfEnd ?? throw new InvalidOperationException()).First(), out var endNeighborSubCells);
                endNeighborCells.AddRange((endNeighborSubCells ?? throw new InvalidOperationException())
                    .Select(v => new Vector2I(v.X, v.Y)).Distinct());

                neighborCells.AddRange(startNeighborCells.Intersect(endNeighborCells));
            }
            else
            {
                neighborCells.Add(new Vector2I(edge.edgeStart.X, edge.edgeStart.Y));
            }


            (GeometryMode mode, (float, float) parameters) effectiveEdgeAction =
                //Find which operation we should effectively apply on the edge  
                editionRequests.Values.Distinct().Count() == 1
                    ? editionRequests.Values.First() //only one type, use it
                    : SelectEffectiveAction(editionRequests, edge); // select it otherwise

            foreach (var editionRequest in editionRequests)
            {
                var triangleData = editionRequest.Key;
                var effectiveRequest = editionRequest.Value;
                Console.WriteLine($"     T[#{triangleData.triId};{triangleData.triIdx}] :" +
                                  $" =>  <{effectiveRequest.mode},{effectiveRequest.parameters}>" +
                                  $" => <{effectiveEdgeAction.mode},{effectiveEdgeAction.parameters}>");

                var startIdx = new Vector2I(edge.edgeStart.X, edge.edgeStart.Y);
                if (startIdx != new Vector2I(edge.edgeEnd.X, edge.edgeEnd.Y))
                {
                    throw new InvalidOperationException();
                }

                _cellVertexEditors[edge.edgeStart].RegisterLocalEdgeAction(
                    _cells[startIdx],
                    (edge.edgeStart.Z, edge.edgeEnd.Z),
                    _triangulationsById[triangleData.triId],
                    triangleData.triIdx,
                    neighborCells.Select(c => _cells[c]).ToList(),
                    effectiveEdgeAction);
            }
        }
    }

    private (GeometryMode, (float, float)) 
        SelectEffectiveAction(
        Dictionary<(int, int), (GeometryMode mode, (float, float) parameters)> requests,
        (Vector3I start, Vector3I end) edge)
    {
        var cellStart = _cells[new Vector2I(edge.start.X,edge.start.Y)];
        var cellEnd = _cells[new Vector2I(edge.end.X,edge.end.Y)];

        if (cellStart != cellEnd)
        {
            throw new ArgumentException();
        }

        var posStart = edge.start.Z == -1
            ? new Vector2((float)cellStart.CenterPosition.X, (float)cellStart.CenterPosition.Y)
            : new Vector2((float)cellStart.VertexPositionsInPlane[edge.start.Z].X,
                (float)cellStart.VertexPositionsInPlane[edge.start.Z].Y);
        var posEnd = edge.end.Z == -1
            ? new Vector2((float)cellStart.CenterPosition.X, (float)cellStart.CenterPosition.Y)
            : new Vector2((float)cellStart.VertexPositionsInPlane[edge.end.Z].X,
                (float)cellStart.VertexPositionsInPlane[edge.end.Z].Y);      
        
        //Pre -checks (Debug only)
        (Vector2, Vector2) GetEdgeExtremitiesInTriangulation((int, int) valueTuple)
        {
            var tri = _triangulationsById[valueTuple.Item1];

            var firstNode = tri.Edges[valueTuple.Item2].First;
            var lastNode = tri.Edges[valueTuple.Item2].Last;

            if (firstNode == null || lastNode == null) throw new InvalidOperationException();
            var startEdgeIdx = firstNode.Value;
            var endEdgeIdx = lastNode.Value;
            var startPos = tri.Vertices[tri.SubEdges.ElementAt(startEdgeIdx).Key.Item1];
            var endPos = tri.Vertices[tri.SubEdges.ElementAt(endEdgeIdx).Key.Item2];
            return (new Vector2(startPos.X,startPos.Z), new Vector2(endPos.X,endPos.Z));
        }

        foreach (var valueTuple in requests.Keys)
        {
            var p = GetEdgeExtremitiesInTriangulation(valueTuple);
            var pairMatches = p.Item1.IsEqualApprox(posStart) ^ p.Item1.IsEqualApprox(posEnd) &&
                              p.Item2.IsEqualApprox(posStart) ^ p.Item2.IsEqualApprox(posEnd);
            if (!pairMatches)
            {
                throw new InvalidOperationException();
            }
        }
        
        
        
        var mode = requests.Values.Select(r => r.mode).Min();
        var param1 = requests.Values.Select(r => r.parameters.Item1).Min();
        var param2 = requests.Values.Select(r => r.parameters.Item2).Min();


        var success = _stitchedEdges.TryGetValue((edge.start, edge.end), out var listOfStitchedEdges);
        if (!success || listOfStitchedEdges == null)
        {
            listOfStitchedEdges = new List<(int, int)>();
            _stitchedEdges[(edge.start, edge.end)] = listOfStitchedEdges;
        }

        listOfStitchedEdges.AddRange(requests.Keys);

        return (mode, (param1, param2));
    }

    public void StitchEdges()
    {
        foreach (var stitchRequest in _stitchedEdges.Values)
        {
            (Vector3, Vector3) GetEdgeExtremitiesInTriangulation((int, int) valueTuple)
            {
                var tri = _triangulationsById[valueTuple.Item1];

                var firstNode = tri.Edges[valueTuple.Item2].First;
                var lastNode = tri.Edges[valueTuple.Item2].Last;

                if (firstNode == null || lastNode == null) throw new InvalidOperationException();
                var startEdgeIdx = firstNode.Value;
                var endEdgeIdx = lastNode.Value;
                var startPos = tri.Vertices[tri.SubEdges.ElementAt(startEdgeIdx).Key.Item1];
                var endPos = tri.Vertices[tri.SubEdges.ElementAt(endEdgeIdx).Key.Item2];
                return (startPos, endPos);
            }

            var init = stitchRequest.First();
            var posInitPair = GetEdgeExtremitiesInTriangulation(init);
            var posXoZInitPair = (
                new Vector2(posInitPair.Item1.X, posInitPair.Item1.Z),
                new Vector2(posInitPair.Item2.X, posInitPair.Item2.Z));
            var sameExtremitiesInXoZPlane = stitchRequest.Select(GetEdgeExtremitiesInTriangulation)
                .Select(pair => (
                    new Vector2(pair.Item1.X, pair.Item1.Z),
                    new Vector2(pair.Item2.X, pair.Item2.Z))).All(p =>
                    (p.Item1.IsEqualApprox(posXoZInitPair.Item1) ^ p.Item1.IsEqualApprox(posXoZInitPair.Item2)) &&
                    (p.Item2.IsEqualApprox(posXoZInitPair.Item1) ^ p.Item2.IsEqualApprox(posXoZInitPair.Item2)));
            if (!sameExtremitiesInXoZPlane)
            {
                throw new InvalidOperationException();
            }
        }
        //  throw new NotImplementedException();
    }
}