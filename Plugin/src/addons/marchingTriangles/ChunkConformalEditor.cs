using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class ChunkConformalEditor
{
    private readonly HexagonalTerrainChunk _chunk;

    // Dictionaries on vertices (dual(data) grid)
    private readonly Dictionary<Vector3I, VertexConformalGeometryEdit> _editions = new();

    private readonly Dictionary<Vector3I, List<(Vector2I, int)>> vertexToCellsMapping = new();

    //Dictionary for cell centers (since there is no indexing for them)
    private readonly Dictionary<Vector2I, VertexConformalGeometryEdit> _cellCenterEditions = new();

    // Dictionaries on cell idxs (hex grid)
    private readonly Dictionary<Vector2I, HexTerrainCell> _cells = new();
    private readonly Dictionary<Vector2I, Triangulation?[]?> _triangulationsPerCell = new();

    public ChunkConformalEditor(HexagonalTerrainChunk underlying)
    {
        _chunk = underlying;
        PrepareMappings();
    }

    public HexagonalTerrainChunk Chunk
    {
        get { return _chunk; }
    }

    //Once and for all computation of the vertex=> cells mappings
    private void PrepareMappings()
    {
        foreach (var cell in _chunk._terrainDualGrid.CompleteCells)
        {
            if (!_cells.TryAdd(cell.CellCoordsImplicit, cell))
            {
                throw new InvalidOperationException($"The _cells dictionary already had the entry {cell}");
            }

            foreach (var cellIdxToVertex in cell.DualCellsMapping)
            {
                // Build vertexToCellMapping
                var exists = vertexToCellsMapping.TryGetValue(cellIdxToVertex.Value, out var vertexToCellList);
                if (!exists)
                {
                    vertexToCellList = new List<(Vector2I, int)>();
                    //Create the array holding the cells touching the vertex
                    vertexToCellsMapping.Add(cellIdxToVertex.Value, vertexToCellList);
                }

                if (vertexToCellsMapping[cellIdxToVertex.Value].Count >= 3)
                {
                    throw new InvalidOperationException("A vertex should only have up to 3 cells as neighbors");
                }

                vertexToCellsMapping[cellIdxToVertex.Value].Add((cell.CellCoordsImplicit, cellIdxToVertex.Key));

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
                    EngineUtils.mod(cellIdxToVertex.Key + 1, HexTerrainCell.VertexCount)];

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
                    cell.GetVertexData!(EngineUtils.mod(cellIdxToVertex.Key + 1, HexTerrainCell.VertexCount)),
                    (float)c2D.Y);

                triangulationArray![cellIdxToVertex.Key] ??= new Triangulation([a, b, c], false, true);
            }
        }

        if (true) //DEBUG CONSISTENCY CHECK
        {
            foreach (var keyValuePair in vertexToCellsMapping)
            {
                var pos = _chunk.DataGrid.OrientationSystem.GetCellCentroid(keyValuePair.Key);
                if (keyValuePair.Value.Any(t => (_cells[t.Item1].VertexPositionsInPlane[t.Item2] - pos).Length > 1e-5))
                {
                    throw new Exception(
                        $"Inconsistent mapping data at [{keyValuePair.Key}]=>[{String.Join(";", keyValuePair.Value)}]");
                }
            }
        }
    }

    public void CollectAllOperations(bool forceRebuild)
    {
        // Only process the full cells
        var completeCells = _chunk._terrainDualGrid.CompleteCells;
        var vertices = completeCells.SelectMany(c => c.DualCellsMapping.Values).ToHashSet();

        if (forceRebuild || _chunk.Dirty)
        {
            foreach (var vIdx in vertices)
            {
                ComputeVertexGeometryActions(vIdx);
            }
        }
        else
        {
            var updatedVertices = _chunk.NeedUpdate
                .Where(vNeedsUpdate => vNeedsUpdate.Value && vertices.Contains(vNeedsUpdate.Key))
                .Select(vNeedsUpdate => vNeedsUpdate.Key).ToList();

            var copiedVertices = _chunk.NeedUpdate
                .Where(vNeedsUpdate => !vNeedsUpdate.Value && vertices.Contains(vNeedsUpdate.Key))
                .Select(vNeedsUpdate => vNeedsUpdate.Key).ToList();
            //Register all copies first
            foreach (var vIdx in copiedVertices)
            {
                ComputeVertexGeometryActions(vIdx, true);
            }

            //Register all the actions cell-by-cell
            foreach (var vIdx in updatedVertices)
            {
                ComputeVertexGeometryActions(vIdx);
            }
        }

        // Process the cell centers if needed
        foreach (var hexTerrainCell in completeCells)
        {
            // The cell center is not a part of the data grid, and its value is interpolated from the cell
            // however, we may want to apply local geometry changes to the triangulations
            // (notably for the flat triangle usecase)
            ComputeCellCenterGeometryActions(hexTerrainCell);
        }
    }

    private void ComputeCellCenterGeometryActions(HexTerrainCell hexTerrainCell, bool copyOnly = false)
    {
        if (copyOnly)
        {
            throw new NotImplementedException();
        }

        if (_cellCenterEditions.ContainsKey(hexTerrainCell.CellCoordsImplicit))
        {
            throw new InvalidOperationException(
                $"We should collect the local actions only once, but the cell center" +
                $" {hexTerrainCell.CellCoordsImplicit} was visited at least twice");
        }

        var appliedGeometryOperations =
            new Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>>();
        var appliedTriangulations =
            new Dictionary<(int, HexTerrainCell), Tuple<(Triangulation, int), (Triangulation, int)?>>();

        var edit = new VertexConformalGeometryEdit();
        _cellCenterEditions.Add(hexTerrainCell.CellCoordsImplicit, edit);
        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            //Process cell data to determine the mask for each of the triangles (maybe??)
            // ^This may not be needed (MARK AS TODO just in case)
            var appliedMode = (hexTerrainCell.GeometryModesOverride ?? _chunk.DefaultGeometryModes).Item1;
            //
            appliedGeometryOperations.Add(
                (i, hexTerrainCell), new Tuple<GeometryMode, GeometryMode?>(appliedMode, null));

            appliedTriangulations.Add((i, hexTerrainCell), new Tuple<(Triangulation, int), (Triangulation, int)?>(
                (_triangulationsPerCell[hexTerrainCell.CellCoordsImplicit][i], 0),
                null));
        }
        edit.RegisterLocalAction(appliedGeometryOperations, appliedTriangulations);

    }

    private void ComputeVertexGeometryActions(Vector3I coords, bool copyOnly = false)
    {
        if (copyOnly)
        {
            throw new NotImplementedException();
        }

        if (_editions.ContainsKey(coords))
        {
            throw new InvalidOperationException(
                $"We should collect the local actions only once, but vertex {coords} was visited at least twice");
        }
        else
        {
            var localVertexIndexingInNeighbors = vertexToCellsMapping[coords];
            var triangulationsByCell =
                new Dictionary<(int, HexTerrainCell), Tuple<(Triangulation, int), (Triangulation, int)?>>();
            var neighborCells = new Dictionary<Vector2I, HexTerrainCell>();
            foreach (var neigborCell in localVertexIndexingInNeighbors)
            {
                neighborCells.Add(neigborCell.Item1, _cells[neigborCell.Item1]);
                //Add the two triangulations of the cell containing the vertex (the one pointed by the mapping...and the one after)
                triangulationsByCell.Add((neigborCell.Item2, _cells[neigborCell.Item1]),
                    new Tuple<(Triangulation, int), (Triangulation, int)?>(
                        (_triangulationsPerCell[neigborCell.Item1]?[neigborCell.Item2]!,
                            1), // For the first triangulation, the vertex is the index#1 of the triangulation
                        (_triangulationsPerCell[neigborCell.Item1]?[EngineUtils.mod(neigborCell.Item2 - 1, HexTerrainCell.VertexCount)]!,
                            2) // For the second triangulation, the vertex is the index#2 of the triangulation
                    )
                );
            }

            var edit = new VertexConformalGeometryEdit();
            _editions.Add(coords, edit);
            if (true) //DEBUG FLAG
            {
                var comp = new EngineUtils.V2DComp(1e-5);
                //Check local data is consistent
                if (triangulationsByCell.Select(t => t.Key)
                        .Select(tuple => tuple.Item2.VertexPositionsInPlane[tuple.Item1]).DistinctBy(k => k, comp)
                        .Count() > 1)
                {
                    throw new Exception("More than one [X,Z] position value for the source data");
                }

                if (triangulationsByCell.Select(t => t.Key)
                        .Select(tuple => tuple.Item2.GetVertexData(tuple.Item1)).Distinct().Count() > 1)
                {
                    throw new Exception("More than one Y value for the source data");
                }
            }

            ComputeLocalGeometryActions(
                edit,
                localVertexIndexingInNeighbors,
                triangulationsByCell,
                _chunk.DefaultGeometryModes,
                _chunk.DefaultThreshold,
                neighborCells);
        }
    }

    private static void ComputeLocalGeometryActions(
        VertexConformalGeometryEdit editor,
        List<(Vector2I, int)> localVertexIndexing,
        Dictionary<(int, HexTerrainCell), Tuple<(Triangulation, int), (Triangulation, int)?>> localTriangulations,
        Tuple<GeometryMode, GeometryMode> chunkGeometryMode,
        Tuple<float, ThresholdComputationMode> chunkThreshold,
        Dictionary<Vector2I, HexTerrainCell> neighborCells)
    {
        //Compute the vertex operation mask. The ordering is obtained by the localVertexIndexing indexing
        if (localVertexIndexing.Count is > 3 or < 1)
        {
            throw new ArgumentException(nameof(localVertexIndexing));
        }

        int thresholdMask = ComputeLocalMask(localVertexIndexing, chunkThreshold, neighborCells);

        Dictionary<Vector2I, GeometryMode> requestedGeometryOperation = new();
        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> appliedGeometryOperations = new();

        for (int i = 0; i < localVertexIndexing.Count; i++)
        {
            var requestedGeometryMode =
                chunkGeometryMode ?? neighborCells[localVertexIndexing[i].Item1].GeometryModesOverride;
            requestedGeometryOperation.Add(localVertexIndexing[i].Item1,
                (thresholdMask ^ (1 << i)) == 0 ? (requestedGeometryMode.Item1) : (requestedGeometryMode.Item2));
        }

        // Now that we know which operations are requested by each cell, we check if there are incompatibilities
        var differentGeometryModes = requestedGeometryOperation.Values.Distinct();

        Dictionary<(int, Vector2I), Tuple<GeometryMode, GeometryMode?>> plannedGeometryOperations = new();
        //Single type of operation, all the triangulations will be edited similarly
        if (differentGeometryModes.Count() == 1)
        {
            foreach (var cellAndVertexIndex in localTriangulations.Keys)
            {
                appliedGeometryOperations.Add(
                    cellAndVertexIndex,
                    new Tuple<GeometryMode, GeometryMode?>(
                        differentGeometryModes.First(),
                        differentGeometryModes.First()));
            }
        }
        else //At least two cells have different operation types: We compare them pairwise, and the one will the
            // LOWEST ORDINAL will have priority. That operation will be applied on the edge bordering the two
            // cells for BOTH of the triangulations (on each side of the edge). 
        {
            appliedGeometryOperations = ProcessCellGeometryOperations();
        }

        editor.RegisterLocalAction(appliedGeometryOperations, localTriangulations);
    }

    private static Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> ProcessCellGeometryOperations()
    {
        throw new NotImplementedException();
    }

    private static int ComputeLocalMask(
        List<(Vector2I, int)> localVertexIndexing,
        Tuple<float, ThresholdComputationMode> chunkThreshold,
        Dictionary<Vector2I, HexTerrainCell> neighborCells)
    {
        int thresholdMask = 0;
        int count = 0;

        (Vector2I, int) firstEntry = localVertexIndexing[0];
        Vector2D posVertex2D = neighborCells[firstEntry.Item1].VertexPositionsInPlane[firstEntry.Item2];
        float posVertexY = neighborCells[firstEntry.Item1].GetVertexData!(firstEntry.Item2);

        Vector3 posVertex = new Vector3((float)posVertex2D.X, posVertexY, (float)posVertex2D.Y);

        foreach (var vertexInHexCell in localVertexIndexing)
        {
            Vector2D otherVertex2D = neighborCells[firstEntry.Item1]
                .VertexPositionsInPlane[(firstEntry.Item2 + 1) % HexTerrainCell.VertexCount];
            float otherVertexY =
                neighborCells[firstEntry.Item1].GetVertexData!((firstEntry.Item2 + 1) % HexTerrainCell.VertexCount);

            Vector3 otherVertex = new Vector3((float)otherVertex2D.X, otherVertexY, (float)otherVertex2D.Y);

            //Get the threshold for this cell (per cell threshold not used atm)
            Tuple<float, ThresholdComputationMode> threshold = chunkThreshold;

            thresholdMask += threshold.Item2.IsOverThreshold(threshold.Item1, posVertex, otherVertex) ? 1 << count : 0;
        }

        return thresholdMask;
    }

    public Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> GetTriangleInfos()
    {
        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();
        foreach (var triangulation in _triangulationsPerCell)
        {
            // Find the cell
            var cell = _cells[triangulation.Key];
            output.TryAdd(cell, new List<HexTerrainCell.TriangleInfo>());
            foreach (var tuple in triangulation.Value)
            {
                //Process the triangulation and fetch the triangles
                output[cell].AddRange(tuple.ToTriangleInfoList());
            }
        }

        return output;
    }

    public void ApplyGeometryOperations()
    {
        foreach (var geometryEdit in _editions.Values)
        {
            geometryEdit.ApplyTransformations();
        }

        foreach (var cellCenterGeometryEdit in (_cellCenterEditions.Values))
        {
            cellCenterGeometryEdit.ApplyTransformations();
        }
    }
}