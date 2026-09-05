using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using GdUnit4;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using static GdUnit4.Assertions;
using NUnit.Framework;
using FileAccess = Godot.FileAccess;

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
        GD.Print(dataFiles[0].Item2);
        GD.Print(dataFiles[0].Item1);
        // Load the content of the saved files
        if (ResourceLoader.Load(dataFiles[0].Item1, "", ResourceLoader.CacheMode.IgnoreDeep) is not MttChunkData)
        {
            Assert.Fail();
        }
        if (ResourceLoader.Load(dataFiles[0].Item2, "DelegatedChunkDataStruct", ResourceLoader.CacheMode.IgnoreDeep) is not DelegatedChunkDataStruct)
        {
            Assert.Fail();
        }
    }

    [GdUnit4.TestCase]
    public void TestOutputFileIntegrityOnSingleSaveChunk()
    {
        Vector2I chunkCoord = Vector2I.One;

        terrain.AddNewChunk(chunkCoord, plugin);
        var chunk = terrain.Chunks[chunkCoord];

        MttDataHandler.SaveChunks(terrain);

        // Get the output file for the chunk
        var dataFile = AssertOutputDirectoryContentAndCollectDatafiles(chunkCoord)[0];

        GD.Print(dataFile.Item1);
        var res = GD.Load<MttChunkData>(dataFile.Item1);
        GD.Print(dataFile.Item2);
        var structRes = GD.Load<DelegatedChunkDataStruct>(dataFile.Item2);
        
        // Arrays
        Assert.NotNull(res.CollisionFaces);
        AssertThat(res.CollisionFaces.Length).IsGreater(0);
        //Mesh
        Assert.NotNull(res.Mesh);
        AssertThat(res.Mesh.GetSurfaceCount()).IsGreater(0);

        // Compare the Loaded resource (NOT AS A CHUNK - as this test is not about chunk loading)
        // to the initial chunk values :

        Assert.That(res.ChunkCoords, Is.EqualTo(chunk.Underlying.Coordinates));

        Assert.That(structRes.FrameDimensions, Is.EqualTo(chunk.Underlying.Dimensions));
        Assert.That(res.MergeMode, Is.EqualTo(chunk.Underlying.MergeMode));
        // Test Data Struct
        Assert.That(
            structRes.Values,
            Is.EqualTo(chunk.Underlying.DataGrid.Data.Values.ToArray()).AsCollection);
    }


    private List<Tuple<string, string>> AssertOutputDirectoryContentAndCollectDatafiles(Vector2I chunkCoord)
    {
        List<Tuple<string, string>> dataFiles = new();

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
                            dataFiles.Add(new Tuple<string, string>(filePath, nextPath.PathJoin(subDir.GetNext())));
                        }
                        else if (subFileName == MttDataHandler.DataStructFilename)
                        {
                            //Collect the resource filepath
                            dataFiles.Add(new Tuple<string, string>(nextPath.PathJoin(subDir.GetNext()), filePath));
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

    [GdUnit4.TestCase]
    public void TestCanWriteStructureToFile()
    {
        var chunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null);
        var dataStructImpl = new ChunkDataStructImpl();
        MttDataHandler.FillDataStructFromChunk(dataStructImpl, chunk);
        var facade = new DelegatedChunkDataStruct(dataStructImpl);
        var dir = DirAccess.Open("res://resources");
        var error = ResourceSaver.Save(facade, "res://resources/file.tres", ResourceSaver.SaverFlags.BundleResources);
        if (error != Error.Ok)
        {
            GD.Print("Failed to remove test temp file : " + error);
            Assert.Fail();
        }

        //Read the file
        String str = FileAccess.GetFileAsString("res://resources/file.tres");
        Assert.That(str.Length, Is.GreaterThan(0));
        error = dir.Remove("res://resources/file.tres");
        if (error != Error.Ok)
        {
            GD.Print("Failed to remove test temp file : " + error);
            Assert.Fail();
        }
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
        // terrain.Chunks.Keys.ToImmutableList().ForEach((chk) => terrain.RemoveChunkFromTree(chk, plugin));
        // // Clean up the chunk directories referring to chunks that no longer exist in the saved scene
        // MttDataHandler.CleanupOrphanedChunkDirectories(terrain);
        // // Clean up the terrain directories referring to terrain nodes no longer existing in the scene
        // MttDataHandler.CleanupOrphanedTerrainDirectories(terrain);
        // DirAccess.RemoveAbsolute(terrain.DataDirectory.TrimSuffix("/"));
    }
}