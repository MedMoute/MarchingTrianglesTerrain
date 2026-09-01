using System.Collections.Immutable;
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
        Assert.DoesNotThrow(() =>
        {
            MttDataHandler.SaveChunks(terrain);
        });
    }

    [GdUnit4.TestCase]
    public void TestSaveSimpleSingleTerrainInstance()
    {
        AssertThat(FileUtils.GetDirectorySizeRecursive(terrain.DataDirectory)).IsEqual(0);
        Assert.DoesNotThrow(() =>
        {  
            terrain.TerrainSettings.ChunkDimensions = 5 * Vector2I.One;
            terrain.AddNewChunk(new Vector2I(0, 0), plugin);
            MttDataHandler.SaveChunks(terrain);
            });
        AssertThat(FileUtils.GetDirectorySizeRecursive(terrain.DataDirectory)).IsGreater(0);
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
        terrain.Chunks.Keys.ToImmutableList().ForEach((chk)=>terrain.RemoveChunkFromTree(chk, plugin));
        // Clean up the chunk directories referring to chunks that no longer exist in the saved scene
        MttDataHandler.CleanupOrphanedChunkDirectories(terrain);
        // Clean up the terrain directories referring to terrain nodes no longer existing in the scene
        MttDataHandler.CleanupOrphanedTerrainDirectories(terrain);
        DirAccess.RemoveAbsolute(terrain.DataDirectory.TrimSuffix("/"));
    }
}