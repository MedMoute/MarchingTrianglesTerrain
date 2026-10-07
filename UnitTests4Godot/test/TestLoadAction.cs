using GdUnit4;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.data;
using NUnit.Framework;
using static GdUnit4.Assertions;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.test;

[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class TestLoadAction
{
    private MarchingTrianglesTerrainPlugin _plugin = null!;
    private MarchingTrianglesTerrain _terrain = null!;

    [BeforeTest]
    public void Setup()
    {
        _plugin = AddNode(new MarchingTrianglesTerrainPlugin());
        _terrain = AddNode(new MarchingTrianglesTerrain());
    }

    [GdUnit4.TestCase]
    public void TestLoadOnSingleChunk()
    {       
        AssertThat(_terrain.Chunks.Count).IsEqual(0);
        Vector2I chunkCoord = Vector2I.One;
        _terrain.TerrainSettings.ChunkDimensions = new Vector2I(3, 3);
        _terrain.AddNewChunk(chunkCoord, _plugin ?? throw new InvalidOperationException());
        AssertThat(_terrain.Chunks.Count).IsEqual(1);
        
        Assert.That(_terrain.Chunks[chunkCoord].Underlying.Dimensions,
            Is.EqualTo(new Vector3I(3,3,2)));
        
        _terrain.DataDirectory = "res://resources/Chunks"; 
        AssertThat(FileUtils.GetDirectorySizeRecursive(_terrain.DataDirectory)).IsGreater(0);
        // Ensure the load does not fail catastrophically
        Assert.DoesNotThrow(() => { MttDataHandler.LoadTerrainData(_terrain); });
        // Ensure the load succeeds
        Assert.That(MttDataHandler.LoadTerrainData(_terrain),Is.True);       
        // Ensure the dimensions of the chunk were changed
        Assert.That(_terrain.Chunks[chunkCoord].Underlying.Dimensions,
            Is.EqualTo(new Vector3I(10, 10, 2)));
        AssertThat(_terrain.Chunks.Count).IsEqual(1);

    }
}