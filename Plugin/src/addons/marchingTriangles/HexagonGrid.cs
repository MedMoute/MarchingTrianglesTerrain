using System;
using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Hexagonal tiling build by duality of a TriangleGrid.
/// </summary>
public class HexagonGrid
{
    public RegularUniformFrame Frame { get; }

    public Dictionary<Vector2I, HexTerrainCell?> PendingCells { get; } = new();

    public HashSet<HexTerrainCell> CompleteCells = new();

    private readonly RegularUniformFrame _dualFrame;

    private HexagonGrid(RegularUniformFrame frame, RegularUniformFrame dualFrame)
    {
        if (frame is not HexTileOrientationSystem)
        {
            throw new ArgumentException("orientationSystem is not a HexTileOrientationSystem");
        }

        Frame = frame;
        _dualFrame = dualFrame;
    }


    public static HexagonGrid BuildFromDual(
        TriangleGrid dualGrid,
        Vector2I chunkDimensions,
        Func<Vector2I, TriangleGrid> neighborDataGridProvider,
        Func<Vector2I, bool> chunkTester)
    {
        RegularUniformFrame dualFrame = dualGrid.OrientationSystem;
        RegularUniformFrame frame = dualFrame.GetDual();
        HexagonGrid grid = new(frame, dualFrame);
        foreach (var dataPoint in dualGrid.Data)
        {
            grid.AddDeltaTileCellValues(dataPoint.Key, chunkDimensions, neighborDataGridProvider, chunkTester);
        }

        return grid;
    }

    public void AddDeltaTileCellValues(
        Vector3I trianglesTile,
        Vector2I dimensions2D,
        Func<Vector2I, TriangleGrid> neighborDataGridProvider,
        Func<Vector2I, bool> chunkTester)
    {
        var triangleVertices = _dualFrame.GetVertices(trianglesTile);
        List<Vector2I> affectedHexCells = new();
        //Find the cubeCoordinates affected by the cell by checking the cube coordinates
        // of each vertex
        foreach (var vertexPos in triangleVertices)
        {
            var hexCoords = Frame.GetCell(vertexPos);
            if (!affectedHexCells.Contains(hexCoords))
            {
                affectedHexCells.Add(hexCoords);
            }
        }

        // For every cube coord affected by a vertex of the cell :
        // Either create of add data for the matching HexagonCell 
        for (var i = 0; i < affectedHexCells.Count; i++)
        {
            var cellCoords = affectedHexCells[i];
            HexTerrainCell cell;
            if (!PendingCells.ContainsKey(cellCoords))
            {
                cell = new HexTerrainCell(cellCoords, Frame, _dualFrame);
                cell.SetDataFetchingFunction(dimensions2D, neighborDataGridProvider, chunkTester);
                PendingCells.Add(cellCoords, cell);
            }
            else
            {
                PendingCells.TryGetValue(cellCoords, out cell);
                if (cell == null)
                {
                    continue; // If the cell already exists and is ready => entry exist but was set to null, we just ignore the visit
                }
            }

            cell.VisitedBy(trianglesTile, dimensions2D);

            if (cell.IsReady())
            {
                CompleteCells.Add(cell);
                PendingCells.Remove(cellCoords);
            }
        }
    }

    public void PrintGridData()
    {
        GD.Print("FULL CELLS");
        foreach (KeyValuePair<Vector2I, HexTerrainCell> kvp in PendingCells)
        {
            GD.Print($"Coordinates = {kvp.Key} , Cell = {kvp.Value}");
        }
    }

    /// <summary>
    /// https://www.redblobgames.com/grids/hexagons/#rounding
    /// </summary>
    public static Vector2I CubeRound(Vector2D frac)
    {
        var q = (int)Math.Round(frac.X);
        var r = (int)Math.Round(frac.Y);
        var s = (int)Math.Round((-frac.X - frac.Y));

        var q_diff = Math.Abs(q - frac.X);
        var r_diff = Math.Abs(r - frac.Y);
        var s_diff = Math.Abs(s - (-frac.X - frac.Y));

        if (q_diff > r_diff && q_diff > s_diff)
        {
            q = -r - s;
        }
        else if (r_diff > s_diff)
        {
            r = -q - s;
        }
        else
        {
            s = -q - r;
        }

        return new Vector2I(q, r);
    }

    public static Vector3I ToFullCubeCoords(Vector2I axialCoords)
    {
        return new Vector3I(axialCoords.X, axialCoords.Y, -axialCoords.X - axialCoords.Y);
    }

