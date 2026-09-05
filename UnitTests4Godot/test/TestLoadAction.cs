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
public class TestLoadAction
{
    private MarchingTrianglesTerrainPlugin plugin;
    private MarchingTrianglesTerrain.addons.marchingTriangles.MarchingTrianglesTerrain terrain;

    [BeforeTest]
    public void Setup()
    {
        plugin = AddNode(new MarchingTrianglesTerrainPlugin());
        terrain = AddNode(new MarchingTrianglesTerrain.addons.marchingTriangles.MarchingTrianglesTerrain());
    }

    [GdUnit4.TestCase]
    public void TestLoadOnSingleChunk()
    {       
        AssertThat(terrain.Chunks.Count).IsEqual(0);
        Vector2I chunkCoord = Vector2I.One;
        terrain.TerrainSettings.ChunkDimensions = new Vector2I(3, 3);
        terrain.AddNewChunk(chunkCoord, plugin);
        AssertThat(terrain.Chunks.Count).IsEqual(1);
        
        Assert.That(terrain.Chunks[chunkCoord].Underlying.Dimensions,
            Is.EqualTo(new Vector3I(3,3,2)));
        
        terrain.DataDirectory = "res://resources/Chunks"; 
        AssertThat(FileUtils.GetDirectorySizeRecursive(terrain.DataDirectory)).IsGreater(0);
        // Ensure the load does not fail catastrophically
        Assert.DoesNotThrow(() => { MttDataHandler.LoadTerrainData(terrain); });
        // Ensure the load succeeds
        Assert.That(MttDataHandler.LoadTerrainData(terrain),Is.True);       
        // Ensure the dimensions of the chunk were changed
        Assert.That(terrain.Chunks[chunkCoord].Underlying.Dimensions,
            Is.EqualTo(new Vector3I(10, 10, 2)));
        AssertThat(terrain.Chunks.Count).IsEqual(1);

    }
}