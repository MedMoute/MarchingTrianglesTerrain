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
/// Each underlying hexagonal cell is then split into 6 equilateral triangles, and we apply the Marching Triangles algorithm to
/// extract a mesh for it.
public class HexagonalTerrainChunk
{
    /// <summary>
    /// Coordinates of the current chunk in the global plane frame
    /// </summary>
    public Vector2I Coordinates { get; }

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
    public TriangleGrid DataGrid { get; }

    /// <summary>
    /// The dual grid used for data representation.
    /// </summary>
    protected internal HexagonGrid _terrainDualGrid;

    /// <summary>
    /// Stores the coordinates of the neighboring chunks that exist that 
    /// </summary>
    private readonly HashSet<Vector2I> _createdNeighbors = new();

    /// <summary>
    /// Neighbor-only aware chunk provider.
    /// The coordinates to provide are the offset coordinates.  
    /// </summary>
    private readonly Func<Vector2I, HexagonalTerrainChunk> _neighborChunksProvider;

    /// Helper for computing/interpolating cell colors.
    private readonly VertexColorHelper _colorHelper ;

    /// <summary>
    /// Dirtiness (need to reprocess) flag for the chunk.
    /// </summary>
    public bool Dirty { get; set; }

    // TODO : Marching triangle properties should be encapsulated
    /// <summary>
    /// Property for the threshold value for which a
    /// new geometry may be created along an edge of the terrain.
    /// </summary>
    public float MergeThreshold { get; set; }


    /// <summary>
    /// Data holder for the chunk's 
    /// </summary>
    public TerrainColorMaps ColorMaps { get; private set; }

    public int MergeMode => 1;

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
        float[][] dataSource = null,
        float[][] dataSource2 = null)
    {
        Coordinates = chunkCoordinates;
        Dimensions2D = dimension;
        _createdNeighbors.Add(Vector2I.Zero); // Register the chunk as its own neighbor

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
            v => _createdNeighbors.Contains(v));
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

    public List<List<List<float>>> CopyHeightMapData()
    {
        var data = new List<List<List<float>>>();

        foreach (var dataSource in DataGrid.Data)
        {
            var tile = dataSource.Key;
            data[tile.X][tile.Y][tile.Z] = dataSource.Value;
        }

        return data;
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
    public List<HexTerrainCell> GetHexCells()
    {
        var result = new List<HexTerrainCell>(_terrainDualGrid.CompleteCells);
        var pendingCells = _terrainDualGrid.PendingCells.Where(c => c.Value != null);
        foreach (var pendingCell in pendingCells)
        {
            result.Add(pendingCell.Value);
        }

        return result;
    }

    private Vector2I ApplyBorderFrom(HexagonalTerrainChunk neighbor)
    {
        var offset = neighbor.Coordinates - Coordinates;
        var offset3D = new Vector3I(offset.X, offset.Y, 0) * Dimensions;

        // For debug
        var foundCells = 0;
        //

        _createdNeighbors.Add(offset);

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
                    v => _createdNeighbors.Contains(v));
                NeedUpdate[triCell-offset3D] = true;
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
        Console.WriteLine(Coordinates);
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

    public Dictionary<string, Color> BlendColors(GdPluginHexTerrainChunk chunk, HexTerrainCell cell, Vector3 pos, Vector2 uv, bool b)
    {
        return _colorHelper.BlendColors(chunk, cell, pos, uv, b);
    }
}