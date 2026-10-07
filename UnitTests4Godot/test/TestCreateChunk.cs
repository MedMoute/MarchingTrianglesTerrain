using GdUnit4;
using Godot;
using NUnit.Framework;
using static GdUnit4.Assertions;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.test;

[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class TestCreateChunk
{
    [GdUnit4.TestCase]
    public void TestBasicChunkCreation()
    {
        MarchingTrianglesTerrain terrain = null!;
        MarchingTrianglesTerrainPlugin plugin = null!;
        
        Assert.DoesNotThrow(() =>
        {
            plugin = AddNode(new MarchingTrianglesTerrainPlugin());
            terrain = AddNode(new MarchingTrianglesTerrain());
        });
        AssertThat(terrain.GetChildCount()).IsEqual(0);

        terrain.AddNewChunk(new Vector2I(0, 0), plugin ?? throw new InvalidOperationException());
        AssertThat(terrain.GetChildCount()).IsEqual(1);
    }
}
