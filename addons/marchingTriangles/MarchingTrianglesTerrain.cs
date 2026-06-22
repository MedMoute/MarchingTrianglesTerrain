using System.Data;
using Godot;
using Localproto.addons.marchingTriangles.tiling;
using Localproto.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;

namespace Localproto.addons.marchingTriangles;

[Tool]
public partial class MarchingTrianglesTerrain : Node3D
{
    public enum StorageMode
    {
        // Saves load time.
        Baked,

        //Saves disk space
        Runtime
    }

    private StorageMode _storageType = StorageMode.Baked;

    /// <summary>
    /// Chunk dictionary.
    /// Stores transient data.
    /// </summary>
    private Dictionary<Vector2I, GdPluginHexTerrainChunk> _chunks = [];

    /// <summary>
    /// Provides a neighbor-only aware chunk provider for a given chunk.
    /// The coordinates to give to the generated providers are the offset coordinates.  
    /// </summary>
    private readonly Func<Vector2I,Vector2I, HexagonalTerrainChunk?> _neighborChunkProviderProvider;

    public Dictionary<Vector2I, GdPluginHexTerrainChunk> Chunks => _chunks;

    [ExportCategory("Storage Options")]
    [Export]
    public StorageMode StorageType
    {
        get => _storageType;
        set
        {
            if (_storageType == value) return;
            _storageType = value;
            //Mark all chunks dirty to force re-save of data/meshes
            foreach (var hexTerrainChunk in _chunks)
            {
                hexTerrainChunk.Value.Underlying.Dirty = true;
            }

            GD.Print("[MTT] Storage mode changed. All chunks marked for save.");
            NotifyPropertyListChanged();
        }
    }

    /// <summary>
    /// If true, storage will include grass data, ignored if storage_mode = RUNTIME
    /// </summary>
    private bool _bakeGrass = false;

    [Export]
    public bool BakeGrass
    {
        get => _bakeGrass;
        set
        {
            _bakeGrass = value;
            //Mark all chunks dirty to force re-save of data/meshes
            foreach (var hexTerrainChunk in _chunks)
            {
                hexTerrainChunk.Value.Underlying.Dirty = true;
            }
        }
    }

    /// <summary>
    /// If true, storage will include collision shape, ignored if storage_mode = RUNTIME
    /// </summary>
    private bool _bakeCollision = true;

    [Export]
    public bool BakeCollision
    {
        get => _bakeCollision;
        set
        {
            _bakeCollision = value;
            //Mark all chunks dirty to force re-save of data/meshes
            foreach (var hexTerrainChunk in _chunks)
            {
                hexTerrainChunk.Value.Underlying.Dirty = true;
            }
        }
    }

    private string _dataDir = "";

//  The folder where this terrain's data is saved. 
//  If left empty, it automatically fills with a folder name relative to your scene file.
//  Note: Manually setting a path locks the save location even if you rename the terrain node later.
    [Export(PropertyHint.Dir)]
    public string DataDirectory
    {
        get
        {
            if (!Engine.IsEditorHint() || _dataDir.Length != 0) return _dataDir;
            //Auto save path
            var autoPath = MttDataHandler.GenerateDataDirectory(this);
            if (autoPath.Length == 0)
            {
                throw new ConstraintException("Unexpected empty default path for data storage");
            }

            _dataDir = autoPath;
            return _dataDir;
        }
        set => _dataDir = value;
    }

    [ExportCategory("Runtime Baking")]

    // If this option is true, the textures will be baked into a texture atlas
    // at runtime. This will improve rendering performance, but increase cost of generation
    // slightly.
    [Export]
    public bool EnableRuntimeTextureBaking { get; set; } = true;

    // The resolution used per polygon when baking the texture atlas. Increase this value
    // when using high-res textures. Higher values increase the baking time and memory usage
    [Export] public short PolygonTextureRes { get; set; } = 16;

    // Used for overriding the material of the baked terrain texture.
    [Export] public Material BakeMaterialOverride { get; set; }

    // FIXME : Property Storages => play with class property list 

    //True after external storage has been initialized.
    //Used to detect when migration from embedded data is needed.
    [Export] public bool StorageInitialized { get; set; }

    // Tracks the mode used during the last successful save for reporting purposes.
    [Export] public StorageMode LastWorkingStorageMode { get; set; } = StorageMode.Baked;

    [Export] public MarchingTrianglesTexturesPreset CurrentTexturePreset { get; set; }

