using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Chunk of hexagonal cells that takes a data source map and generates two terrain meshes from it.
/// </summary>
/// 
/// The source map is a cartesian grid, storing the output of two separate <b>triangular</b> vertex brushes.
/// This map can also be seen as a cluster of height values in the dual hexagonal grid.
/// Each underlying hexagonal cell is then split into 6 equilateral triangles
public class HexagonalTerrainChunk
{
    /// <summary>
    /// Coordinates of the current chunk in the global plane frame
    /// </summary>
    public Vector2I Coordinates { get; set; }

    public ((int, int), (int, int)) GeometryModeParameters;


    /// <summary>
    /// The size of the chunk (measured in cells)
    /// </summary>
    public Vector3I Dimensions
    {
        get => _dimension;
        set
        {
            _dimension = value;
            _dimension2D = new Vector2I(value.X, value.Y);
        }
    }

    public Vector2I Dimensions2D
    {
        get => _dimension2D;
        private init
        {
            _dimension2D = value;
            _dimension = new Vector3I(value.X, value.Y, TerrainSettings.OrientationSystem.PolygonCount);
        }
    }

    // Fields backing the dimension properties
    private Vector3I _dimension;
    private Vector2I _dimension2D;

    /// <summary>
    /// The triangle-based grid holding the data.
    /// </summary>
    public TriangleGrid DataGrid { get; set; }

    /// <summary>
    /// The dual grid used for data representation.
    /// </summary>
    public HexagonGrid _terrainDualGrid;


    /// <summary>
    /// Stores the coordinates of the neighboring chunks that exist that 
    /// </summary>
    public HashSet<Vector2I> existingNeighbors { get; set; } = new();

    /// <summary>
    /// Neighbor-only aware chunk provider.
    /// The coordinates to provide are the offset coordinates.  
    /// </summary>
    private readonly Func<Vector2I, HexagonalTerrainChunk> _neighborChunksProvider;

    /// Helper for computing/interpolating cell colors.
    private readonly VertexColorHelper _colorHelper;

    /// <summary>
    /// Dirtiness (need to reprocess) flag for the chunk.
    /// </summary>
    public bool Dirty { get; set; }

    // TODO : use GeometryModeParameters
    /// <summary>
    /// Property for the threshold value for which a
    /// new geometry may be created along an edge of the terrain.
    /// </summary>
    public float MergeThreshold { get; set; }

    /// <summary>
    /// Default geometry mode value 
    /// </summary>
    public Tuple<GeometryMode, GeometryMode> DefaultGeometryModes { get; set; }

    /// <summary>
    /// Default threshold computation method and value
    /// </summary>
    public Tuple<float, ThresholdComputationMode> DefaultThreshold = new(MathF.PI / 4, ThresholdComputationMode.Angle);


    /// <summary>
    /// Data holder for the chunk's color data
    /// </summary>
    public TerrainColorMaps ColorMaps { get; internal set; }

    /// <summary>
    /// Chunk's type of merge operation
    /// </summary>
    public int MergeMode { get; set; } = 1;

    public Dictionary<Vector3I, bool> NeedUpdate { get; } = new();

    /// <summary>
    /// Chunk constructor.
    /// </summary>
    /// <param name="chunkCoordinates">Position of the chunk in the parent chunk grid</param>
    /// <param name="dimension">Size of the chunk, in amount of triangular cells</param>
    /// <param name="neighboringChunkDataHandle">Neighbor-only aware chunk provider.</param>
    /// <param name="dataSource">Starting height data for the 0-triangle cells of the triangular tiling</param>
    /// <param name="dataSource2">Starting height data for the 1-triangle cells of the triangular tiling</param>
    public HexagonalTerrainChunk(Vector2I chunkCoordinates,
        Vector2I dimension,
        Func<Vector2I, HexagonalTerrainChunk> neighboringChunkDataHandle,
        float[][]? dataSource = null,
        float[][]? dataSource2 = null)
    {
        Coordinates = chunkCoordinates;
        Dimensions2D = dimension;
        existingNeighbors.Add(Vector2I.Zero); // Register the chunk as its own neighbor

        var src1 = dataSource ?? new float[dimension.X][];
        var src2 = dataSource2 ?? new float[dimension.X][];
        var chunkPos = GetChunkGlobalPosition(chunkCoordinates, TerrainSettings.OrientationSystem);
        RegularUniformFrame terrainFrame = TerrainSettings.OrientationSystem.OffsetBy(chunkPos);
        DataGrid = TriangleGrid.BuildFrom(src1, src2, terrainFrame);

        _neighborChunksProvider = neighboringChunkDataHandle;
        _terrainDualGrid = HexagonGrid.BuildFromDual(
            DataGrid,
            Dimensions2D,
            v => neighboringChunkDataHandle(v).DataGrid,
            v => existingNeighbors.Contains(v));
        _colorHelper = new VertexColorHelper(_neighborChunksProvider);
        NeedUpdate = new();
    }

