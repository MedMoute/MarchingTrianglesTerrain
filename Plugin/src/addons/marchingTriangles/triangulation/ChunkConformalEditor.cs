using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class ChunkConformalEditor
{
    private readonly HexagonalTerrainChunk _chunk;

    // Dictionaries on vertices (dual(data) grid)
    private readonly Dictionary<Vector3I, VertexConformalGeometryEdit> _editions = new();

    private readonly Dictionary<Vector3I, List<(Vector2I, int)>> _vertexToCellsMapping = new();

    //Dictionary for cell centers (since there is no indexing for them)
    private readonly Dictionary<Vector2I, VertexConformalGeometryEdit> _cellCenterEditions = new();

    // Dictionaries on cell indexes (hex grid)
    private readonly Dictionary<Vector2I, HexTerrainCell> _cells = new();
    private readonly Dictionary<Vector2I, Triangulation?[]?> _triangulationsPerCell = new();

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
                var exists = _vertexToCellsMapping.TryGetValue(cellIdxToVertex.Value, out var vertexToCellList);
                if (!exists)
                {
                    vertexToCellList = new List<(Vector2I, int)>();
                    //Create the array holding the cells touching the vertex
                    _vertexToCellsMapping.Add(cellIdxToVertex.Value, vertexToCellList);
                }

                if (_vertexToCellsMapping[cellIdxToVertex.Value].Count >= 3)
                {
                    throw new InvalidOperationException("A vertex should only have up to 3 cells as neighbors");
                }

                _vertexToCellsMapping[cellIdxToVertex.Value].Add((cell.CellCoordsImplicit, cellIdxToVertex.Key));

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
            }
        }

        if (true) //DEBUG CONSISTENCY CHECK
        {
            foreach (var keyValuePair in _vertexToCellsMapping)
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
        var completeCells = _chunk.TerrainDualGrid.CompleteCells;
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
            // (notably for the flat triangle use-case)
            ComputeCellCenterGeometryActions(hexTerrainCell);
        }
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
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
        var appliedGeometryParameters =
            new Dictionary<(int, HexTerrainCell), Tuple<(float, float), (float, float)?>>();
        var appliedTriangulations =
            new Dictionary<(int, HexTerrainCell), Tuple<(Triangulation, int), (Triangulation, int)?>>();

        var edit = new VertexConformalGeometryEdit();
        _cellCenterEditions.Add(hexTerrainCell.CellCoordsImplicit, edit);
        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            //TODO process mask for determining which is the correct appliedMode
            var appliedMode = (hexTerrainCell.GeometryModesOverride ??
                               _chunk.DefaultGeometryModes ??
                               throw new Exception("Chunk default should have been set")).Item1;

            var cellParameters = (hexTerrainCell.ParametersOverride ??
                                  _chunk.GeometryModeParameters ??
                                  throw new Exception("Chunk default should have been set")).Item1;
            appliedGeometryOperations.Add(
                (i, hexTerrainCell), new Tuple<GeometryMode, GeometryMode?>(appliedMode, null));
            appliedGeometryParameters.Add(
                (i, hexTerrainCell), new Tuple<(float, float), (float, float)?>(cellParameters, null));
            appliedTriangulations.Add((i, hexTerrainCell), new Tuple<(Triangulation, int), (Triangulation, int)?>(
                (_triangulationsPerCell[hexTerrainCell.CellCoordsImplicit]?[i], 0)!,
                null));
        }

        edit.RegisterLocalVertexAction(
            appliedGeometryOperations,
            appliedGeometryParameters,
            appliedTriangulations);
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
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

        var localVertexIndexingInNeighbors = _vertexToCellsMapping[coords];
        var triangulationsByCell =
            new Dictionary<(int, HexTerrainCell), Tuple<(Triangulation, int), (Triangulation, int)?>>();
        var impactedCells = new Dictionary<Vector2I, HexTerrainCell>();
        foreach (var cellAndVertexIndex
                 in localVertexIndexingInNeighbors)
        {
            impactedCells.Add(cellAndVertexIndex.Item1, _cells[cellAndVertexIndex.Item1]);
            //Add the two triangulations of the cell containing the vertex (the one pointed by the mapping...and the one after)
            triangulationsByCell.Add((cellAndVertexIndex.Item2, _cells[cellAndVertexIndex.Item1]),
                new Tuple<(Triangulation, int), (Triangulation, int)?>(
                    (_triangulationsPerCell[cellAndVertexIndex.Item1]?[cellAndVertexIndex.Item2]!,
                        1), // For the first triangulation, the vertex is the index#1 of the triangulation
                    (_triangulationsPerCell[cellAndVertexIndex.Item1]?[EngineUtils.Mod(cellAndVertexIndex.Item2 - 1, HexTerrainCell.VertexCount)]!,
                        2) // For the second triangulation, the vertex is the index#2 of the triangulation
                )
            );
        }

        var vertexEditor = new VertexConformalGeometryEdit();
        _editions.Add(coords, vertexEditor);
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
                    .Select(tuple => tuple.Item2.GetVertexData!(tuple.Item1)).Distinct().Count() > 1)
            {
                throw new Exception("More than one Y value for the source data");
            }
        }

        if (_chunk.DefaultGeometryModes == null)
        {
            throw new InvalidOperationException("Chunk default geometry mode should have been set already");
        }

        if (_chunk.GeometryModeParameters == null)
        {
            throw new InvalidOperationException("Chunk default geometry parameters should have been set already");
        }

        ComputeLocalGeometryActions(
            vertexEditor,
            localVertexIndexingInNeighbors,
            triangulationsByCell,
            _chunk.DefaultGeometryModes,
            _chunk.GeometryModeParameters.Value,
            _chunk.DefaultThreshold,
            impactedCells);
    }

    private static void ComputeLocalGeometryActions(
        VertexConformalGeometryEdit editor,
        List<(Vector2I, int)> localVertexIndexing,
        Dictionary<(int, HexTerrainCell), Tuple<(Triangulation, int), (Triangulation, int)?>> localTriangulations,
        Tuple<GeometryMode, GeometryMode> chunkGeometryMode,
        ((float, float), (float, float)) chunkGeometryParameters,
        Tuple<float, ThresholdComputationMode> chunkThreshold,
        Dictionary<Vector2I, HexTerrainCell> neighborCells)
    {
        //Compute the vertex operation mask. The ordering is obtained by the localVertexIndexing indexing
        if (localVertexIndexing.Count is > 3 or < 1)
        {
            throw new ArgumentException(nameof(localVertexIndexing));
        }

        //For each cell, we extract the list of requested operations, the result is ordered by index
        Dictionary<Vector2I, HashSet<GeometryMode>?[]> requestPerCell = new();

        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> appliedGeometryOperations = new();
        Dictionary<(int, HexTerrainCell), Tuple<(float, float), (float, float)?>> appliedGeometryParameters = new();

        //Iterate over Neighboring cells of the vertex
        for (int i = 0; i < localVertexIndexing.Count; i++)
        {
            var curVertex = localVertexIndexing[i];
            var requestedGeometryMode =
                neighborCells[curVertex.Item1].GeometryModesOverride ?? chunkGeometryMode;


            // Fetch the request list
            requestPerCell.TryGetValue(curVertex.Item1, out var listOfRequests);
            if (listOfRequests is null)
            {
                listOfRequests = new HashSet<GeometryMode>[6];
                requestPerCell.Add(curVertex.Item1, listOfRequests);
            }

            var vertexRegisteredRequests = listOfRequests[curVertex.Item2];
            if (vertexRegisteredRequests is null)
            {
                vertexRegisteredRequests = new();
                listOfRequests[curVertex.Item2] = vertexRegisteredRequests;
            }

            //Each cell has 2 triangulations related to the vertex
            var t1 = localTriangulations[(curVertex.Item2, neighborCells[curVertex.Item1])]
                .Item1;
            var mask1 = t1.Item1.ComputeMask(chunkThreshold);
            bool t1IsOverThreshold = (mask1 & (1 << t1.Item2)) != 0;

            vertexRegisteredRequests.Add(t1IsOverThreshold
                ? requestedGeometryMode.Item1
                : requestedGeometryMode.Item2);

            var t2 = localTriangulations[(curVertex.Item2, neighborCells[curVertex.Item1])]
                .Item2;
            if (t2 is not null)
            {
                var mask2 = t2.Value.Item1.ComputeMask(chunkThreshold);
                bool t2IsOverThreshold = (mask2 & (1 << t2.Value.Item2)) != 0;
                vertexRegisteredRequests.Add(t2IsOverThreshold
                    ? requestedGeometryMode.Item1
                    : requestedGeometryMode.Item2);
            }
        }

        // Now that we know which operations are requested by each cell, we check if there are incompatibilities
        var geometryModes = requestPerCell.Values.SelectMany(array =>
            array.Where(e => e is not null)
                .SelectMany(e => e!)).Distinct().ToArray();

        if (geometryModes.Length == 1)
        {
            //Single type of operation, all the triangulations will be edited similarly
            foreach (var cellAndVertexIndex in localTriangulations.Keys)
            {
                appliedGeometryOperations.Add(
                    cellAndVertexIndex,
                    new Tuple<GeometryMode, GeometryMode?>(
                        geometryModes.First(),
                        geometryModes.First()));

                var applyBaseMode = geometryModes.First() == chunkGeometryMode.Item1;
                (float, float) parameters =
                    applyBaseMode ? chunkGeometryParameters.Item1 : chunkGeometryParameters.Item2;

                appliedGeometryParameters.Add(
                    cellAndVertexIndex,
                    new Tuple<(float, float), (float, float)?>(parameters, parameters));
            }
        }
        else //At least two triangulations have different operation types for the current vertex:
            // We consider triangulation pairs that have a common edge.
            // If the triangulations have a different request, the one will the
            // LOWEST ORDINAL will have priority.
            // That operation will be applied on the edge bordering the two
            // cells for BOTH of the triangulations (on each side of the edge). 
            // TODO ^ Currenlty the method below just applies the lowest on the cell rather than edge by edge
        {
            appliedGeometryOperations =
                ProcessCellGeometryOperations(requestPerCell, neighborCells, localTriangulations);
            //Get the correct pair of parameters
            foreach (var appliedGeometryOperation in appliedGeometryOperations)
            {
                var parameters = appliedGeometryOperation.Key.Item2.ParametersOverride
                                 ?? chunkGeometryParameters;

                var applyBaseMode1 = appliedGeometryOperation.Value.Item1 == chunkGeometryMode.Item1;
                var applyBaseMode2 = appliedGeometryOperation.Value.Item2 == chunkGeometryMode.Item1;

                var parameters1 = applyBaseMode1 ? parameters.Item1 : parameters.Item2;
                var parameters2 = applyBaseMode2 ? parameters.Item1 : parameters.Item2;
                appliedGeometryParameters.Add(
                    appliedGeometryOperation.Key,
                    new Tuple<(float, float), (float, float)?>(parameters1, parameters2));
            }
        }

//TODO : explicit the operation edge  by edge so we can apply the correct operations
        editor.RegisterLocalVertexAction(appliedGeometryOperations, appliedGeometryParameters, localTriangulations);
    }

    private static Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> ProcessCellGeometryOperations(
        Dictionary<Vector2I, HashSet<GeometryMode>?[]> requestedGeometryOperation,
        Dictionary<Vector2I, HexTerrainCell> hexTerrainCells,
        Dictionary<(int, HexTerrainCell), Tuple<(Triangulation, int), (Triangulation, int)?>> neighborCells)
    {
        //TODO only apply pairwise operations
        Dictionary<(int, HexTerrainCell), Tuple<GeometryMode, GeometryMode?>> perCellModes = new();

        foreach (var entry in requestedGeometryOperation)
        {
            var cellIdx = entry.Key;
            var array = entry.Value;
            for (int i = 0; i < HexTerrainCell.VertexCount; i++)
            {
                var modes = array[i];
                if (modes is not null)
                {
                    int level = modes.Select(mode => (int)mode)
                        .Prepend(int.MaxValue)
                        .Min();
                    perCellModes.Add(
                        (i, hexTerrainCells[cellIdx]),
                        new Tuple<GeometryMode, GeometryMode?>((GeometryMode)level, (GeometryMode)level));
                }
            }
        }

        return perCellModes;
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

        foreach (var _ in localVertexIndexing)
        {
            Vector2D otherVertex2D = neighborCells[firstEntry.Item1]
                .VertexPositionsInPlane[(firstEntry.Item2 + 1) % HexTerrainCell.VertexCount];
            float otherVertexY =
                neighborCells[firstEntry.Item1].GetVertexData!((firstEntry.Item2 + 1) % HexTerrainCell.VertexCount);

            Vector3 otherVertex = new Vector3((float)otherVertex2D.X, otherVertexY, (float)otherVertex2D.Y);

            //Get the threshold for this chunk (per cell threshold not used atm)
            Tuple<float, ThresholdComputationMode> threshold = chunkThreshold;

            thresholdMask += threshold.Item2.IsOverThreshold(threshold.Item1, posVertex, otherVertex)
                ? 1 << count
                : 0;
        }

        return thresholdMask;
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