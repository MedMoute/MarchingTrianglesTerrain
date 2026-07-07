using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.NativeInterop;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

public abstract class MttDataHandler
{
    public const string ChunkPrefix = "chunk_";
    public const string TerrainSuffix = "_TerrainData";
    public const string MetadataFilename = "metadata.res";

    /// <summary>
    /// Generates a default storing directory and returns its path
    /// </summary>
    /// <param name="instance"></param>
    /// <returns></returns>
    public static string GenerateDataDirectory(MarchingTrianglesTerrain terrain)
    {
        if (!terrain.IsInsideTree())
        {
            return "";
        }

        var rootNode = EngineUtils.GetRootNode(terrain);
        if (rootNode == null || rootNode.SceneFilePath.Length == 0)
        {
            return "";
        }

        var scenePath = rootNode.SceneFilePath;
        var sceneDir = scenePath.GetBaseDir();
        var sceneName = scenePath.GetBaseName();
        return sceneDir.PathJoin(sceneName + "_TerrainData").PathJoin(terrain.Name + "_" + GenerateTerrainUid());
    }

    /// <summary>
    /// Generates a unique terrain ID (called once on first save)
    /// </summary>
    /// <returns>The Uid's string</returns>
    private static string GenerateTerrainUid()
    {
        return Guid.NewGuid().ToString();
    }

    public static void SaveChunks(MarchingTrianglesTerrain terrain)
    {
        var dirPath = terrain.DataDirectory;
        if (dirPath.Length == 0)
        {
            // Invalid data directory
            GD.PushError("Save aborted due to empty storage directory");
            return;
        }

        if (!EnsureDirectoryExists(dirPath))
        {
            return;
        }

        var initialFolderSize = FileUtils.GetDirectorySizeRecursive(dirPath);

        int savedCount = 0;
        foreach (var chunk in terrain.Chunks.Values)
        {
            //Skip chunks being removed by undo/redo operations
            if (chunk.SkipSaveOnExit)
            {
                continue;
            }

            var needsSave = chunk.Underlying.Dirty;
            if (!needsSave && !MetadataExists(dirPath, chunk.Underlying.Coordinates))
            {
                needsSave = true;
            }

            if (needsSave)
            {
                SaveChunkResource(terrain, chunk);
                chunk.Underlying.Dirty = false;
                savedCount++;
            }
        }

        if (savedCount > 0)
        {
            ReportStorageSizeChanges(terrain, dirPath, initialFolderSize, savedCount);
            terrain.LastWorkingStorageMode = terrain.StorageType;
        }

        // Cleanup operations :

        // Clean up the chunk directories referring to chunks that no longer exist in the saved scene
        CleanupOrphanedChunkDirectories(terrain);
        // Clean up the terrain directories referring to terrain nodes no longer existing in the scene
        CleanupOrphanedTerrainDirectories(terrain);
        terrain.StorageInitialized = true;
    }

    /// Cleans up orphaned chunk directories that no longer exist in the scene.
    private static void CleanupOrphanedChunkDirectories(MarchingTrianglesTerrain terrain)
    {
        var dirPath = terrain.DataDirectory;
        if (dirPath.Length == 0)
        {
            // Invalid data directory
            GD.PushError("Operation aborted due to empty storage directory");
            return;
        }

        var dir = DirAccess.Open(dirPath);
        if (dir == null)
        {
            GD.PushError("MttDataHandler : Cannot open directory : " + dirPath);
            return;
        }

        var orphanedDirs = new List<string>();

        dir.ListDirBegin();
        var folderName = dir.GetNext();
        while (folderName != "")
        {
            if (dir.CurrentIsDir() && folderName.StartsWith(ChunkPrefix))
            {
                // Parse chunk coordinates from folder name: chunk_X_Y
                var parts = folderName.TrimPrefix(ChunkPrefix).Split("_");
                if (parts.Length == 2)
                {
                    var coords = new Vector2I(parts[0].ToInt(), parts[1].ToInt());
                    // If the chunk doesn't exist in the terrain provided for cleanup , mark for deletion
                    if (!terrain.Chunks.ContainsKey(coords))
                    {
                        orphanedDirs.Add(dirPath.PathJoin(folderName));
                    }
                }
                else
                {
                    GD.PrintErr("Unexpected format for chunk data folder, expected : " + ChunkPrefix + "X_Y" +
                                " , instead got : " + folderName);
                }
            }
            else
            {
                GD.PrintErr("Unexpected format for chunk data folder, expected : " + ChunkPrefix + "X_Y" +
                            " , instead got : " + folderName);
            }

            folderName = dir.GetNext();
        }

        dir.ListDirEnd();

        // Delete the collected folders
        foreach (var orphanedDir in orphanedDirs)
        {
            DeleteChunkDirectory(orphanedDir);
            GD.Print("MttDataHandler : Cleaned up orphaned chunk at " + orphanedDir);
        }
    }

