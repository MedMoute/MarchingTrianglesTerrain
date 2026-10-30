using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;
using MathNet.Spatial.Euclidean;
using TerrainSettings = MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils.TerrainSettings;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

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
    public HexagonGrid TerrainDualGrid;


    /// <summary>
    /// Stores the coordinates of the neighboring chunks that exist that 
    /// </summary>
    public HashSet<Vector2I> ExistingNeighbors { get; set; } = new();

    /// <summary>
    /// Neighbor-only aware chunk provider.
    /// The coordinates to provide are the offset coordinates.  
    /// </summary>
    private Func<Vector2I, HexagonalTerrainChunk?> _neighborChunksProvider;

    /// Helper for computing/interpolating cell colors.
    private VertexColorHelper _colorHelper;

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
    public Tuple<GeometryMode, GeometryMode>? DefaultGeometryModes { get; set; }

    /// <summary>
    /// Default chunk geometry mode parameters 
    /// </summary>
    public ((float, float), (float, float))? GeometryModeParameters;


    /// <summary>
    /// Default threshold computation method and value
    /// </summary>
    public Tuple<float, ThresholdComputationMode> DefaultThreshold = new(MathF.PI / 4, ThresholdComputationMode.Angle);


    /// <summary>
    /// Data holder for the chunk's color data
    /// </summary>
    public TerrainColorMaps? ColorMaps { get; internal set; }

    /// <summary>
    /// Chunk's type of merge operation
    /// </summary>
    public int MergeMode { get; set; } = 1;

    public Dictionary<Vector3I, bool> NeedUpdate { get; set; }

    //TEMP

    public TerrainChunkGraph Graph { get; private set; }

    public static HexagonalTerrainChunk NewChunkAtCoordinates(Vector2I chunkCoordinates,
        Vector2I dimension,
        Func<Vector2I, HexagonalTerrainChunk?> neighboringChunkDataHandle,
        float[][]? dataSource = null,
        float[][]? dataSource2 = null)
    {
        return new HexagonalTerrainChunk(
            chunkCoordinates,
            dimension,
            neighboringChunkDataHandle,
            dataSource,
            dataSource2);
    }

    /// <summary>
    /// Chunk constructor.
    /// </summary>
    /// <param name="chunkCoordinates">Position of the chunk in the parent chunk grid</param>
    /// <param name="dimension">Size of the chunk, in amount of triangular cells</param>
    /// <param name="neighboringChunkDataHandle">Neighbor-only aware chunk provider.</param>
    /// <param name="dataSource">Starting height data for the 0-triangle cells of the triangular tiling</param>
    /// <param name="dataSource2">Starting height data for the 1-triangle cells of the triangular tiling</param>
    private HexagonalTerrainChunk(Vector2I chunkCoordinates,
        Vector2I dimension,
        Func<Vector2I, HexagonalTerrainChunk?> neighboringChunkDataHandle,
        float[][]? dataSource = null,
        float[][]? dataSource2 = null)
    {
        Coordinates = chunkCoordinates;
        Dimensions2D = dimension;
        ExistingNeighbors.Add(Vector2I.Zero); // Register the chunk as its own neighbor

        var src1 = dataSource ?? new float[dimension.X][];
        var src2 = dataSource2 ?? new float[dimension.X][];
        var chunkPos = GetChunkGlobalPosition(chunkCoordinates, TerrainSettings.OrientationSystem);
        IRegularUniformFrame terrainFrame = TerrainSettings.OrientationSystem.OffsetBy(chunkPos);
        DataGrid = TriangleGrid.BuildFrom(src1, src2, terrainFrame);
        _neighborChunksProvider = neighboringChunkDataHandle;
        
        TerrainDualGrid = HexagonGrid.BuildFromDual(
            DataGrid,
            Dimensions2D,
            v => (v == Vector2I.Zero ? this : neighboringChunkDataHandle(v))?.DataGrid,
            v => ExistingNeighbors.Contains(v));
        _colorHelper = new VertexColorHelper(neighboringChunkDataHandle);
        NeedUpdate = new();

        Graph = BuildEdgeAdjacencyGraph();
        FillGraphTriangulationData();
    }


    /// <summary>
    /// Edits the chunk's <see cref="TerrainChunkGraph"/> to add triangulations.
    /// </summary>
    /// The triangulations are created so that the first vertex matches a cell center.
    internal void FillGraphTriangulationData()
    {
        //Create the triangulations
        foreach (var cell in TerrainDualGrid.CompleteCells.Values)
        {
            for (int i = 0; i < HexTerrainCell.VertexCount; i++)
            {
                var a2D = cell.CenterPosition;
                var b2D = cell.VertexPositionsInPlane[i];
                var c2D = cell.VertexPositionsInPlane[EngineUtils.Mod(i + 1, HexTerrainCell.VertexCount)];
                var a = new Vector3(
                    (float)a2D.X,
                    float.IsNaN(cell.AverageHeight) ? float.MaxValue : cell.AverageHeight,
                    (float)a2D.Y);
                
                var b = new Vector3(
                    (float)b2D.X,
                    float.IsNaN(cell.GetVertexData!(i)) ? float.MaxValue : cell.GetVertexData!(i),
                    (float)b2D.Y);

                var c = new Vector3(
                    (float)c2D.X,
                    float.IsNaN(cell.GetVertexData!(EngineUtils.Mod(i + 1, HexTerrainCell.VertexCount)))
                        ? float.MaxValue
                        : cell.GetVertexData!(EngineUtils.Mod(i + 1, HexTerrainCell.VertexCount)),
                    (float)c2D.Y);

                var triangle = new Triangulation([a, b, c], false, true);
                Graph.AddTriangulationToMap(cell.CellCoordsImplicit, i, triangle);
                //We set the triangulation for each graph node
                Graph.RegisterTriangulationInGraph(cell, i, triangle);
            }
        }

        Graph.RegisterTriangulationDtos();
    }


    public HexagonalTerrainChunk? GetNeighborChunk(Vector2I chunk)
    {
        return _neighborChunksProvider.Invoke(chunk);
    }

    public Vector3I GetDataCellCoordinatesFromHexGridSummit(Vector3I hexCellCoords)
    {
        return DataGrid.OrientationSystem.GetVertexIndexInDualSpace(
            new Vector3I(hexCellCoords.X, hexCellCoords.Y, 0),
            TerrainDualGrid.Frame,
            hexCellCoords.Z);
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
        var offset = HexTerrainCell.GetChunkOffsetForDualCell(_dimension2D, cellCoords);
        var grid = _neighborChunksProvider(offset)?.DataGrid;

        if (grid is null)
        {
            return 0f;
        }

        var scaledOffset = new Vector3I(offset.X * _dimension2D.X, offset.Y * _dimension2D.Y, 0);
        return grid.Data[cellCoords + scaledOffset];
    }

    public Vector2D GetChunkGlobalPosition(Vector2I chunkIndex, IRegularUniformFrame referenceFrame)
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
    private List<Vector3I> GetTriCellsTouchingNeighbour(Vector2I offset)
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
        var result = new List<HexTerrainCell>(TerrainDualGrid.CompleteCells.Values.Where(predicate ?? (_ => true)));
        result.AddRange(
            TerrainDualGrid.PendingCells
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

        ExistingNeighbors.Add(offset);

        var triCells = GetTriCellsTouchingNeighbour(offset);
        foreach (var triCell in triCells)
        {
            // There may not be a corresponding value for every cell if the cell the border is expanding from 
            // has an extra border-related datapoint.
            if (neighbor.DataGrid.Data.ContainsKey(triCell - offset3D))
            {
                foundCells++;
                TerrainDualGrid.AddDeltaTileCellValues(
                    triCell,
                    Dimensions2D,
                    v => _neighborChunksProvider(v)!.DataGrid,
                    v => ExistingNeighbors.Contains(v));
                NeedUpdate[triCell - offset3D] = true;
            }
        }

        return new Vector2I(triCells.Count, foundCells);
    }

    /// <summary>
    /// Processes the chunk borders between two chunk indexes/coordinates. 
    /// </summary>
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
        if (_neighborChunksProvider == null)
        {
            throw new InvalidOperationException("the chunk provider is not set properly");
        }

        HashSet<HexagonalTerrainChunk?> neighbors =
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

        var neighborsNotNull = neighbors.Where(c => c != null)
            .Select(c => c!)
            .ToHashSet();

        HashSet<Vector2I> chunksFlaggedForRebuild = new HashSet<Vector2I>();
        // //Debug statement
        // Console.WriteLine(Coordinates);
        foreach (var neighbor in neighborsNotNull)
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

    private Dictionary<string, Color> BlendColors(HexTerrainCell cell, Vector3 pos, Vector2 uv, bool b)
    {
        return _colorHelper.BlendColors(this, cell, pos, uv, b);
    }

    internal void CopyPointDataToCellStructures(Vector3 p, Vector2 uv, HexTerrainCell cell)
    {
        //UV - used for ledge detection. X = closeness to top terrace, Y = closeness to bottom of terrace
        //Walls will always have UV of 1, 1

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
        var color1 = (cell.GeometryModesOverride
                      ?? DefaultGeometryModes
                      ?? throw new InvalidOperationException("DefaultGeometryModes should have been set")).Item1
            .GetModePalette()(
                parameters!.Value.Item1.Item1,
                parameters.Value.Item1.Item2);
        var color2 = (cell.GeometryModesOverride
                      ?? DefaultGeometryModes
                      ?? throw new InvalidOperationException("DefaultGeometryModes should have been set")).Item2
            .GetModePalette()(
                parameters.Value.Item2.Item1,
                parameters.Value.Item2.Item2);

        var bytesR = BitConverter.GetBytes((Half)color1.R).Concat(BitConverter.GetBytes((Half)color2.R)).ToArray();
        var bytesG = BitConverter.GetBytes((Half)color1.G).Concat(BitConverter.GetBytes((Half)color2.G)).ToArray();
        var bytesB = BitConverter.GetBytes((Half)color1.B).Concat(BitConverter.GetBytes((Half)color2.B)).ToArray();
        var bytesA = BitConverter.GetBytes((Half)color1.A).Concat(BitConverter.GetBytes((Half)color2.A)).ToArray();

        var packedColors = new Color(
            BitConverter.ToSingle(bytesR),
            BitConverter.ToSingle(bytesG),
            BitConverter.ToSingle(bytesB),
            BitConverter.ToSingle(bytesA));
        data.Custom3Value.Add(packedColors);

        data.MatBlend.Add(colors["mat_blend"]);
        data.Floor.Add(cell.FloorMode);
    }

    public Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> ProcessGeometry(
        out ChunkConformalEditor editor, bool forcePrintOutput = false)
    {
        editor = new ChunkConformalEditor(Graph);
        //Step 1 : Collect all the required operations
        editor.CollectAllOperationsRequests();
        editor.CollectBorderOperationRequests();

        //Step 2 : Register the edge Edition operations;
        editor.RegisterOperations();
        //Step 3 : Apply all the operations on triangulations
        editor.ApplyGeometryOperations();
        // Step 1.5 : Stitch edges that need to be fixed
        var res = editor.PostProcess();

        //Step 4 : Extract the triangles
        return res;
    }

    private TerrainChunkGraph BuildEdgeAdjacencyGraph()
    {
        var graph = new TerrainChunkGraph(this);

        //Create all the GraphNodes : 
        var nodes = new List<ChunkTerrainNode>();
        var cellCenters = new Dictionary<Vector2I, CellCenterPoint>();
        var cellSummits = new Dictionary<Vector3I, CellSummitPoint>();
        foreach (var vertexAndData in DataGrid.Data) //Add the data points
        {
            var cellSummit = new CellSummitPoint(DataGrid, vertexAndData.Key,graph);
            nodes.Add(cellSummit);
            cellSummits.Add(vertexAndData.Key, cellSummit);
        }

        foreach (var fullCell in TerrainDualGrid.CompleteCells.Values) //Add the cell centers
        {
            var idx = fullCell.CellCoordsImplicit;
            var cellCenter = new CellCenterPoint(fullCell,graph);
            nodes.Add(cellCenter);
            cellCenters.Add(idx, cellCenter);
        }

        var edges = ChunkTerrainEdge.BuildEdgesFromChunkAndNodes(this, cellSummits, cellCenters);

        foreach (var terrainEdge in edges)
        {
            var added = graph.AddVertex(terrainEdge.Source);
            if (added) graph.AddNodeToMap(terrainEdge.Source.Position, terrainEdge.Source);
            added = graph.AddVertex(terrainEdge.Target);
            if (added) graph.AddNodeToMap(terrainEdge.Target.Position, terrainEdge.Target);
            if (graph.Edges.Any(edge => terrainEdge.Equals(edge))) continue;
            graph.AddEdge(terrainEdge);
        }

        return graph;
    }

    //
    public void UpdateFullCellCenterValues(ICollection<Vector3I> dataIndexes)
    {
        Graph.UpdateCellCenterValuesInTris(dataIndexes);
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
    FlatHexagons = 4,
    FlatTriangles = 5,
    SmoothLinear = 0,
    BendingEdge = 1,
    Foothill = 2,
    Plateau = 3,
    FlatHexagonsNoFans = 6,
    FlatTrianglesNoFans = 7
}