    public void InitializeColorMaps()
    {
        ColorMaps = new TerrainColorMaps(this,
            new Dictionary<Vector3I, Color>(),
            new Dictionary<Vector3I, Color>(),
            new Dictionary<Vector3I, Color>(),
            new Dictionary<Vector3I, Color>());

        for (int x = 0; x < Dimensions2D.X; x++)
        {
            for (int y = 0; y < Dimensions2D.Y; y++)
            {
                for (int z = 0; z < DataGrid.OrientationSystem.PolygonCount; z++)
                {
                    var cell = new Vector3I(x, y, z);
                    ColorMaps.SetGroundColor0(cell, new Color(0, 0, 0, 0));
                    ColorMaps.SetGroundColor1(cell, new Color(0, 0, 0, 0));
                    ColorMaps.SetWallColor0(cell, new Color(1, 0, 0, 0)); // Defaults to texture slot 0
                    ColorMaps.SetWallColor1(cell, new Color(1, 0, 0, 0));
                }
            }
        }
    }

    /// <summary>
    /// Returns the stored height data for a given position if it exists
    /// </summary>
    /// <param name="cartesianPos"></param>
    /// <returns></returns>
    public float GetHeightFromCartesianCoords(Vector2D cartesianPos)
    {
        Vector3I cellCoords = DataGrid.GetCellCoordFromCartesian(cartesianPos);
        var data = DataGrid.Data[new Vector3I(
            cellCoords.X % Dimensions2D.X,
            cellCoords.Y % Dimensions2D.Y,
            cellCoords.Z)];
        return data;
    }

