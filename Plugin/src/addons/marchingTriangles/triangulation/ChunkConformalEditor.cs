using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class ChunkConformalEditor
{
    /// <summary>
    /// The chunk on which the editor works
    /// </summary>
    private readonly HexagonalTerrainChunk _chunk;

    private readonly Func<Vector2I, Vector2I, HexagonalTerrainChunk?>? _neighborChunkProvider;

    ///
    /// Dictionaries of the vertex editors indexed by the dual indexing.
    /// Entries with Z = -1 are the editors of the cell centers
    ///
    private readonly Dictionary<Vector3I, VertexGeometryEditor> _cellVertexEditors = new();

    private readonly Dictionary<Vector3I, List<Vector3I>> _dataPointToCellSummitsMapping = new();

    private readonly Dictionary<Vector3I, List<Vector3I>> _dataPointNeighborsAsCellSummit = new();

    ///Flattened neighboring graph in the data point coordinate space
    private readonly Dictionary<Vector3I, List<Vector3I>> _dataPointNeighborMap = new();
    
    
    private readonly SortedSet<(Vector3I start, Vector3I end)> externalEdges = new(UnorderedV3TupleComparer.Instance);


    private readonly Dictionary<Vector3I, List<Vector3I>> _cellSummitsToDataPointMapping = new();


    // Dictionaries on cell indexes (hex summit grid)
    private readonly Dictionary<Vector2I, HexTerrainCell> _cells = new();
    private readonly Dictionary<Vector2I, Triangulation?[]?> _triangulationsPerCell = new();


    private object requests;
    private object remoteRequests;


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

                triangulationArray![cellIdxToVertex.Key] ??= new Triangulation([a, b, c], false, true);


                //Build data point neighbor maps as we know the cell summits (cellIdxToVertex-1) , (cellIdxToVertex+1)
                //and are neighbors of the current cell summit,
                exists = _dataPointNeighborsAsCellSummit.TryGetValue(cellIdxToVertex.Value, out var dataPointNeighbors);
                if (!exists || dataPointNeighbors == null)
                {
                    dataPointNeighbors = new List<Vector3I>();
                    _dataPointNeighborsAsCellSummit.Add(cellIdxToVertex.Value, dataPointNeighbors);
                }

                dataPointNeighbors.Add(
                    new Vector3I(
                        cell.CellCoordsImplicit.X,
                        cell.CellCoordsImplicit.Y,
                        EngineUtils.Mod(cellIdxToVertex.Key - 1, HexTerrainCell.VertexCount)));
                dataPointNeighbors.Add(
                    new Vector3I(
                        cell.CellCoordsImplicit.X,
                        cell.CellCoordsImplicit.Y,
                        EngineUtils.Mod(cellIdxToVertex.Key + 1, HexTerrainCell.VertexCount)));
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
        var internalEdges = new SortedSet<(Vector3I start,Vector3I end)>(UnorderedV3TupleComparer.Instance);

        //We instantiate all the cell summit editors
        foreach (var vertex in _cellSummitsToDataPointMapping.Keys)
        {
            _cellVertexEditors.Add(vertex, new VertexGeometryEditor(vertex));
        }

        //Cell centers are indexed with their cell index and -1 for Z value.
        foreach (var cell in _cells.Values)
        {
            var centerVertex = new Vector3I(
                cell.CellCoordsImplicit.X,
                cell.CellCoordsImplicit.Y,
                -1);
            _cellVertexEditors.Add(centerVertex, new VertexGeometryEditor(centerVertex));
            //Use the cell iteration to add the iternal cells to the edges
            for (int i = 0; i < HexTerrainCell.VertexCount; i++)
            {
                internalEdges.Add((centerVertex, new Vector3I(cell.CellCoordsImplicit.X, cell.CellCoordsImplicit.Y, i)));
            }
        }

        
        //We obtain all the edges
        var edges = internalEdges.Concat(internalEdges);
        
        foreach (var internalEdge in edges)
        {
            var eStart = internalEdge.start;
            var eEnd = internalEdge.end;

            HashSet<Triangulation?> edgeTris = new();
            
            if (eStart.Z!=-1)
            {
                edgeTris.Add(_triangulationsPerCell[new Vector2I(eStart.X, eStart.Y)]?[eStart.Z]);

            }
            if (eEnd.Z!=-1)
            {
                edgeTris.Add(_triangulationsPerCell[new Vector2I(eEnd.X, eEnd.Y)]?[eEnd.Z]);
            }
            
            Dictionary<Vector2I, HexTerrainCell> eStartNeighborCells=new();
            Dictionary<Vector2I, HexTerrainCell> eEndNeighborCells=new();
            var cell= _cells[new Vector2I(eStart.X, eStart.Y)];

            if (eStart.Z==-1)
            {
                eStartNeighborCells.Add(cell.CellCoordsImplicit,cell);
            }
            else
            {
                foreach (var cellIdx in _cellSummitsToDataPointMapping[eStart]
                             .SelectMany(v=> _dataPointNeighborsAsCellSummit[v]
                                 .Select(vec=>new Vector2I(vec.X,vec.Y))).Distinct())   
                {
                    eStartNeighborCells.Add(cellIdx,_cells[cellIdx]);
                }
            }
            
            if (eEnd.Z==-1)
            {
                eEndNeighborCells.Add(cell.CellCoordsImplicit,cell);
            }
            else
            {
                foreach (var cellIdx in _cellSummitsToDataPointMapping[eEnd]
                             .SelectMany(v=> _dataPointNeighborsAsCellSummit[v]
                                 .Select(vec=>new Vector2I(vec.X,vec.Y))).Distinct())
                {
                    eEndNeighborCells.Add(cellIdx,_cells[cellIdx]);
                }
            }
            foreach (var triangulation in edgeTris.Where(tri=> tri!=null))
            {
                //Register the local edition of both points on the edge
                RegisterEdgeSummitRequests(eStart, eStartNeighborCells);
                RegisterEdgeSummitRequests(eEnd, eEndNeighborCells);

                void RegisterEdgeSummitRequests(Vector3I cellSummit, Dictionary<Vector2I, HexTerrainCell> neighorCells)
                {
                    var indexOfCellSummitInTri = GetCellSummitIndexInTriangulation(cellSummit, triangulation, cell);
                    var request = GetCellSummitGeometryRequest(cell.CellCoordsImplicit,triangulation ,indexOfCellSummitInTri);
                    _cellVertexEditors[cellSummit].RegisterLocalVertexAction(
                        cellSummit,
                        (triangulation,indexOfCellSummitInTri),
                        request,
                        neighorCells,
                        internalEdge);
                }
            }
        }
        
    }

    private static int GetCellSummitIndexInTriangulation(Vector3I cellSummit, Triangulation? triangulation, HexTerrainCell cell)
    {
        var cellIdx = new Vector2I(cellSummit.X, cellSummit.Y);
                
        if (triangulation==null)
        {
            throw new ArgumentException();
        }
        int indexOfCellSummitInTri;
    
        if (cellSummit.Z == -1)
        {
            indexOfCellSummitInTri = 0;
        } else {
            var posIdx1= triangulation.SourceTriangle[1];
            var vertPos = cell.VertexPositionsInPlane[cellSummit.Z];
            
            indexOfCellSummitInTri = 
                new Vector2(posIdx1.X, posIdx1.Y).IsEqualApprox(new Vector2((float)vertPos.X,(float)vertPos.Y)) 
                    ? 1 
                    : 2;
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
                throw new ArgumentException("missing triangulation",nameof(triangulation));
            }
        }
            
        var cell = _cells[cellIdx];

        var requestedGeometryMode =
            cell.GeometryModesOverride ?? _chunk.DefaultGeometryModes ?? throw new ConstraintException();
        var requestedParameters =
            cell.ParametersOverride ?? _chunk.GeometryModeParameters?? throw new ConstraintException();
    
        
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
}