    public TerrainSettings TerrainSettings { get; }

    public MarchingTrianglesTerrain()
    {
        TerrainSettings = new TerrainSettings(
            this, 
            GD.Load<ShaderMaterial>("res://addons/marchingTriangles/editor/resources/plugin_materials/mst_terrain_shader.tres"));
        
        CurrentTexturePreset = new MarchingTrianglesTexturesPreset();
        
        _neighborChunkProviderProvider = BuildNeighborChunkProvider(i => _chunks.TryGetValue(i, out var chk) ? chk.Underlying : null);

    }

    /// <summary>
    /// Builds a Function providing an offset-ed Chunk only if the offset length is smaller than sqrt(2).
    /// E.g. the function will return for a given position and offset if the chunk at the index[position+offset] exists
    ///  and if || offset ||² is less than 2.
    /// </summary>
    /// <param name="chunkProvider"></param>
    public static Func<Vector2I, Vector2I, HexagonalTerrainChunk> BuildNeighborChunkProvider(Func<Vector2I,HexagonalTerrainChunk> chunkProvider)
    {
        return (chunkCoord,offset) =>
        {
            if (offset.LengthSquared() > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Maximum supported offset" +
                                                                      " length is sqrt(2) for diagonal neighbors.");
            }
            return chunkProvider(offset+chunkCoord);
        };
    }

    public void ForceRebuildTerrain()
    {
        //throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the Cell coordinates on which the provided position belongs
    /// </summary>
    /// <returns></returns>
    public Vector3I GetGlobalCellCoordsFromCartesian(Vector2D pos)
    {
        RegularUniformFrame tiling = TerrainSettings.OrientationSystem;
        var xy = tiling.GetCell(pos);
        return new Vector3I(xy.X, xy.Y, tiling.GetPolygonIndexFromCartesian(pos, xy));
    }

    /// <summary>
    /// Returns the Chunk coordinates on which the provided position belongs
    /// </summary>
    /// <param name="vector2"></param>
    /// <returns></returns>
    public static Vector2I GetChunkCoordsFromCartesian(Vector2D vector2, Vector2I chunkDimensions)
    {
        RegularUniformFrame tiling = TerrainSettings.OrientationSystem;
        Vector2I globalCoords = tiling.GetCell(vector2);

        return new Vector2I(
            (int)Math.Floor((double)globalCoords.X / chunkDimensions.X),
            (int)Math.Floor((double)globalCoords.Y / chunkDimensions.Y));
    }

    public List<Vector3> GetChunkLimits(Vector2I chunkCoords)
    {
        var cellBuildingVectors =
            TerrainSettings.OrientationSystem.Transform;
        Vector2D buildVector1 = Vector2D.OfVector(cellBuildingVectors.Column(0));
        Vector2D buildVector2 = Vector2D.OfVector(cellBuildingVectors.Column(1));

        buildVector1 = new Vector2D(
            buildVector1.X * TerrainSettings.ChunkDimensions.X,
            buildVector1.Y * TerrainSettings.ChunkDimensions.Y);
        //buildVector1 *= TerrainSettings.CellScale;
        var bv1 = new Vector3((float)buildVector1.X, 0, (float)buildVector1.Y);

        buildVector2 = new Vector2D(
            buildVector2.X * TerrainSettings.ChunkDimensions.X,
            buildVector2.Y * TerrainSettings.ChunkDimensions.Y);
        //buildVector2 *= TerrainSettings.CellScale;
        var bv2 = new Vector3((float)buildVector2.X, 0, (float)buildVector2.Y);


        Vector3 cellOrigin = new Vector3(
            (float)(chunkCoords.X * buildVector1.X + chunkCoords.Y * buildVector2.X),
            0,
            (float)(chunkCoords.X * buildVector1.Y + chunkCoords.Y * buildVector2.Y));


        return [cellOrigin, cellOrigin + bv1, cellOrigin + bv2 + bv1, cellOrigin + bv2];
    }


    public void AddNewChunk(Vector2I coords, addons.marchingTriangles.MarchingTrianglesTerrainPlugin plugin)
    {
        //GD.Print("[DEBUG][Terrain node : AddNewChunk] Chunk coords : {0}", coords);
        var newChunk = new GdPluginHexTerrainChunk(
            coords,
            TerrainSettings.ChunkDimensions,
            v => _neighborChunkProviderProvider(coords,v));
        _chunks.Add(coords, newChunk);
        newChunk.Name = "Chunk" + coords;
        AddChunk(coords, newChunk, plugin);
        var chunksToRebuild = newChunk.Underlying.ProcessChunkBorderCells();

        foreach (var rebuiltChunks in chunksToRebuild)
        {
            Chunks[rebuiltChunks].GenerateTerrain(false);
        }
        //Rebuild the chunk if some parts were affected by the border
        newChunk.GenerateTerrain(false);
    }

    public Vector2I GetTempChunkCoords()
    {
        return new Vector2I(
            int.MaxValue - (TerrainSettings.ChunkDimensions.X + 1),
            int.MaxValue - (TerrainSettings.ChunkDimensions.Y + 1));
    }

    public void AddChunk(Vector2I coords, GdPluginHexTerrainChunk newChunk, addons.marchingTriangles.MarchingTrianglesTerrainPlugin plugin)
    {
        AddChild(newChunk);
        EngineUtils.SetOwnerAsSceneRoot(newChunk);
        newChunk.InitializeTerrain();
        if (plugin != null)
        {
            if (plugin.PluginHelper.CurrentSelectedChunk != null &&
                plugin.PluginHelper.CurrentSelectedChunk.Underlying.Coordinates == GetTempChunkCoords())
            {
                plugin.PluginHelper.CurrentSelectedChunk = newChunk;
            }

            plugin.Ui.UiToolAttributes.ShowToolAttributes((int)TerrainToolMode.ChunkManagement);
            plugin.GizmoPlugin.TriggerRedraw(this);
        }

        //GD.Print("[DEBUG][Terrain node : AddChunk] Chunk coords : {0} - Origin {1}",
        //    coords,
        //    newChunk.Underlying.GetChunkGlobalPosition(coords, TerrainSettings.OrientationSystem));
    }

    public void RemoveChunk(Vector2I coords, addons.marchingTriangles.MarchingTrianglesTerrainPlugin _plugin)
    {
        var existingChunk = _chunks.GetValueOrDefault(coords, null);
        _chunks.Remove(coords);
        // Handle the case where the selected chunk is the deleted one
        if (_chunks.Count == 0)
        {
            var tmpChunk = new GdPluginHexTerrainChunk(new Vector2I(int.MaxValue, int.MinValue),
                TerrainSettings.ChunkDimensions, v => _neighborChunkProviderProvider(coords,v));

            _plugin.PluginHelper.CurrentSelectedChunk = tmpChunk;
        }
        else
        {
            _plugin.PluginHelper.CurrentSelectedChunk = _chunks.First().Value;
        }

        RemoveChild(existingChunk);
        if (existingChunk != null) existingChunk.Owner = null;
        _plugin.Ui.UiToolAttributes.ShowToolAttributes((int)TerrainToolMode.ChunkManagement);
        _plugin.GizmoPlugin.TriggerRedraw(this);
    }

    /// <summary>
    /// Clear from tree but do not free a chunk so the operation can be undone 
    /// </summary>
    /// <param name="coords"></param>
    public void RemoveChunkFromTree(Vector2I coords, addons.marchingTriangles.MarchingTrianglesTerrainPlugin _plugin)
    {
        var existingChunk = _chunks.GetValueOrDefault(coords, null);
        _chunks.Remove(coords);
        // Handle the case where the selected chunk is the deleted one
        if (_chunks.Count == 0)
        {
            var tmpChunk = new GdPluginHexTerrainChunk(GetTempChunkCoords(),
                TerrainSettings.ChunkDimensions,
                v => _neighborChunkProviderProvider(coords,v));

            _plugin.PluginHelper.CurrentSelectedChunk = tmpChunk;
        }
        else
        {
            _plugin.PluginHelper.CurrentSelectedChunk = _chunks.First().Value;
        }
    }

    /// <summary>
    /// Whether an empty chunk can be added or not.
    /// </summary>
    /// <param name="currentHoveredChunk"></param>
    /// <returns></returns>
    public bool CanAddEmptyChunk(Vector2I currentHoveredChunk)
    {
        return Chunks.Count == 0 || Chunks.Keys.Any(key =>
            key == currentHoveredChunk + Vector2I.Up
            || key == currentHoveredChunk + Vector2I.Down
            || key == currentHoveredChunk + Vector2I.Left
            || key == currentHoveredChunk + Vector2I.Right);
    }
}