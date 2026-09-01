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
public class TestCreateChunk
{
    [GdUnit4.TestCase]
    public void TestBasicChunkCreation()
    {
        MarchingTrianglesTerrain.addons.marchingTriangles.MarchingTrianglesTerrain? terrain = null;
        MarchingTrianglesTerrainPlugin? plugin = null;
        
        Assert.DoesNotThrow(() =>
        {
            AddNode(new MarchingTrianglesTerrainPlugin());
            terrain = AddNode(new MarchingTrianglesTerrain.addons.marchingTriangles.MarchingTrianglesTerrain());
            terrain.DataDirectory = "res://out";
        });
        AssertThat(terrain.GetChildCount()).IsEqual(0);

        terrain.AddNewChunk(new Vector2I(0, 0), plugin);
        AssertThat(terrain.GetChildCount()).IsEqual(1);
    }
}