    /// <summary>
    /// Deletes the folder containing a chunk and all of its content.
    /// This method does NOT recursively remove data
    /// </summary>
    private static void DeleteChunkDirectory(string directoryPath)
    {
        var dir = DirAccess.Open(directoryPath);
        if (dir == null)
        {
            GD.PushError("MttDataHandler : Cannot open directory : " + directoryPath);
            return;
        }

        // Delete all files
        dir.ListDirBegin();
        var fileName = dir.GetNext();
        Error error;
        while (fileName != "")
        {
            if (!dir.CurrentIsDir())
            {
                error = dir.Remove(fileName);
                if (error != Error.Ok)
                {
                    GD.PushError("MttDataHandler : Failed to delete file " + fileName + " in " + directoryPath);
                }
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd();

        // Remove the directory itself
        error = DirAccess.RemoveAbsolute(directoryPath.TrimSuffix("/"));
        if (error != Error.Ok)
        {
            GD.PushError("MttDataHandler : Failed to delete directory " + directoryPath);
        }
    }

    /// Clean up terrain data directories for terrains that no longer exist in the saved scene.
    /// Called during save to prevent disk bloat from deleted terrains.
    private static void CleanupOrphanedTerrainDirectories(MarchingTrianglesTerrain terrain)
    {
        var sceneTree = terrain.GetTree();
        if (sceneTree == null)
        {
            return;
        }

        var rootOfScene = EngineUtils.GetRootNode(terrain);
        if (rootOfScene == null || rootOfScene.SceneFilePath.Length == 0)
        {
            return;
        }

        //  Get the _TerrainData folder for this root scene :
        var scenePath = rootOfScene.SceneFilePath;
        var sceneDir = scenePath.GetBaseDir();
        var sceneName = scenePath.GetFile().GetBaseName();
        var terrainDataDirectory = sceneDir.PathJoin(sceneName + TerrainSuffix);

        if (!DirAccess.DirExistsAbsolute(terrainDataDirectory))
        {
            return;
        }

        // Collect all terrain data directories currently active in the scene
        var activeTerrainDirs = CollectTerrainDirsRecursive(rootOfScene, new());

        var orphanedTerrains = new List<string>();

        // Scan the terrain data directory for orphaned folders
        var dir = DirAccess.Open(terrainDataDirectory);
        dir.ListDirBegin();
        var folderName = dir.GetNext();
        while (folderName != "")
        {
            if (dir.CurrentIsDir()) // Ignore files here
            {
                var terrainName = terrainDataDirectory.PathJoin(folderName).SimplifyPath();
                if (!activeTerrainDirs.ContainsKey(terrainName))
                {
                    orphanedTerrains.Add(terrainName);
                }
            }

            folderName = dir.GetNext();
        }

        dir.ListDirEnd();

        // Actually delete the orphaned directories
        foreach (var orphanedTerrainDirectory in orphanedTerrains)
        {
            DeleteDirectoryRecursive(orphanedTerrainDirectory);
            GD.Print("MttDataHandler has cleaned up orphaned terrain directory at " + orphanedTerrainDirectory);
        }
    }

    private static void DeleteDirectoryRecursive(string directoryPath)
    {
        var dir = DirAccess.Open(directoryPath);
        if (dir == null)
        {
            GD.PushError("MttDataHandler : Cannot open directory : " + directoryPath);
            return;
        }

        // Delete all files
        dir.ListDirBegin();
        var fileName = dir.GetNext();
        Error error;
        while (fileName != "")
        {
            if (dir.CurrentIsDir())
            {
                DeleteDirectoryRecursive(directoryPath.PathJoin(fileName));
            }
            else
            {
                error = dir.Remove(fileName);
                if (error != Error.Ok)
                {
                    GD.PushError("MttDataHandler : Failed to delete file " + fileName + " in " + directoryPath);
                }
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd();

        // Remove the directory itself
        error = DirAccess.RemoveAbsolute(directoryPath.TrimSuffix("/"));
        if (error != Error.Ok)
        {
            GD.PushError("MttDataHandler : Failed to delete directory " + directoryPath);
        }
    }

    /// <summary>
    /// Recursively collect terrain data directories that happen to be children of the provided node .
    /// </summary>
    private static Dictionary<string, List<MarchingTrianglesTerrain>> CollectTerrainDirsRecursive(
        Node node, Dictionary<string, List<MarchingTrianglesTerrain>> dirs)
    {
        if (node is MarchingTrianglesTerrain terrain && terrain.DataDirectory.Length != 0)
        {
            var simplePath = terrain.DataDirectory.SimplifyPath();
            if (!dirs.ContainsKey(simplePath))
            {
                dirs[simplePath] = [terrain];
            }
            else
            {
                dirs[simplePath].Add(terrain);
            }
        }

        foreach (var child in node.GetChildren())
        {
            CollectTerrainDirsRecursive(child, dirs);
        }

        return dirs;
    }

    ///Reports the storage size change after a save operation.
    private static void ReportStorageSizeChanges(MarchingTrianglesTerrain terrain, string dirPath,
        int initialFolderSize, int savedCount)
    {
        var finalSize = FileUtils.GetDirectorySizeRecursive(dirPath);
        var sizeDifference = finalSize - initialFolderSize;
        var percentageChange = 0f;

        if (initialFolderSize > 0)
        {
            percentageChange = 100f * ((float)sizeDifference / initialFolderSize);
        }
        else if (sizeDifference > 0)
        {
            percentageChange = 100f;
        }

        var signString = sizeDifference >= 0 ? "+" : "-";

        var storageModes = Enum.GetValuesAsUnderlyingType(typeof(MarchingTrianglesTerrain.StorageMode));
        var previousStorageString = storageModes.GetValue((int)terrain.LastWorkingStorageMode)?.ToString();
        var currentStorageString = storageModes.GetValue((int)terrain.StorageType)?.ToString();

        GD.Print("MTTDataHandler: Saved ", savedCount, " chunk(s) to ", dirPath);
        GD.Print(String.Format("MSTDataHandler: Storage Size: {0} ({1}) -> {2} ({3}) ({4}{5}%)"
            , initialFolderSize,
            previousStorageString,
            finalSize,
            currentStorageString,
            signString,
            percentageChange
        ));
    }

    /// <summary>
    /// Saves the  chunk data to an external file
    /// </summary>
    /// <param name="terrain"></param>
    /// <param name="chunk"></param>
    private static void SaveChunkResource(MarchingTrianglesTerrain terrain, GdPluginHexTerrainChunk chunk)
    {
        var dirPath = terrain.DataDirectory;
        if (dirPath.Length == 0)
        {
            GD.PrintErr("MSTDataHandler: Cannot save chunk - no valid data directory");
            return;
        }

        var chunkName = $"{ChunkPrefix}{chunk.Underlying.Coordinates.X}_{chunk.Underlying.Coordinates.Y}";
        var chunkDir = dirPath.PathJoin(chunkName);
        EnsureDirectoryExists(chunkDir);
        // Export the chunk Data
        MttChunkData data = ExportChunkData(chunk);
        //  Clear transient data based on mode and config
        bool isBakedMode = terrain.StorageType == MarchingTrianglesTerrain.StorageMode.Baked;

        if (!isBakedMode)
        {
            data.Mesh = null;
        }

        if (!isBakedMode || !terrain.BakeCollision)
        {
            data.CollisionFaces = null;
        }

        var metadataPath = chunkDir.PathJoin(MetadataFilename);
        var error = ResourceSaver.Save(data, metadataPath, ResourceSaver.SaverFlags.Compress);
        if (error != Error.Ok)
        {
            GD.PrintErr("MTTDataHandler: Failed to save metadata to ", metadataPath);
        }

        GD.Print("MSTDataHandler: Saved chunk ", chunk.Underlying.Coordinates);
    }

    private static MttChunkData ExportChunkData(GdPluginHexTerrainChunk chunk)
    {
        var data = new MttChunkData();
        data.ChunkCoords = chunk.Underlying.Coordinates;
        data.MergeMode = chunk.Underlying.MergeMode;
        // Source data :
        data.DataGrid = chunk.Underlying.DataGrid;
        data.DualGrid = chunk.Underlying._terrainDualGrid;
        //
        var cellCount = data.DataGrid.Size;
        //TODO
        FillDataFromChunk(data, chunk);
        data.Mesh = chunk.Mesh;

        if (chunk.GetParent() is MarchingTrianglesTerrain { BakeCollision: true })
        {
            foreach (var child in chunk.GetChildren())
            {
                if (child is StaticBody3D body)
                {
                    foreach (var bodyChild in child.GetChildren())
                    {
                        if (bodyChild is CollisionShape3D { Shape: ConcavePolygonShape3D concaveShape })
                        {
                            data.SetCollisionFromShape(concaveShape);
                            break;
                        }
                    }
                }
            }
        }

        return data;
    }

    private static void ImportChunkData(GdPluginHexTerrainChunk chunk, MttChunkData data)
    {
        if (data == null)
        {
            GD.PrintErr("MTTDataHandler : ImportChunkData calles with null data");
            return;
        }

        chunk.Underlying.Coordinates = data.ChunkCoords;
        chunk.Underlying.MergeMode = data.MergeMode;
        chunk.Underlying.DataGrid = data.DataGrid;
        chunk.Underlying._terrainDualGrid = data.DualGrid;

        //Restore baked assets if they exist
        if (data.Mesh != null)
        {
            chunk.Mesh = data.Mesh;
        }
        else if (chunk.GetParent() is MarchingTrianglesTerrain
                 {
                     StorageType: MarchingTrianglesTerrain.StorageMode.Baked
                 })
        {
            GD.PushWarning("Baking enabled, but terrain resource does not contain mesh data.");
        }

        if (chunk.GetParent() is MarchingTrianglesTerrain { BakeCollision: true } && data.CollisionFaces.IsEmpty())
        {
            GD.PushWarning("Collision baking enabled, but terrain resource does not contain collision data.");
        }

        if (!data.CollisionFaces.IsEmpty())
        {
            chunk._tempCollisionShape = data.GetCollisionShape();
        }
        //TODO
        FillChunkFromData(data, chunk);
    }

    private static void FillChunkFromData(MttChunkData data, GdPluginHexTerrainChunk chunk)
    {
        throw new NotImplementedException();
    }

    private static void FillDataFromChunk(MttChunkData data, GdPluginHexTerrainChunk chunk)
    {
        throw new NotImplementedException();
    }

    private static bool MetadataExists(string dirPath, Vector2I underlyingCoordinates)
    {
        if (dirPath.Length == 0)
        {
            return false;
        }
        var chunkDirectory = dirPath.PathJoin($"{ChunkPrefix}{underlyingCoordinates.X}_{underlyingCoordinates.Y}");
        return FileAccess.FileExists(chunkDirectory.PathJoin(MetadataFilename));
    }

    /// <summary>
    /// Checks if a terrains' data directory is unique
    /// </summary>
    public static bool IsDataDirectoryUnique(MarchingTrianglesTerrain terrain)
    {
        if (Engine.IsEditorHint() && terrain.IsInsideTree())
        {
            return true; // Sure but why ??
        }

        var sceneRoot = EngineUtils.GetRootNode(terrain);
        var dirs = CollectTerrainDirsRecursive(sceneRoot, new Dictionary<string, List<MarchingTrianglesTerrain>>());

        var simplifiedPath = terrain.DataDirectory.SimplifyPath();
        if (!dirs.ContainsKey(simplifiedPath))
        {
            return true;
        }

        return dirs[simplifiedPath].Count switch
        {
            0 => true,
            1 => dirs[simplifiedPath][0] == terrain,
            _ => false
        };
    }


    public static void LoadTerrainData(MarchingTrianglesTerrain terrain)
    {
        var dirPath = terrain.DataDirectory;
        GD.Print("MttDataHandler : LoadTerrainData");
        if (dirPath.Length == 0)
        {
            return;
        }
        
        var dir = DirAccess.Open(dirPath);
        if (dir == null)
        {
            return;
        }
        // Scan the chunk directories with the expected format : ChunkPrefix + "X_Y"
        List<Vector2I> chunkDirs = new();
        dir.ListDirBegin();
        var folderName = dir.GetNext();
        while (folderName != "")
        {
            if (dir.CurrentIsDir() && folderName.StartsWith(ChunkPrefix))
            {
                // Parse chunk coordinates from folder name: chunk_X_Y
                var parts = folderName.TrimPrefix(ChunkPrefix).Split("_");
                if (parts.Length == 2)
                {
                    var coords = new Vector2I(parts[0].ToInt(), parts[1].ToInt()); 
                    chunkDirs.Add(coords);
                }
            }
            folderName = dir.GetNext();
        }
        dir.ListDirEnd();
        
        if (chunkDirs.Count == 0){
        
            return;
        }
        GD.Print("MttDataHandler  :Loading "+chunkDirs.Count+ " chunk(s) from "+dirPath);
        
        foreach (var coords in chunkDirs)
        {
            LoadChunkFromDirectory(terrain, coords);
        }
    }

    private static void LoadChunkFromDirectory(MarchingTrianglesTerrain terrain, Vector2I coords)
    {
        var dirPath = terrain.DataDirectory;
        var chunkName = String.Format("{0}{1}_{2}",ChunkPrefix,coords.X,coords.Y);
        var chunkDir = dirPath.PathJoin(chunkName);

        // Mesh, collision, and grass are regenerated separately by the chunk
        var exists = terrain.Chunks.TryGetValue(coords,out var chunk);
        if (!exists)
        {
            return;
        }
        //Load metadata source data
        var metadataPath = chunkDir.PathJoin(MetadataFilename);
        if (ResourceLoader.Exists(metadataPath))
        {
            if (GD.Load(metadataPath) is MttChunkData data)
            {
                ImportChunkData(chunk, data);
            }
        }
        GD.Print("MttDataHandler : Loaded chunk " + coords);
    }

    /// <summary>
    /// Checks if this terrain needs migration from embedded to external storage.
    /// </summary>
    public static bool NeedsMigration(MarchingTrianglesTerrain terrain)
    {
        // If already initialized with external storage, no migration needed
        if (terrain.StorageInitialized)
        {
            return false;
        }
        // Check if any chunks have embedded data but no external files exist
        var dirPath = terrain.DataDirectory;
        if (dirPath.Length == 0)
        {
            return false;
        }
        foreach (var chunk in terrain.Chunks.Values)
        {
            if (chunk.Underlying.DataGrid != null && chunk.Underlying.DataGrid.Size > 0)
            {
                if (!MetadataExists(dirPath, chunk.Underlying.Coordinates))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static void MigrateToExternalStorage(MarchingTrianglesTerrain terrain)
    {
        GD.Print("MttDataHandler: Migrating to external storage...");
        // Mark all chunks dirty to force save
        foreach (var chunk in terrain.Chunks.Values)
        {
            chunk.Underlying.Dirty =true;
        }

        SaveChunks(terrain);
        GD.Print("MttDataHandler: Migration complete . External data saved to "+terrain.DataDirectory);

    }

    /// <summary>
    /// Checks if a directory exists by looking at the provided path, and attempt to create one in none exists.
    /// </summary>
    /// <param name="path">the provided absolute path where we want a directory to exist</param>
    /// <returns>True is the directory exists at the end of the method, false otherwise</returns>
    private static bool EnsureDirectoryExists(string path)
    {
        if (DirAccess.DirExistsAbsolute(path))
        {
            return true;
        }

        var error = DirAccess.MakeDirRecursiveAbsolute(path);
        if (error != Error.Ok)
        {
            GD.PrintErr("MTTDataHandler : The directory creation failed for path :" + path + " .Error : " + error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Copies recursively the content of a directory in another one
    /// </summary>
    /// <param name="fromPath">absolute source path</param>
    /// <param name="toPath">absolute destination path</param>
    public static void CopyRecursive(string fromPath, string toPath)
    {
        var dir = DirAccess.Open(fromPath);
        if (dir == null)
        {
            GD.PushError("MttDataHandler : Cannot open source directory to perform copy . Source path : " + fromPath);
            return;
        }

        DirAccess.MakeDirRecursiveAbsolute(toPath);

        dir.ListDirBegin();
        var fileName = dir.GetNext();
        while (fileName != "")
        {
            if (fileName is "." or "..") //Self or parent in list => we skip those
            {
                fileName = dir.GetNext();
                continue;
            }

            var src = fromPath.PathJoin(fileName);
            var dest = toPath.PathJoin(fileName);

            if (dir.CurrentIsDir()) //Recursive folder check
            {
                CopyRecursive(src, dest);
            }
            else
            {
                var error = DirAccess.CopyAbsolute(src, dest);
                if (error != Error.Ok)
                {
                    GD.PushError("Failed to copy file : " + src + " => " + dest);
                }
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd();
    }
}