    public float GetHeightFromTriCellCoords(Vector3I cellCoords)
    {
        try
        {
            var offset = HexTerrainCell.GetChunkOffsetForDualCell(_dimension2D, cellCoords);
            var grid = _neighborChunksProvider(offset).DataGrid;

            var scaledOffset = new Vector3I(offset.X * _dimension2D.X, offset.Y * _dimension2D.Y, 0);
            return grid.Data[cellCoords + scaledOffset];
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public Vector2D GetChunkGlobalPosition(Vector2I chunkIndex, RegularUniformFrame referenceFrame)
    {
        var buildVectors = referenceFrame.Transform;

        return new Vector2D(
            buildVectors[0, 0] * Dimensions2D.X * chunkIndex.X +
            buildVectors[0, 1] * Dimensions2D.Y * chunkIndex.Y,
            buildVectors[1, 0] * Dimensions2D.X * chunkIndex.X +
            buildVectors[1, 1] * Dimensions2D.Y * chunkIndex.Y
        );
    }

    public float GetDataFromTriangles(Vector2D position)
    {
        var triCell = DataGrid.OrientationSystem.GetCell(position);
        var triIdx = DataGrid.OrientationSystem.GetPolygonIndexFromCartesian(position, triCell);
        return GetHeightFromTriCellCoords(new Vector3I(triCell.X, triCell.Y, triIdx));
    }

    /// <summary>
    /// Returns the list of triangle tiles' coordinates of the chunk that are affected by the addition of a border
    /// </summary>
    /// <param name="neighborChunkIndex">the index of the neighboring chunk</param>
    /// <returns></returns>
    public List<Vector3I> GetTriCellsTouchingNeighbour(Vector2I offset)
    {
        var offset3D = new Vector3I(offset.X, offset.Y, 0);

        return DataGrid.Points.Where(pos =>
        {
            switch (offset.X)
            {
                case 1 when offset.Y == 0:
                    return pos.X == Dimensions2D.X - 1;
                case 1 when offset.Y == 1:
                    return pos.X == Dimensions2D.X - 1 && pos.Y == Dimensions2D.Y - 1;
                case 1 when offset.Y == -1:
                    return pos.X == Dimensions2D.X - 1 && pos.Y == 0;
                case -1 when offset.Y == 0:
                    return pos.X == 0;
                case -1 when offset.Y == 1:
                    return pos.X == 0 && pos.Y == Dimensions2D.Y - 1;
                case -1 when offset.Y == -1:
                    return pos is { X: 0, Y: 0 };
            }

            return offset.Y switch
            {
                1 => pos.Y == Dimensions2D.Y - 1,
                -1 => pos.Y == 0,
                _ => false
            };
        }).Select(pos => pos + offset3D).ToList();
    }

    /// <summary>
    /// Returns the hexagonal cells of a chunk.
    /// </summary>
    public List<HexTerrainCell> GetHexCells(Func<HexTerrainCell, bool>? predicate = null)
    {
        var result = new List<HexTerrainCell>(_terrainDualGrid.CompleteCells.Where(predicate ?? (_ => true)));
        result.AddRange(
            _terrainDualGrid.PendingCells
                .Where(c => c.Value != null && (predicate?.Invoke(c.Value) ?? true))
                .Select(kvp => kvp.Value));
        return result;
    }

    private Vector2I ApplyBorderFrom(HexagonalTerrainChunk neighbor)
    {
        var offset = neighbor.Coordinates - Coordinates;
        var offset3D = new Vector3I(offset.X, offset.Y, 0) * Dimensions;

        // For debug
        var foundCells = 0;
        //

        existingNeighbors.Add(offset);

        var triCells = GetTriCellsTouchingNeighbour(offset);
        foreach (var triCell in triCells)
        {
            // There may not be a corresponding value for every cell if the cell the border is expanding from 
            // has an extra border-related datapoint.
            if (neighbor.DataGrid.Data.TryGetValue(triCell - offset3D, out var newCellData))
            {
                foundCells++;
                _terrainDualGrid.AddDeltaTileCellValues(
                    triCell,
                    Dimensions2D,
                    v => _neighborChunksProvider(v).DataGrid,
                    v => existingNeighbors.Contains(v));
                NeedUpdate[triCell - offset3D] = true;
            }
        }

        return new Vector2I(triCells.Count, foundCells);
    }

    /// <summary>
    /// Processes the chunk borders between two chunk indexes/coordinates. 
    /// </summary>
    /// <param name="other"></param>
    /// <param name="func"></param>
    /// <returns>The chunk index that will receive the data from the border, or null if none</returns>
    private Vector2I? ProcessNewChunkBorder(HexagonalTerrainChunk other)
    {
        if (Math.Abs(Coordinates.X - other.Coordinates.X) > 1 || Math.Abs(Coordinates.Y - other.Coordinates.Y) > 1)
        {
            //No possible border
            return null;
        }

        return ApplyBorderFrom(other);
    }

    /// <summary>
    /// Processes the border cells of a chunk.
    /// If there is a neighbor chunk, this method will propagate
    /// the values on the boundary of the previously existing chunk to the provided chunk.
    /// This method also performs cell visits for the pending cells of each chunk. 
    /// </summary>
    public HashSet<Vector2I> ProcessChunkBorderCells()
    {
        HashSet<HexagonalTerrainChunk> neighbors =
        [
            _neighborChunksProvider.Invoke(Vector2I.Up),
            _neighborChunksProvider.Invoke(Vector2I.Up + Vector2I.Left),
            _neighborChunksProvider.Invoke(Vector2I.Up + Vector2I.Right),
            _neighborChunksProvider.Invoke(Vector2I.Down),
            _neighborChunksProvider.Invoke(Vector2I.Down + Vector2I.Left),
            _neighborChunksProvider.Invoke(Vector2I.Down + Vector2I.Right),
            _neighborChunksProvider.Invoke(Vector2I.Left),
            _neighborChunksProvider.Invoke(Vector2I.Right)
        ];

        neighbors = neighbors.Where(c => c != null).ToHashSet();

        HashSet<Vector2I> chunksFlaggedForRebuild = new HashSet<Vector2I>();
        // //Debug statement
        // Console.WriteLine(Coordinates);
        foreach (var neighbor in neighbors)
        {
            var editedChunkStatistics = ProcessNewChunkBorder(neighbor);
            // Border debug stats
            //Console.WriteLine("<== "+neighbor.Coordinates + editedChunkStatistics.Value.Y + "/" + editedChunkStatistics.Value.X + "Border cells processed.");
            if (editedChunkStatistics is { Y: > 0 })
            {
                chunksFlaggedForRebuild.Add(neighbor.Coordinates);
            }
        }

        return chunksFlaggedForRebuild;
    }

    public Dictionary<string, Color> BlendColors(HexTerrainCell cell, Vector3 pos, Vector2 uv, bool b)
    {
        return _colorHelper.BlendColors(this, cell, pos, uv, b);
    }

    internal void CopyPointDataToCellStructures(Vector3 p, Vector2 _uv, HexTerrainCell cell)
    {
        //UV - used for ledge detection. X = closeness to top terrace, Y = closeness to bottom of terrace
        //Walls will always have UV of 1, 1
        Vector2 uv = _uv;

        Vector2 uv2 = cell.FloorMode
            ? new Vector2(p.X, p.Z) / 1f / MathF.Sqrt(3)
            : new Vector2(p.X, p.Y) + new Vector2(p.Z, p.Y);

        var data = cell.TempDataArrays;

        data.Pt.Add(p);
        data.Uv.Add(uv);
        data.Uv2.Add(uv2);
        var colors = BlendColors(cell, p, uv, true);
        data.Custom1Value.Add(colors["custom_1_value"]);
        data.Color0.Add(colors["color_0"]);
        data.Color1.Add(colors["color_1"]);
        // Pack two colors in a single channel using half-precision (16 bits)
        //
        var parameters = cell.ParametersOverride ?? GeometryModeParameters;
        var color1 = (cell.GeometryModesOverride ?? DefaultGeometryModes).Item1.GetModePalette()(
            parameters.Item1.Item1,
            parameters.Item1.Item2);
        var color2 = (cell.GeometryModesOverride ?? DefaultGeometryModes).Item2.GetModePalette()(
            parameters.Item1.Item1,
            parameters.Item1.Item2);

        var bytesR = BitConverter.GetBytes((Half)color1.R).Concat(BitConverter.GetBytes((Half)color2.R)).ToArray();
        var bytesG = BitConverter.GetBytes((Half)color1.G).Concat(BitConverter.GetBytes((Half)color2.G)).ToArray();
        var bytesB = BitConverter.GetBytes((Half)color1.B).Concat(BitConverter.GetBytes((Half)color2.B)).ToArray();
        var bytesA = BitConverter.GetBytes((Half)color1.A).Concat(BitConverter.GetBytes((Half)color2.A)).ToArray();

        var PackedColors = new Color(
            BitConverter.ToSingle(bytesR),
            BitConverter.ToSingle(bytesG),
            BitConverter.ToSingle(bytesB),
            BitConverter.ToSingle(bytesA));
        data.Custom3Value.Add(PackedColors);

        data.MatBlend.Add(colors["mat_blend"]);
        data.Floor.Add(cell.FloorMode);
    }

    public Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> ProcessGeometry(bool forceRebuild = false)
    {
        var editor = new ChunkConformalEditor(this);
        //Step 1 : Collect all the required operations
        editor.CollectAllOperations(forceRebuild);
        //Step 2 : Apply all the operations on triangulations
        editor.ApplyGeometryOperations();
        //Step 3 : Extract the triangles
        return editor.GetTriangleInfos();
    }
}

public enum ThresholdComputationMode
{
    Angle,
    HeightDifference
}

public static class ThresholdComputationModeExtensions
{
    public static bool IsOverThreshold(this ThresholdComputationMode mode, float threshold, Vector3 a, Vector3 b)
    {
        switch (mode)
        {
            case ThresholdComputationMode.Angle:
                return (b - a).AngleTo((b - a).Slide(Vector3.Up)) >= threshold;
            case ThresholdComputationMode.HeightDifference:
                return Math.Abs(b.Y - a.Y) >= threshold;
            default:
                return false;
        }
    }
}

public enum GeometryMode
{
    FlatHexagons = 0,
    FlatTriangles = 1,
    SmoothLinear = 2,
    Foothill = 3,
    Plateau = 4,
    BendingEdge = 5,
    FlatHexagonsNoFans = 6,
    FlatTrianglesNoFans = 7
}

/// <summary>
/// Extension class for defining methods using the GeometryMode enum
/// </summary>
public static class GeometryModeExtensions
{
    private static float lMin = 0.3f;

    private static float lMax = 0.8f;

    //Hue-margin
    private static float hMargin = 0.2f;

    //Saturation margin
    private static float saturationMargin = 0.3f;

    public static bool SupportsParameter(this GeometryMode gMode,int parameterIdx)
    {
        bool supportsParameter;
        if (parameterIdx >= 2)
        {
            throw new NotSupportedException();
        }
        switch (gMode)

        {
            case GeometryMode.FlatHexagonsNoFans:
            case GeometryMode.FlatTrianglesNoFans:
            case GeometryMode.FlatTriangles:
            case GeometryMode.FlatHexagons:
            case GeometryMode.SmoothLinear:
                supportsParameter = false;
                break;
            case GeometryMode.Foothill:
            case GeometryMode.Plateau:
            case GeometryMode.BendingEdge:
                supportsParameter = true;
                break;
            default:
                throw new NotSupportedException();
        }
        return supportsParameter;
    }
    
    /// <summary>
    /// Returns the palette method (a double parametered float function)
    /// for a given GeometryMode.
    /// The input range for the resulting function is [0,1]
    /// </summary>
    /// <param name="gMode"></param>
    /// <returns></returns>
    //TODO : Use Color.FromOkHSl when fixed on Godot lib
    public static Func<float?, float?, Color> GetModePalette(this GeometryMode gMode)
    {
        //We quantize the HSL space into as many quadrants as there are GeometryModes.
        //The output palettes will have lightness values between lMin and lMax.
        var enumArray = Enum.GetValues(typeof(GeometryMode));

        var count = enumArray.Length;
        var idx = (int)gMode;
        if (idx < 0 || idx > count)
        {
            throw new Exception("This is not supported");
        }

        var hueMin = (idx - 1f) / count;
        var hueMax = (float)idx / count;
        var hueMid = (hueMax + hueMin) / 2;
        var lMid = (lMin + lMax) / 2;
        var sDefault = 0.8f;

        var v = (float s, float l) => l + s * Math.Min(l, 1 - l);
        var vMid = v(sDefault, lMid);
        // Parameter #1 handling
        bool supportsParameter1 = SupportsParameter(gMode, 0);
        bool supportsParameter2 = SupportsParameter(gMode,1);

        if (supportsParameter2)
        {
            if (!supportsParameter1)
            {
                throw new InvalidOperationException("Geometry mode is said to support 2 parameters," +
                                                    " but not 1. Verify your implementations.");
            }

            //Double ended function we map
            // the hue to the middle or the range
            // the saturation to [margin,1-margin] 
            //the lightness to [lMin, lMax]
            return (p1, p2) =>
            {
                if (p1 is > 1 or < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(p1));
                }

                if (p2 is > 1 or < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(p2));
                }

                var x = Color.FromHsv(hueMid,
                    Mathf.Lerp(saturationMargin, 1 - saturationMargin, p1.Value),
                    v(
                        Mathf.Lerp(saturationMargin, 1 - saturationMargin, p1.Value),
                        Mathf.Lerp(lMin, lMax, p2.Value))
                );
                return x;
            };
        }

        if (supportsParameter1)
        {
            //single paramerized function
            //We map with the saturation, with lightness fixed @ lMin+lMax/2
            return (p1, _) =>
            {
                if (p1 is > 1 or < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(p1));
                }

                var x = Color.FromHsv(hueMid,
                    Mathf.Lerp(saturationMargin, 1 - saturationMargin, p1.Value),
                    vMid);
                return x;
            };
        }

        //Constant
        return (_, _) =>
        {
            // https://github.com/godotengine/godot/issues/118138 : Cannot use Color.FromOkHsl on linux
            var x = Color.FromHsv(hueMid, sDefault, vMid);
            return x;
        };
    }
}