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

    public Dictionary<Vector2I, HexTerrainCell> PendingCells { get; } = new();

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
        Func<Vector2I,bool> chunkTester)
    {
        RegularUniformFrame dualFrame = dualGrid.OrientationSystem;
        RegularUniformFrame frame = dualFrame.GetDual();
        HexagonGrid grid = new(frame, dualFrame);
        foreach (var dataPoint in dualGrid.Data)
        {
            grid.AddDeltaTileCellValues(dataPoint.Key, chunkDimensions, neighborDataGridProvider,chunkTester);
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

            cell.VisitedBy(trianglesTile,dimensions2D);

            if (cell.IsReady())
            {
                CompleteCells.Add(cell);
                PendingCells[cellCoords] = null;
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
}