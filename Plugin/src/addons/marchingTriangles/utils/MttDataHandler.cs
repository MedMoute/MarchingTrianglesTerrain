using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Godot.Collections;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MathNet.Spatial.Euclidean;
using Microsoft.Extensions.Logging;


namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

/// <summary>
/// Utility class for handling the project-related IO operations
/// (mainly Saving/Loading chunks from a directory)
/// </summary>
public abstract class MttDataHandler
{
    internal const string ChunkPrefix = "chunk_";
    internal static readonly Func<Vector2I, string> ChunkSuffixProvider = (c) => $"{c.X}_{c.Y}";
    internal const string TerrainSuffix = "_TerrainData";
    // TODO :: consider switching to .res+compress (or add a flag/option ?)
    // TODO : Sanitize on loading to avoid potential remote code execution
    internal const string MetadataFilename = "metadata.tres";
    internal const string DataStructFilename = "datastruct.tres";

    private static ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
    private static ILogger logger = factory.CreateLogger("MttDataHandler");

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
        var sceneName = scenePath.GetBaseName().GetFile();
        return sceneDir.PathJoin(sceneName + TerrainSuffix).PathJoin(terrain.Name + "_" + GenerateTerrainUid());
    }

    /// <summary>
    /// Generates a unique terrain ID (called once on first save)
    /// </summary>
    /// <returns>The Uid's string</returns>
    private static string GenerateTerrainUid()
    {
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Saves the chunks of a given terrain at the directory defined
    /// in the provided terrain's settings. 
    /// </summary>
    /// <param name="terrain"></param>
    public static void SaveChunks(MarchingTrianglesTerrain terrain)
    {
        var dirPath = terrain.DataDirectory;
        if (dirPath.Length == 0)
        {
            // Invalid data directory
            GD.PrintErr("Save aborted due to empty storage directory");
            return;
        }

        if (!EnsureDirectoryExists(dirPath))
        {
            GD.PrintErr("Attempted to save the chunks of a terrain with a non existing dirpath. Aborting the save.");
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
                logger.LogInformation("Saving chunk resource {chunkCoords}.", chunk.Underlying.Coordinates);
                SaveChunkResource(terrain, chunk);
                chunk.Underlying.Dirty = false;
                savedCount++;
            }
            else
            {
                logger.LogInformation("Chunk resource {chunkCoords} skipped for saving.", chunk.Underlying.Coordinates);
            }
        }

        if (savedCount > 0)
        {
            ReportStorageSizeChanges(terrain, dirPath, initialFolderSize, savedCount);
            terrain.LastWorkingStorageMode = terrain.StorageType;
        }
        else
        {
            GD.Print("MTTDataHandler: Saved ", savedCount, " chunk(s) to ", dirPath);
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
        if (!terrain.IsInsideTree())
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

    /// <summary>
    /// Recursively deletes a directory.
    /// </summary>
    /// <param name="directoryPath"></param>
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
    private static System.Collections.Generic.Dictionary<string, List<MarchingTrianglesTerrain>>
        CollectTerrainDirsRecursive(
            Node node, System.Collections.Generic.Dictionary<string, List<MarchingTrianglesTerrain>> dirs)
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

        var chunkName = $"{ChunkPrefix}" + ChunkSuffixProvider.Invoke(chunk.Underlying.Coordinates);
        var chunkDir = dirPath.PathJoin(chunkName);
        EnsureDirectoryExists(chunkDir);
        // Export the chunk Data
        Tuple<MttChunkData, IChunkDataStruct> dataTuple = ExportChunkData(chunk);
        //  Clear transient data based on mode and config
        bool isBakedMode = terrain.StorageType == MarchingTrianglesTerrain.StorageMode.Baked;

        if (!isBakedMode)
        {
            dataTuple.Item1.Mesh = null;
        }

        if (!isBakedMode || !terrain.BakeCollision)
        {
            dataTuple.Item1.CollisionFaces = null;
        }

        var metadataPath = chunkDir.PathJoin(MetadataFilename);

        var errorMetadata = ResourceSaver.Save(dataTuple.Item1, metadataPath);
        if (errorMetadata != Error.Ok)
        {
            GD.PrintErr("MTTDataHandler: Failed to save metadata to ", metadataPath);
            return;
        }

        var dataPath = chunkDir.PathJoin(DataStructFilename);

        var errorStruct = ResourceSaver.Save(new DelegatedChunkDataStruct(dataTuple.Item2), dataPath);
        if (errorStruct != Error.Ok)
        {
            GD.PrintErr("MTTDataHandler: Failed to save data structure to ", dataPath);
            return;
        }

        logger.LogInformation("Saved data: [Coords]{0}", dataTuple.Item1.ChunkCoords);
    }

    /// <summary>
    /// Exports a chunk's data into a set of Resources in order to serialize it.
    /// </summary>
    /// <returns>the Serializable resources</returns>
    private static Tuple<MttChunkData, IChunkDataStruct> ExportChunkData(GdPluginHexTerrainChunk chunk)
    {
        var data = new MttChunkData();
        data.ChunkCoords = chunk.Underlying.Coordinates;
        data.MergeMode = chunk.Underlying.MergeMode;

        var dataStructImpl = new ChunkDataStructImpl();

        FillDataStructFromChunk(dataStructImpl, chunk.Underlying);
        if (dataStructImpl.ExistingNeighborsAsV2I == null)
        {
            throw new Exception("Serialization failed. Aborting.");
        }

        data.Mesh = chunk.Mesh;

        if (chunk.GetParent() is MarchingTrianglesTerrain { BakeCollision: true })
        {
            foreach (var child in chunk.GetChildren())
            {
                if (child is StaticBody3D)
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

        logger.LogInformation("Chunk data being exported : [Coords]{0}", chunk.Underlying.Coordinates);
        return new Tuple<MttChunkData, IChunkDataStruct>(data, dataStructImpl);
    }

    /// <summary>
    /// Imports a chunk's data from a set of Resources in order to deserialize the info
    /// available in the Resources.
    /// </summary>
    private static bool ImportChunkData(
        GdPluginHexTerrainChunk chunk,
        MttChunkData data, 
        IChunkDataStruct dataStruct)
    {
        if (data == null)
        {
            GD.PrintErr("MTTDataHandler : ImportChunkData called with null data");
            return false;
        }

        chunk.Underlying.Coordinates = data.ChunkCoords;
        chunk.Underlying.MergeMode = data.MergeMode;

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
            chunk.TempCollisionShape = data.GetCollisionShape();
        }

        return FillChunkFromData(((DelegatedChunkDataStruct)dataStruct).GetUnderlying(), chunk.Underlying);
    }

    /// <summary>
    /// Fills a chunk with the data available in the provided IChunkDataStruct
    /// </summary>
    /// <param name="dataStruct">provided data</param>
    /// <param name="chunk">chunk to fill</param>
   public static bool FillChunkFromData(
        IChunkDataStruct dataStruct, 
        HexagonalTerrainChunk chunk)
    {
        if (chunk.ColorMaps == null)
        {
            chunk.InitializeColorMaps();
        }

        // // -- Encoded Data grid (TriangleGrid)
        // //----------------------------------------
        // // ----> Grid Expected Size
        chunk.Dimensions = dataStruct.FrameDimensions;

        DoubleDeltaTileOrientationSystem frame =
            new DoubleDeltaTileOrientationSystem(
                new Vector2D(dataStruct.TriFrameSeed1[0], dataStruct.TriFrameSeed1[1]),
                new Vector2D(dataStruct.TriFrameSeed2[0], dataStruct.TriFrameSeed2[1])
            );
        chunk.DataGrid = TriangleGrid.BuildFrom(dataStruct.Values, dataStruct.FrameDimensions, frame);

        // // -- Encoded DualGrid (Hexagonal grid)
        // //----------------------------------------
        // // ----> RegularUniformFrame (DoubleDeltaTiling) seeds
        chunk.TerrainDualGrid = HexagonGrid.BuildFromSerialData(
            dataStruct.HexFrameSeed1,
            dataStruct.HexFrameSeed2,
            dataStruct.TriFrameSeed1,
            dataStruct.TriFrameSeed2,
            dataStruct.FrameDimensions,
            dataStruct.FullCellIndicesAsV2I,
            dataStruct.FullCellMappingsAsV3I,
            dataStruct.FullCellVisitsMappingKeyAsV3I,
            dataStruct.FullCellVisitsMappingValueAsV2I,
            dataStruct.PendingCellIndicesAsV2I,
            dataStruct.PendingCellsVisitsMappingKeyAsV3I,
            dataStruct.PendingCellsVisitsMappingValueAsV2I);

        // Encoded neighbors
        //----------------------
        chunk.ExistingNeighbors = new HashSet<Vector2I>();
        for (int i = 0; i < dataStruct.ExistingNeighborsAsV2I.Length / 2; i++)
        {
            var neighbor = new Vector2I(dataStruct.ExistingNeighborsAsV2I[2 * i],
                dataStruct.ExistingNeighborsAsV2I[2 * i + 1]);
            chunk.ExistingNeighbors.Add(neighbor);
        }

        // Encoded Marching Triangles properties
        //----------------------
        chunk.MergeMode = dataStruct.MergeMode;
        chunk.MergeThreshold = dataStruct.MergeThreshold;
        //     
        // // Encoded ColorMaps
        long zOffSet = dataStruct.FrameDimensions.X * dataStruct.FrameDimensions.Y;
        long yOffset = dataStruct.FrameDimensions.X;
        long xOffset = 1;
        for (int i = 0; i < dataStruct.FrameDimensions.X; i++)
        {
            for (int j = 0; j < dataStruct.FrameDimensions.Y; j++)
            {
                for (int k = 0; k < dataStruct.FrameDimensions.Z; k++)
                {
                    var cell = new Vector3I(i, j, k);
                    chunk.ColorMaps.SetGroundColor0(cell,
                        dataStruct.Ground0Colors[i * xOffset + j * yOffset + k * zOffSet]);
                    chunk.ColorMaps.SetGroundColor1(cell,
                        dataStruct.Ground1Colors[i * xOffset + j * yOffset + k * zOffSet]);
                    chunk.ColorMaps.SetWallColor0(cell,
                        dataStruct.Wall0Colors[i * xOffset + j * yOffset + k * zOffSet]);
                    chunk.ColorMaps.SetWallColor1(cell,
                        dataStruct.Wall1Colors[i * xOffset + j * yOffset + k * zOffSet]);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Copies the underlying data structures of the chunk objet
    /// into a Persistent Data Chunk Resource object
    /// </summary>
    public static void FillDataStructFromChunk(
        ChunkDataStructImpl dataStruct,
        HexagonalTerrainChunk chunk)
    {
        if (chunk.ColorMaps == null)
        {
            chunk.InitializeColorMaps();
        }

        // // -- Encoded Data grid (TriangleGrid)
        // //----------------------------------------
        // // ----> Grid Expected Size
        dataStruct.FrameDimensions = chunk.Dimensions;
        // // ----> RegularUniformFrame (DoubleDeltaTiling) seeds
        var seed1 = chunk.DataGrid.OrientationSystem.UnscaledOriginCellCentroidPositions[0];
        dataStruct.TriFrameSeed1 = [seed1.X, seed1.Y];
        var seed2 = chunk.DataGrid.OrientationSystem.UnscaledOriginCellCentroidPositions[1];
        dataStruct.TriFrameSeed2 = [seed2.X, seed2.Y];
        // // ----> Data
        dataStruct.Values =
            new float[dataStruct.FrameDimensions.X * dataStruct.FrameDimensions.Y * dataStruct.FrameDimensions.Z];
        long zOffSet = dataStruct.FrameDimensions.X * dataStruct.FrameDimensions.Y;
        long yOffset = dataStruct.FrameDimensions.X;
        long xOffset = 1;
        foreach (var kvp in chunk.DataGrid.Data)
        {
            dataStruct.Values[kvp.Key.X * xOffset + kvp.Key.Y * yOffset + kvp.Key.Z * zOffSet] = kvp.Value;
        }

        // // -- Encoded DualGrid (Hexagonal grid)
        // //----------------------------------------
        // // ----> RegularUniformFrame (DoubleDeltaTiling) seeds
        seed1 = chunk.TerrainDualGrid.Frame.UnscaledOriginCellCentroidPositions[0];
        dataStruct.HexFrameSeed1 = [seed1.X, seed1.Y];
        seed2 = ((HexTileOrientationSystem)chunk.TerrainDualGrid.Frame).InitialSeedVector;
        dataStruct.HexFrameSeed2 = [seed2.X, seed2.Y];
        // // ----> FullCells
        // // -------> Cell indexes
        dataStruct.FullCellIndicesAsV2I = new int[chunk.TerrainDualGrid.CompleteCells.Count * 2];
        // // -------> DataIndexesMapping (FullCellIndices.length)
        dataStruct.FullCellMappingsAsV3I = new int[6 * dataStruct.FullCellIndicesAsV2I.Length * 3];
        dataStruct.FullCellVisitsMappingKeyAsV3I = new int[6 * dataStruct.FullCellIndicesAsV2I.Length * 3];
        dataStruct.FullCellVisitsMappingValueAsV2I = new int[6 * dataStruct.FullCellIndicesAsV2I.Length * 3];

        int cursor = 0;
        foreach (var hexTerrainCell in chunk.TerrainDualGrid.CompleteCells)
        {
            dataStruct.FullCellIndicesAsV2I[2 * cursor] = hexTerrainCell.CellCoordsImplicit.X;
            dataStruct.FullCellIndicesAsV2I[2 * cursor + 1] = hexTerrainCell.CellCoordsImplicit.Y;

            for (int i = 0; i < 6; i++)
            {
                dataStruct.FullCellMappingsAsV3I[3 * (6 * cursor + i)] = hexTerrainCell.DualCellsMapping[i].X;
                dataStruct.FullCellMappingsAsV3I[3 * (6 * cursor + i) + 1] = hexTerrainCell.DualCellsMapping[i].Y;
                dataStruct.FullCellMappingsAsV3I[3 * (6 * cursor + i) + 2] = hexTerrainCell.DualCellsMapping[i].Z;
            }

            int subCursor = 0;
            foreach (var visits in hexTerrainCell.Visits)
            {
                dataStruct.FullCellVisitsMappingKeyAsV3I[3 * (6 * cursor + subCursor)] = visits.Key.X;
                dataStruct.FullCellVisitsMappingKeyAsV3I[3 * (6 * cursor + subCursor) + 1] = visits.Key.Y;
                dataStruct.FullCellVisitsMappingKeyAsV3I[3 * (6 * cursor + subCursor) + 2] = visits.Key.Z;

                dataStruct.FullCellVisitsMappingValueAsV2I[2 * (6 * cursor + subCursor)] = visits.Value.X;
                dataStruct.FullCellVisitsMappingValueAsV2I[2 * (6 * cursor + subCursor) + 1] = visits.Value.Y;

                subCursor++;
            }

            cursor++;
        }

        // // ----> PendingCells
        // // -------> Pending Cell indexes & Mapping
        dataStruct.PendingCellIndicesAsV2I = new int[2 * chunk.TerrainDualGrid.PendingCells.Count];
        dataStruct.PendingCellsVisitsMappingKeyAsV3I = new int[3 * chunk.TerrainDualGrid.PendingCells.Count * 6];
        dataStruct.PendingCellsVisitsMappingValueAsV2I = new int[2 * chunk.TerrainDualGrid.PendingCells.Count * 6];

        cursor = 0;
        foreach (var kvp in chunk.TerrainDualGrid.PendingCells)
        {
            var subCursor = 0;
            dataStruct.PendingCellIndicesAsV2I[2 * cursor] = kvp.Key.X;
            dataStruct.PendingCellIndicesAsV2I[2 * cursor + 1] = kvp.Key.Y;

            foreach (var visits in kvp.Value.Visits)
            {
                dataStruct.PendingCellsVisitsMappingKeyAsV3I[3 * (6 * cursor + subCursor)] = visits.Key.X;
                dataStruct.PendingCellsVisitsMappingKeyAsV3I[3 * (6 * cursor + subCursor) + 1] = visits.Key.Y;
                dataStruct.PendingCellsVisitsMappingKeyAsV3I[3 * (6 * cursor + subCursor) + 2] = visits.Key.Z;

                dataStruct.PendingCellsVisitsMappingValueAsV2I[2 * (6 * cursor + subCursor)] = visits.Value.X;
                dataStruct.PendingCellsVisitsMappingValueAsV2I[2 * (6 * cursor + subCursor) + 1] = visits.Value.Y;

                subCursor++;
            }

            // Fill the array with Integer.MAX_VALUE so we do not assume the value is 0;
            for (int i = subCursor; i < 6; i++)
            {
                {
                    dataStruct.PendingCellsVisitsMappingKeyAsV3I[3 * (6 * cursor + subCursor)] = Int32.MaxValue;
                    dataStruct.PendingCellsVisitsMappingKeyAsV3I[3 * (6 * cursor + subCursor) + 1] = Int32.MaxValue;
                    dataStruct.PendingCellsVisitsMappingKeyAsV3I[3 * (6 * cursor + subCursor) + 2] = Int32.MaxValue;

                    dataStruct.PendingCellsVisitsMappingValueAsV2I[2 * (6 * cursor + subCursor)] = Int32.MaxValue;
                    dataStruct.PendingCellsVisitsMappingValueAsV2I[2 * (6 * cursor + subCursor) + 1] = Int32.MaxValue;

                    subCursor++;
                }
            }

            cursor++;
        }


        // Encoded neighbors
        //----------------------
        dataStruct.ExistingNeighborsAsV2I = new int[2 * chunk.ExistingNeighbors.Count];
        cursor = 0;
        foreach (var neighbor in chunk.ExistingNeighbors)
        {
            dataStruct.ExistingNeighborsAsV2I[2 * cursor] = neighbor.X;
            dataStruct.ExistingNeighborsAsV2I[2 * cursor + 1] = neighbor.Y;
            cursor++;
        }

        // Encoded Marching Triangles properties
        //----------------------
        dataStruct.MergeMode = chunk.MergeMode;
        dataStruct.MergeThreshold = chunk.MergeThreshold;
        //     
        // // Encoded ColorMaps
        // //----------------------
        dataStruct.Ground0Colors = new Color[dataStruct.Values.Length];
        dataStruct.Ground1Colors = new Color[dataStruct.Values.Length];
        dataStruct.Wall0Colors = new Color[dataStruct.Values.Length];
        dataStruct.Wall1Colors = new Color[dataStruct.Values.Length];

        for (int i = 0; i < dataStruct.FrameDimensions.X; i++)
        {
            for (int j = 0; j < dataStruct.FrameDimensions.Y; j++)
            {
                for (int k = 0; k < dataStruct.FrameDimensions.Z; k++)
                {
                    var cell = new Vector3I(i, j, k);
                    dataStruct.Ground0Colors[i * xOffset + j * yOffset + k * zOffSet] =
                        chunk.ColorMaps.GetGroundColor0(cell);
                    dataStruct.Ground1Colors[i * xOffset + j * yOffset + k * zOffSet] =
                        chunk.ColorMaps.GetGroundColor1(cell);
                    dataStruct.Wall0Colors[i * xOffset + j * yOffset + k * zOffSet] =
                        chunk.ColorMaps.GetWallColor0(cell);
                    dataStruct.Wall1Colors[i * xOffset + j * yOffset + k * zOffSet] =
                        chunk.ColorMaps.GetWallColor1(cell);
                }
            }
        }
    }

    /// <summary>
    /// Checks whether the chunk metadata file exists within a directory for a given set of chunk coordinates.
    /// </summary>
    private static bool MetadataExists(string dirPath, Vector2I underlyingCoordinates)
    {
        if (dirPath.Length == 0)
        {
            return false;
        }

        var chunkDirectory = dirPath.PathJoin($"{ChunkPrefix}{underlyingCoordinates.X}_{underlyingCoordinates.Y}");
        return Godot.FileAccess.FileExists(chunkDirectory.PathJoin(MetadataFilename));
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
        var dirs = CollectTerrainDirsRecursive(sceneRoot,
            new System.Collections.Generic.Dictionary<string, List<MarchingTrianglesTerrain>>());

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


    /// <summary>
    /// Load the stored terrain data from its default path.
    /// </summary>
    /// By default, the loading does not load the non-existing chunks of the terrain if there are some unreferenced
    /// chunks in the loaded data. This can be forced by setting the "forceLoadFrom Dir" parameter to "true".
    /// <param name="terrain"></param>
    /// <param name="forceLoadFromDir"></param>
    /// <returns></returns>
    public static bool LoadTerrainData(MarchingTrianglesTerrain terrain, bool forceLoadFromDir = false)
    {
        var dirPath = terrain.DataDirectory;
        GD.Print("MttDataHandler : LoadTerrainData");
        if (dirPath.Length == 0)
        {
            return false;
        }

        var dir = DirAccess.Open(dirPath);
        if (dir == null)
        {
            return false;
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

        if (chunkDirs.Count == 0)
        {
            return false;
        }

        GD.Print("MttDataHandler  :Loading " + chunkDirs.Count + " chunk(s) from " + dirPath);

        bool onceSucceed = false;
        foreach (var coords in chunkDirs)
        {
            var res = LoadChunkFromDirectory(terrain, coords, forceLoadFromDir);
            if (!onceSucceed)
            {
                onceSucceed = res;
            }

            if (!res) continue;
            
            //Set the data fetching functions for the new chunks' cells
            foreach (var hexTerrainCell in terrain.Chunks[coords].Underlying.TerrainDualGrid.PendingCells.Values)
            {
                hexTerrainCell.SetDataFetchingFunction(terrain.TerrainSettings.ChunkDimensions,
                    v => terrain.NeighborChunkProviderProvider(coords, v).DataGrid,
                    v => terrain.Chunks[coords].Underlying.ExistingNeighbors.Contains(v));
            }
            foreach (var hexTerrainCell in terrain.Chunks[coords].Underlying.TerrainDualGrid.CompleteCells)
            {
                hexTerrainCell.SetDataFetchingFunction(terrain.TerrainSettings.ChunkDimensions,
                    v => terrain.NeighborChunkProviderProvider(coords, v).DataGrid,
                    v => terrain.Chunks[coords].Underlying.ExistingNeighbors.Contains(v));
            }
        }
        
        return onceSucceed;
    }

    private static bool LoadChunkFromDirectory(MarchingTrianglesTerrain terrain, Vector2I coords, bool forceLoadFromDir)
    {
        var dirPath = terrain.DataDirectory;
        var chunkName = String.Format("{0}{1}_{2}", ChunkPrefix, coords.X, coords.Y);
        var chunkDir = dirPath.PathJoin(chunkName);

        // Mesh, collision, and grass are regenerated separately by the chunk
        var exists = terrain.Chunks.TryGetValue(coords, out var chunk);
        if (!exists && !forceLoadFromDir)
        {
            logger.LogInformation("Chunk " + coords + " not found in the terrain. Load from disk is aborted");
            return false;
        }

        if (!exists)
        {
            //Create a chunk in the terrain
            chunk = terrain.AddChunkInternal(coords);
            terrain.AddChild(chunk);
            EngineUtils.SetOwnerAsSceneRoot(chunk);
        }

        bool success = false;
        //Load metadata source data
        var metadataPath = chunkDir.PathJoin(MetadataFilename);
        var dataStruct = chunkDir.PathJoin(DataStructFilename);

        if (ResourceLoader.Exists(metadataPath))
        {
            var res = ResourceLoader.Load(metadataPath, "", ResourceLoader.CacheMode.Ignore);
            var resStruct = ResourceLoader.Load(dataStruct, "", ResourceLoader.CacheMode.Ignore);

            if (res is MttChunkData metadata && resStruct is DelegatedChunkDataStruct data)
            {
                success = ImportChunkData(chunk, metadata, data);
            }
            else
            {
                logger.LogInformation("Loaded File " + metadataPath + " is not of the expected type.");
            }
        }
        else
        {
            logger.LogInformation("File " + metadataPath + " does not exist.");
        }

        if (success)
        {
            GD.Print("MttDataHandler : Loaded chunk " + coords);
        }

        return success;
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
            chunk.Underlying.Dirty = true;
        }

        SaveChunks(terrain);
        GD.Print("MttDataHandler: Migration complete . External data saved to " + terrain.DataDirectory);
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