    public static HexagonGrid BuildFromSerialData(
        double[] dataStructHexFrameSeed1,
        double[] dataStructHexFrameSeed2,
        double[] dataStructTriFrameSeed1,
        double[] dataStructTriFrameSeed2,
        Vector3I dataStructFrameDimensions,
        int[] dataStructFullCellIndices,
        int[] dataStructFullCellMappings,
        int[] dataStructFullCellVisitsMappingKey,
        int[] dataStructFullCellVisitsMappingValue,
        int[] dataStructPendingCellIndices,
        int[] dataStructPendingCellsVisitsMappingKey,
        int[] dataStructPendingCellsVisitsMappingValue)
    {
        if (dataStructFullCellIndices.Length % 2 != 0 ||
            dataStructFullCellMappings.Length % 3 != 0 ||
            dataStructPendingCellIndices.Length % 2 != 0 ||
            dataStructPendingCellsVisitsMappingKey.Length % 3 != 0 ||
            dataStructPendingCellsVisitsMappingValue.Length % 2 != 0)
        {
            throw new ArgumentException("The provided argument arays do not have an expected size");
            //TODO : Enforce a consistent size across arrays
        }

        Vector2I chunkDimensions = new Vector2I(dataStructFrameDimensions.X, dataStructFrameDimensions.Y);

        HexTileOrientationSystem frame = new HexTileOrientationSystem(
            new Vector2D(dataStructHexFrameSeed1[0], dataStructHexFrameSeed1[1]),
            new Vector2D(dataStructHexFrameSeed2[0], dataStructHexFrameSeed2[1])
        );
        RegularUniformFrame dualFrame = frame.GetDual();
        HexagonGrid grid = new HexagonGrid(frame, dualFrame);
        grid.CompleteCells = new HashSet<HexTerrainCell>();
        for (int i = 0; i < dataStructFullCellIndices.Length / 2; i++)
        {
            Vector2I fulCellIndex = new Vector2I(
                dataStructFullCellIndices[2 * i],
                dataStructFullCellIndices[2 * i + 1]);
            
            
            var cell = new HexTerrainCell(
                fulCellIndex,
                frame,
                dualFrame);
            for (int j = 0; j < 6; j++)
            {
                var cellMappingKey = new Vector3I(
                    dataStructFullCellVisitsMappingKey[3 * (6 * i + j)],
                    dataStructFullCellVisitsMappingKey[3 * (6 * i + j) + 1],
                    dataStructFullCellVisitsMappingKey[3 * (6 * i + j) + 2]);
                var cellMappingValue = new Vector2I(
                    dataStructFullCellVisitsMappingValue[2 * (6 * i + j)],
                    dataStructFullCellVisitsMappingValue[2 * (6 * i + j) + 1]);
                cell.Visits.Add(cellMappingKey, cellMappingValue);
            }

            grid.CompleteCells.Add(cell);

            // Done in the cell constructor
            // for (int j = 0; j < 6; j++)
            // {
            //     var cellMappingVector = new Vector3I(
            //         dataStructFullCellMappings[3 * (6 * i + j)],
            //         dataStructFullCellMappings[3 * (6 * i + j) + 1],
            //         dataStructFullCellMappings[3 * (6 * i + j) + 2]);
            //     cell.DualCellsMapping.Add(j, cellMappingVector);
            // }
        }

        for (int i = 0; i < dataStructPendingCellIndices.Length / 2; i++)
        {
            Vector2I pendingCellIndex = new Vector2I(
                dataStructPendingCellIndices[2 * i],
                dataStructPendingCellIndices[2 * i + 1]);
            var cell = new HexTerrainCell(pendingCellIndex, frame, dualFrame);
            grid.PendingCells.Add(cell.CellCoordsImplicit, cell);

            for (int j = 0; j < 6; j++)
            {
                if (dataStructPendingCellsVisitsMappingKey[3 * (6 * i + j)] != Int32.MaxValue)
                {
                    var cellMappingKey = new Vector3I(
                        dataStructPendingCellsVisitsMappingKey[3 * (6 * i + j)],
                        dataStructPendingCellsVisitsMappingKey[3 * (6 * i + j) + 1],
                        dataStructPendingCellsVisitsMappingKey[3 * (6 * i + j) + 2]);
                    var cellMappingValue = new Vector2I(
                        dataStructPendingCellsVisitsMappingValue[2 * (6 * i + j)],
                        dataStructPendingCellsVisitsMappingValue[2 * (6 * i + j) + 1]);
                    cell.Visits.Add(cellMappingKey, cellMappingValue);
                }
            }
        }

        return grid;
    }
}