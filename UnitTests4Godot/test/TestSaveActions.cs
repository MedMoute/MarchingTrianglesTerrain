using System.Collections.Immutable;
using System.Diagnostics;
using GdUnit4;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using static GdUnit4.Assertions;
using NUnit.Framework;

namespace UnitTests4Godot.test;

[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class TestSaveActions
{
    private MarchingTrianglesTerrainPlugin plugin;
    private MarchingTrianglesTerrain.addons.marchingTriangles.MarchingTrianglesTerrain terrain;

    [GdUnit4.TestCase]
    public void TestSaveEmptyTerrainInstance()
    {
        Assert.DoesNotThrow(() => { MttDataHandler.SaveChunks(terrain); });
    }

    [GdUnit4.TestCase]
    public void TestDataStructureIntegrityOnSingleSaveChunk()
    {
        Vector2I chunkCoord = Vector2I.Zero;
        // Basic assertions on workflow : Empty dir => Non-empty dir after save
        AssertThat(FileUtils.GetDirectorySizeRecursive(terrain.DataDirectory)).IsEqual(0);
        Assert.DoesNotThrow(() =>
        {
            terrain.AddNewChunk(chunkCoord, plugin);
            MttDataHandler.SaveChunks(terrain);
        });
        AssertThat(FileUtils.GetDirectorySizeRecursive(terrain.DataDirectory)).IsGreater(0);

        // Check the content of the directory
        var dataFiles = AssertOutputDirectoryContentAndCollectDatafiles(chunkCoord);

        AssertThat(dataFiles.Count).IsEqual(1);

        // Load the content of the saved files
        var res = GD.Load(dataFiles[0]);
        if (res == null)
        {
            Assert.Fail();
        }
    }

    [GdUnit4.TestCase]
    public void TestOutputFileIntegrityOnSingleSaveChunk()
    {
        Vector2I chunkCoord = Vector2I.One;

        terrain.AddNewChunk(chunkCoord, plugin);
        MttDataHandler.SaveChunks(terrain);

        // Get the output file for the chunk
        var dataFile = AssertOutputDirectoryContentAndCollectDatafiles(chunkCoord)[0];
        var res = GD.Load(dataFile) as MttChunkData;
        Debug.Assert(res != null, nameof(res) + " != null");
        //Primitives
        //AssertThat(res.ChunkCoords).IsEqual(chunkCoord);
        // Arrays
        //Assert.NotNull(res.CollisionFaces);
        //AssertThat(res.CollisionFaces.Length).IsGreater(0);
        //Mesh
        //Assert.NotNull(res.Mesh);
        //AssertThat(res.Mesh.GetSurfaceCount()).IsGreater(0);
        
    }


    private List<string> AssertOutputDirectoryContentAndCollectDatafiles(Vector2I chunkCoord)
    {
        List<string> dataFiles = new();

        var dirPath = terrain.DataDirectory;
        var dir = DirAccess.Open(dirPath);
        // There should only be one subfolder as there is only one saved chunk
        AssertThat(dir.GetFiles().Length).IsEqual(0);
        AssertThat(dir.GetDirectories().Length).IsEqual(1);
        dir.ListDirBegin();
        var fileName = dir.GetNext();
        while (fileName.Length > 0)
        {
            var nextPath = dirPath.PathJoin(fileName);
            if (dir.CurrentIsDir())
            {
                if (fileName == "." || fileName == "..") // Ignore self and parent directories
                {
                    fileName = dir.GetNext();
                    continue;
                }

                AssertThat(fileName).StartsWith(MttDataHandler.ChunkPrefix);
                AssertThat(fileName).EndsWith(MttDataHandler.ChunkSuffixProvider.Invoke(chunkCoord));
                var subDir = DirAccess.Open(nextPath);
                AssertThat(subDir.GetFiles().Length).IsEqual(2);
                AssertThat(subDir.GetDirectories().Length).IsEqual(0);
                subDir.ListDirBegin();
                var subFileName = subDir.GetNext();
                while (subFileName.Length > 0)
                {
                    var filePath = nextPath.PathJoin(subFileName);
                    if (subDir.CurrentIsDir())
                    {
                        if (subFileName == "." || subFileName == "..") // Ignore self and parent directories
                        {
                            subFileName = subDir.GetNext();
                            continue;
                        }

                        Assert.Fail(); // We don't expect subfolders at this point 
                    }
                    else
                    {
                        if (subFileName == MttDataHandler.MetadataFilename)
                        {
                            //Collect the metadata filepath
                            dataFiles.Add(filePath);
                        }
                        else
                        {
                            Assert.Fail();
                        }

                        subFileName = subDir.GetNext();
                    }
                }

                subDir.ListDirEnd(); // Close subdirectory
            }
            else
            {
                // Should not happen (no file expected at this point) 
                Assert.Fail();
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd(); // Close directory
        return dataFiles;
    }

    [BeforeTest]
    public void Setup()
    {
        plugin = AddNode(new MarchingTrianglesTerrainPlugin());
        terrain = AddNode(new MarchingTrianglesTerrain.addons.marchingTriangles.MarchingTrianglesTerrain());
        terrain.DataDirectory = "res://out";
    }

    [AfterTest]
    public void TestCleanup()
    {
        //terrain.Chunks.Keys.ToImmutableList().ForEach((chk) => terrain.RemoveChunkFromTree(chk, plugin));
        //// Clean up the chunk directories referring to chunks that no longer exist in the saved scene
        //MttDataHandler.CleanupOrphanedChunkDirectories(terrain);
        //// Clean up the terrain directories referring to terrain nodes no longer existing in the scene
        //MttDataHandler.CleanupOrphanedTerrainDirectories(terrain);
        //DirAccess.RemoveAbsolute(terrain.DataDirectory.TrimSuffix("/"));
    }
}