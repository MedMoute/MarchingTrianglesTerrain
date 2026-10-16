using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
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

    private readonly Dictionary<(Vector3I, Vector3I), VertexGeometryEditor> _cellEdgeEditors =
        new();


    private readonly Dictionary<Vector3I, List<Vector3I>> _dataPointToCellSummitsMapping = new();

    private readonly Dictionary<Vector3I, List<Vector3I>> _dataPointNeighborsAsCellSummit = new();

    private readonly Dictionary<(Vector3I, Vector3I), List<(int, int)>> _stitchedEdges =
        new();

    private readonly OrderedDictionary<(Vector3I start, Vector3I end),
        Dictionary<(int triId, int edgeIdx), (GeometryMode mode, (float, float) parameters)>> _collectedEdgeRequests =
        new(UnorderedV3TupleComparer.Instance);

    private readonly HashSet<(Vector3I start, Vector3I end)> _externalEdges = new(UnorderedV3TupleComparer.Instance);


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
    public Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> GetTriangleInfos(
        List<Vector2I>? cellIndices = null)
    {
        if (cellIndices == null)
        {
            cellIndices = [.. _triangulationsPerCell.Keys];
        }

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();
        foreach (var cellIndex in cellIndices)
        {
            var cellTriangulations = _triangulationsPerCell[cellIndex];
            if (cellTriangulations == null)
            {
                continue;
            }

            // Find the cell
            var cell = _cells[cellIndex];
            output.TryAdd(cell, []);
            foreach (var tuple in cellTriangulations)
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
        foreach (var geometryEdit in _cellEdgeEditors.Values)
        {
            geometryEdit.ApplyAllTransformations();
        }
    }

    //Collects the geometry behaviour of all the vertices of the full cells of the chunk 
    public void CollectAllOperationsRequests()
    {
        //Enumerate the external edges
        var internalEdges = new HashSet<(Vector3I start, Vector3I end)>(UnorderedV3TupleComparer.Instance);

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

            if (!_cellEdgeEditors.TryGetValue(edge, out var editor))
            {
                editor = new VertexGeometryEditor();
                _cellEdgeEditors.Add(edge, editor);
            }

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

            int IndexOfEdgeInTri(int a, int b)
            {
                switch (a, b)
                {
                    case (1, 0):
                    case (0, 1):
                        return 0;
                    case (1, 2):
                    case (2, 1):
                        return 1;
                    case (0, 2):
                    case (2, 0):
                        return 2;
                    default:
                        throw new NotSupportedException();
                }
            }

            foreach (var triangulation in edgeTris.Where(tri => tri != null))
            {
                //Register the local edition of both points on the edge
                var indexOfCellStartSummitInTri = GetCellSummitIndexInTriangulation(
                    eStart,
                    triangulation,
                    cellStart);

                var indexOfCellEndSummitInTri = GetCellSummitIndexInTriangulation(
                    eEnd,
                    triangulation,
                    cellStart);

                var edgeIdx = IndexOfEdgeInTri(indexOfCellStartSummitInTri, indexOfCellEndSummitInTri);

                var request = GetCellSummitGeometryRequest(
                    new Vector2I(eStart.X, eStart.Y),
                    triangulation,
                    edgeIdx);

                var edgeData = new Dictionary<(int, int), (GeometryMode, (float, float))>();
                var success = _collectedEdgeRequests.TryAdd(edge, edgeData);
                if (!success)
                {
                    edgeData = _collectedEdgeRequests[edge];
                }

                edgeData.Add((triangulation!.Id, edgeIdx), request);
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

            //Console.WriteLine($"[Register] : {edge.start}=>{edge.end}");

            //Fetch all the other cells that are on the same edge
            var neighborCells = new List<Vector2I>();
            var startNeighborCells = new List<Vector2I>();
            var endNeighborCells = new List<Vector2I>();
            if (!(edge.start.Z == -1 || edge.end.Z == -1)) //if Z=-1 => cell interior, no neighbor
            {
                _cellSummitsToDataPointMapping.TryGetValue(edge.start, out var pointsOfStart);
                _dataPointNeighborsAsCellSummit.TryGetValue(
                    (pointsOfStart ?? throw new InvalidOperationException()).First(), out var startNeighborSubCells);
                startNeighborCells.AddRange((startNeighborSubCells ?? throw new InvalidOperationException())
                    .Select(v => new Vector2I(v.X, v.Y)).Distinct());

                _cellSummitsToDataPointMapping.TryGetValue(edge.end, out var pointsOfEnd);
                _dataPointNeighborsAsCellSummit.TryGetValue(
                    (pointsOfEnd ?? throw new InvalidOperationException()).First(), out var endNeighborSubCells);
                endNeighborCells.AddRange((endNeighborSubCells ?? throw new InvalidOperationException())
                    .Select(v => new Vector2I(v.X, v.Y)).Distinct());

                neighborCells.AddRange(startNeighborCells.Intersect(endNeighborCells));
            }
            else
            {
                neighborCells.Add(new Vector2I(edge.start.X, edge.start.Y));
            }


            (GeometryMode mode, (float, float) parameters) effectiveEdgeAction =
                //Find which operation we should effectively apply on the edge  
                editionRequests.Values.Distinct().Count() == 1
                    ? editionRequests.Values.First() //only one type, use it
                    : SelectEffectiveAction(editionRequests, edge); // select it otherwise

            foreach (var editionRequest in editionRequests)
            {
                var triangleData = editionRequest.Key;
                var startIdx = new Vector2I(edge.start.X, edge.start.Y);
                if (startIdx != new Vector2I(edge.end.X, edge.end.Y))
                {
                    throw new InvalidOperationException();
                }

                _cellEdgeEditors[(edge.start, edge.end)].RegisterLocalActions(
                    _cells[startIdx],
                    (edge.start.Z, edge.end.Z),
                    _triangulationsById[triangleData.triId],
                    triangleData.edgeIdx,
                    neighborCells.Select(c => _cells[c]).Where(c => c != _cells[startIdx]).ToList(),
                    effectiveEdgeAction);
            }
        }
    }

    private (GeometryMode, (float, float)) SelectEffectiveAction(
        Dictionary<(int, int), (GeometryMode mode, (float, float) parameters)> requests,
        (Vector3I start, Vector3I end) edge)
    {
        var cellStart = _cells[new Vector2I(edge.start.X, edge.start.Y)];
        var cellEnd = _cells[new Vector2I(edge.end.X, edge.end.Y)];

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
            return (new Vector2(startPos.X, startPos.Z), new Vector2(endPos.X, endPos.Z));
        }
    }


    /// <summary>
    /// Adds the missing triangles to make the chunk triangulation conformal if needed. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void StitchEdges()
    {
        //We process the edge requests to find which VERTICES have different requests
        OrderedDictionary<Vector3I,
                Dictionary<(int triId, int edge), List<(GeometryMode mode, (float, float) parameters)>>>
            _collectedVertexRequests = new();

        foreach (var kvp in _collectedEdgeRequests)
        {
            var edge = kvp.Key;
            var cellIdx = new Vector2I(edge.start.X, edge.start.Y);
            Debug.Assert(cellIdx == new Vector2I(edge.end.X, edge.end.Y));

            var cell = _cells[cellIdx];

            foreach (var request in kvp.Value)
            {
                int vertexInTri;
                var _edge = (edge.start.Z, edge.end.Z);
                var tri = _triangulationsById[request.Key.triId];
                var edgeIdx = request.Key.edgeIdx;


                //if (GeometryModeActions.IsEdgeOrdered(cell, _edge, tri, edgeIdx))
                {
                    vertexInTri = VertexGeometryEditor.GetEdgeStartVertexIndex(cell, _edge, tri, edgeIdx);

                    if (!_collectedVertexRequests.TryGetValue(edge.start, out var requests))
                    {
                        requests = new();
                        _collectedVertexRequests.Add(edge.start, requests);
                    }

                    foreach (var keyValuePair in kvp.Value)
                    {
                        if (!requests.TryGetValue((keyValuePair.Key.triId, vertexInTri), out var requestList))
                        {
                            requestList = new List<(GeometryMode mode, (float, float) parameters)>();
                            requests.Add((keyValuePair.Key.triId, vertexInTri), requestList);
                        }

                        requestList.Add(keyValuePair.Value);
                    }
                }
                //else
                {
                    vertexInTri = VertexGeometryEditor.GetEdgeStartVertexIndex(cell, _edge, tri, edgeIdx);

                    if (!_collectedVertexRequests.TryGetValue(edge.end, out var requests))
                    {
                        requests = new();
                        _collectedVertexRequests.Add(edge.end, requests);
                    }

                    foreach (var entry in kvp.Value)
                    {
                        if (!requests.TryGetValue((entry.Key.triId, vertexInTri), out var requestList))
                        {
                            requestList = new List<(GeometryMode mode, (float, float) parameters)>();
                            requests.Add((entry.Key.triId, vertexInTri), requestList);
                        }

                        requestList.Add(entry.Value);
                    }
                }
            }
        }

        var distinctRequests =
            new Dictionary<(Vector3I cellIdx, (int triId, int edge) triIdx),
                List<(GeometryMode mode, (float, float) parameters)>>();

        foreach (var collectedVertexRequest in _collectedVertexRequests)
        {
            foreach (var requestsPerTriangulation in collectedVertexRequest.Value)
            {
                var newKey = (collectedVertexRequest.Key, requestsPerTriangulation.Key);
                var distinct = requestsPerTriangulation.Value.Distinct().ToList();
                if (distinct.Count > 1)
                {
                    distinctRequests[newKey] = distinct;
                }
            }
        }

        //Now we're finally stitchin some stuff
        foreach (var distinctRequest in distinctRequests)
        {
            var cellIdx = new Vector2I(distinctRequest.Key.cellIdx.X, distinctRequest.Key.cellIdx.Y);
            var summitIdx = distinctRequest.Key.cellIdx.Z;
            var triIdx = distinctRequest.Key.triIdx;

            if (triIdx.edge == 0 && summitIdx == -1) //Easy case : inner cell triangulations
            {
                StitchInnerCellEdge(_cells[cellIdx], summitIdx, _triangulationsById[triIdx.triId], triIdx.edge);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    private void StitchInnerCellEdge(HexTerrainCell cell, int summitIdx, Triangulation triangulation, int triIdxEdge)
    {
        int cellIdx;
        if (summitIdx == -1)
        {
            cellIdx = _triangulationsPerCell[cell.CellCoordsImplicit]?
                          .Index()
                          .Where(t => t.Item == triangulation)
                          .Select(t => t.Index).First()
                      ?? throw new ArgumentException(
                          "Triangulation cannot be found in the sub cells of the provided cell");
        }
        else
        {
            cellIdx = summitIdx;
        }

        //Fetch the neighbor cell index
        var neighborSubCellIndex = GetNeighborSubCellIndex(cellIdx);

        var neighborTri = _triangulationsPerCell[cell.CellCoordsImplicit]?[neighborSubCellIndex];
        if (neighborTri == null) return;
        var neighborEdgeIdx = GetNeighborEdgeIndex();

        var neighborEdge = ExplicitEdge(neighborTri.Edges[neighborEdgeIdx], neighborTri);
        var curEdge = ExplicitEdge(triangulation.Edges[triIdxEdge], triangulation);

        //Ensure start point and end point are similar in the XoZ plane
        // If that is the case, the polygon to cover will be easier to obtain
        var invert = CheckExtremitiesInXoZPlane(neighborEdge, curEdge);
        if (!invert) throw new NotImplementedException(); //the edges go in the "same" order,
        //triangulating might create a flipped triangle
        //Check if the edges are identical.
        if (neighborEdge.Count == curEdge.Count)
        {
            bool equivalentEdges = true;
            for (int i = 0; i < neighborEdge.Count; i++)
            {
                if (!neighborEdge[^(i + 1)].Item1.IsEqualApprox(curEdge[i].Item1))
                {
                    equivalentEdges = false;
                    break;
                }
            }

            if (equivalentEdges) //The neighboring edges are equivalent
            {
                return;
            }
        }

        //We now reconstruct a mesh between the edges using the provided triangulation
        TriangleUtils.BuildPolygonTriangulation(
            new TriangleUtils.PolygonTriangulationBuilderData(
                _cellVertexEditors[new Vector3I(cell.CellCoordsImplicit.X, cell.CellCoordsImplicit.Y, summitIdx)],
                triangulation,
                curEdge,
                triIdxEdge),
            new TriangleUtils.PolygonTriangulationBuilderData(
                _cellVertexEditors[new Vector3I(cell.CellCoordsImplicit.X, cell.CellCoordsImplicit.Y, summitIdx)],
                neighborTri,
                neighborEdge,
                neighborEdgeIdx));


        return;

        int GetNeighborSubCellIndex(int cellSummitIdx)
        {
            return triIdxEdge switch
            {
                0 => EngineUtils.Mod(cellSummitIdx - 1, HexTerrainCell.VertexCount),
                2 => EngineUtils.Mod(cellSummitIdx + 1, HexTerrainCell.VertexCount),
                _ => throw new NotSupportedException()
            };
        }

        int GetNeighborEdgeIndex()
        {
            return triIdxEdge switch
            {
                0 => 2,
                2 => 0,
                _ => throw new NotSupportedException()
            };
        }


        bool CheckExtremitiesInXoZPlane(List<(Vector3, int)> nEdge, List<(Vector3, int)> cEdge)
        {
            var nFirst = nEdge.First().Item1;
            var nLast = nEdge.Last().Item1;

            var nS = new Vector2(nFirst.X, nFirst.Z);
            var nE = new Vector2(nLast.X, nLast.Z);

            var cFirst = cEdge.First().Item1;
            var cLast = cEdge.Last().Item1;
            var cS = new Vector2(cFirst.X, cFirst.Z);
            var cE = new Vector2(cLast.X, cLast.Z);

            if (!(nS.IsEqualApprox(cE) ^ nS.IsEqualApprox(cS)) ||
                !(nE.IsEqualApprox(cE) ^ nE.IsEqualApprox(cS)))
            {
                throw new InvalidOperationException();
            }

            return nE.IsEqualApprox(cS);
        }
    }

    public static List<(Vector3, int Value)> ExplicitEdge(LinkedList<int> linkedList, Triangulation tri)
    {
        var edge = linkedList.Select(edge =>
        {
            var elem = tri.SubEdges.ElementAt(edge);
            return (tri.Vertices[elem.Key.Item1], elem.Value);
        }).ToList();
        var lastElement = tri.SubEdges.ElementAt(linkedList.Last!.Value);
        edge.Add((tri.Vertices[lastElement.Key.Item2], lastElement.Value));
        return edge;
    }

    /// <summary>
    /// Debug method that dumps the state of the chunk editor to the standard output.
    /// </summary>
    public void DebugDumpEditorState()
    {
        StringBuilder sb = new();
        AppendHSeparator(sb);
        sb.Append($"Debug Chunk Editor state dump for chunk {_chunk.Coordinates}")
            .AppendLine()
            .Append($"Snapshot timestamp: {DateTime.Now.ToString(CultureInfo.InvariantCulture)}")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("General chunk information").AppendLine();
        AppendHSeparator(sb);
        sb.Append($"Chunk coordinates: {_chunk.Coordinates}")
            .AppendLine()
            .Append($"Chunk dimensions :{_chunk.Dimensions2D}")
            .AppendLine()
            .Append($"Data points count: {_chunk.DataGrid.Size}")
            .AppendLine()
            .Append($"Full hexagonal cells count: {_chunk.TerrainDualGrid.CompleteCells.Count}")
            .AppendLine()
            .Append($"Partial hexagonal cells count: {_chunk.TerrainDualGrid.PendingCells.Count}")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("Chunk Processing information").AppendLine();
        AppendHSeparator(sb);
        sb.Append($"Chunk threshold computation mode: {_chunk.DefaultThreshold.Item2}")
            .AppendLine()
            .Append($"Chunk threshold value: {_chunk.DefaultThreshold.Item1}")
            .AppendLine()
            .Append($"Default processing mode: {_chunk.DefaultGeometryModes?.Item1}")
            .AppendLine()
            .Append($"Over-threshold processing mode :{_chunk.DefaultGeometryModes?.Item2}")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("Current chunk editor information")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("Processing status: TODO")
            .AppendLine()
            .Append($"Edition request count: {
                _collectedEdgeRequests.Sum(a => a.Value.Count)}")
            .AppendLine()
            .Append($"Triangulation count: {_triangulationsById.Count}")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("Per cell manifold data").AppendLine();
        AppendHSeparator(sb);
        foreach (var hexTerrainCell in _cells.Values)
        {
            DebugDumpCellState(hexTerrainCell, sb);
            sb.AppendLine();
            AppendHSeparator(sb);
        }


        Console.WriteLine(sb);
    }

    private void DebugDumpCellState(HexTerrainCell cell, StringBuilder sb)
    {
        sb.Append('\t').Append(cell)
            .AppendLine()
            .Append('\t');

        sb.Append("Data Y values :");
        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            sb.Append($"     {cell.GetVertexData!(i):0.00}     ");
        }

        sb.AppendLine().Append('\t');
        sb.Append($"Avg cell Y value: {cell.AverageHeight}")
            .AppendLine().Append('\t')
            .Append($"Overrides chunk geometry behavior: {cell.GeometryModesOverride != null}")
            .AppendLine().Append('\t');
        if (cell.GeometryModesOverride != null)
        {
            sb.Append($"Cell processing mode: {_chunk.DefaultGeometryModes?.Item1}")
                .AppendLine().Append('\t')
                .Append($"Cell over-threshold processing mode :{_chunk.DefaultGeometryModes?.Item2}")
                .AppendLine().Append('\t');
        }

        var tris = GetTriangleInfos([cell.CellCoordsImplicit])[cell];
        sb.Append($"Cell triangles count: {tris.Count}")
            .AppendLine().Append('\t');

        var (dico, borderEdgesAsSets, manifoldBorderEdgesAsSets) = TriangleUtils.ReprocessEdgeGeometry(tris, false);
        var isManifold = true &&
                         TriangleUtils.AssertIsClosedManifoldBorder(borderEdgesAsSets, false) &&
                         TriangleUtils.AssertIsClosedManifoldBorder(manifoldBorderEdgesAsSets, false);
        foreach (var _ in dico.Where(kvp => !(
                     kvp.Value == 2 ||
                     kvp.Value == 1 && borderEdgesAsSets.Contains(kvp.Key, new FloatArrayComparer(1e-5)))))
        {
            isManifold = false;
        }

        sb.Append($"Cell triangulation is manifold : {isManifold}").AppendLine();
        AppendHSeparator(sb);
        sb.Append('\t').Append('\t');
        if (!isManifold)
        {
            sb.Append("Dumping cell triangulation data as obj snippet because of non-manifold output :")
                .AppendLine();
            AppendHSeparator(sb);
            ToStlDump(tris, sb);
        }
    }

    private static void ToStlDump(List<HexTerrainCell.TriangleInfo> tris, StringBuilder sb)
    {
        sb.Append("solid mesh").AppendLine();

        foreach (var triangleInfo in tris)
        {
            var n = TriangleUtils.GetNormal(triangleInfo.Points);
            sb.Append($"facet normal {n.X} {n.Y} {n.Z}").AppendLine()
                .Append('\t').Append("outer loop").AppendLine();
            foreach (var p in triangleInfo.Points)
            {
                sb.Append('\t')
                    .Append('\t')
                    .Append($"vertex {p.X} {p.Y} {p.Z}")
                    .AppendLine();
            }
            sb.Append('\t').Append("end loop").AppendLine().Append("end facet").AppendLine();
        }
    }

    private static void AppendHSeparator(StringBuilder sb)
    {
        sb.Append("------------------------------------------------------------------------------------").AppendLine();
    }

    /// <summary>
    /// This method aims to fix issues with the chunk triangulation if the output is not cell-wise manifold 
    /// </summary>
    public void PostProcess()
    {
        //TODO : DO it cellwise otherwise the cost may become prohibitive
        var output = TriangleUtils.ReprocessEdgeGeometry(GetTriangleInfos().SelectMany(res=> res.Value).ToList());
        if (TriangleUtils.AssertIsClosedManifoldBorder(output.borderEdgesAsSets)) return;
        //TODO find coplanar triangles with intersections
        DebugDumpEditorState();
        StitchEdges();
    }
}