/// <summary>
/// Extension class for defining methods using the GeometryMode enum
/// </summary>
public static class GeometryModeExtensions
{
    /// <summary>
    /// Minimum lightness
    /// </summary>
    private const float LMin = 0.3f;

    /// <summary>
    /// Maximum lightness
    /// </summary>
    private const float LMax = 0.8f;

    /// <summary>
    /// Saturation margin value: the range of values will be [sMargin,1-sMargin]
    /// </summary>
    private const float SaturationMargin = 0.3f;

    public static bool SupportsParameter(this GeometryMode gMode, int parameterIdx)
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

    public static GeometryModeActions.ModeProcessor Processor(this GeometryMode gMode)
    {
        switch (gMode)
        {
            case GeometryMode.FlatHexagons:
                return FlatHexagons.Processor;
            case GeometryMode.FlatTriangles:
                return FlatTriangles.Processor;
            case GeometryMode.BendingEdge:
                return BendingEdge.Processor;
            case GeometryMode.Foothill:
                return Foothill.Processor;
            case GeometryMode.Plateau:
                return Plateau.Processor;
            case GeometryMode.SmoothLinear:
                return SmoothLinear.Processor;
            case GeometryMode.FlatHexagonsNoFans:
                return FlatHexagonNoFan.Processor;
            case GeometryMode.FlatTrianglesNoFans:
                return FlatTrianglesNoFan.Processor;
            default:
                throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Returns the palette method (a double parameterized float function)
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
        var lMid = (LMin + LMax) / 2;
        var sDefault = 0.8f;

        var v = (float s, float l) => l + s * Math.Min(l, 1 - l);
        var vMid = v(sDefault, lMid);
        // Parameter #1 handling
        bool supportsParameter1 = SupportsParameter(gMode, 0);
        bool supportsParameter2 = SupportsParameter(gMode, 1);

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
                if (p1 is > 1 or < 0 or null)
                {
                    throw new ArgumentOutOfRangeException(nameof(p1));
                }

                if (p2 is > 1 or < 0 or null)
                {
                    throw new ArgumentOutOfRangeException(nameof(p2));
                }

                var x = Color.FromHsv(hueMid,
                    Mathf.Lerp(SaturationMargin, 1 - SaturationMargin, p1.Value),
                    v(
                        Mathf.Lerp(SaturationMargin, 1 - SaturationMargin, p1.Value),
                        Mathf.Lerp(LMin, LMax, p2.Value))
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
                if (p1 is > 1 or < 0 or null)
                {
                    throw new ArgumentOutOfRangeException(nameof(p1));
                }

                var x = Color.FromHsv(hueMid,
                    Mathf.Lerp(SaturationMargin, 1 - SaturationMargin, p1.Value),
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