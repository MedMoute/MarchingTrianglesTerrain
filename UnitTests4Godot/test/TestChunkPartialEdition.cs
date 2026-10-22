using GdUnit4;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using NUnit.Framework;
using static GdUnit4.Assertions;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.test;

/// <summary>
/// Test edition of chunks via the plugin
/// </summary>
[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class TestChunkPartialEdition
{
    [GdUnit4.TestCase]
    public void TestEditLocalNotAffectingCells()
    {
        var plugin = AddNode(new MarchingTrianglesTerrainPlugin());
        var terrain = AddNode(new MarchingTrianglesTerrain());
        
        terrain.AddNewChunk(Vector2I.Zero, plugin);
        var chunk = terrain.Chunks[Vector2I.Zero].Underlying;
        var cellCount = (terrain.TerrainSettings.ChunkDimensions.X - 1) *
                        (terrain.TerrainSettings.ChunkDimensions.Y - 1);
        //Try editing the terrain

        var edition = new Godot.Collections.Dictionary<
            string,
            Godot.Collections.Dictionary<
                Vector2I,
                Godot.Collections.Dictionary<
                    Vector3I,
                    Variant>>>();

        var heightEdition = new Godot.Collections.Dictionary<
            Vector2I,
            Godot.Collections.Dictionary<
                Vector3I,
                Variant>>();

        var chunkHeightEdition = new Godot.Collections.Dictionary<
            Vector3I,
            Variant>();
        
        heightEdition.Add(Vector2I.Zero, chunkHeightEdition);
        edition.Add("height",heightEdition);

        for (int i = 0; i < 1; i++) //Only X=0 is affected
        {
            for (int j = 0; j < terrain.TerrainSettings.ChunkDimensions.Y; j++)
            {
                for (int k = 0; k < DoubleDeltaTileOrientationSystem.polygonCount; k++)
                {
                    chunkHeightEdition.Add(new Vector3I(i,j,k),1f);
                }
            }
        }

        Assert.That(plugin.PluginHelper, Is.Not.Null);
        //Apply edition
        Assert.DoesNotThrow(()=>plugin.PluginHelper!.ApplyCompositePatternAction(terrain, edition));

        var scaleSize = DoubleDeltaTileOrientationSystem.polygonCount;
        var expected = Enumerable.Repeat(1f, scaleSize* terrain.TerrainSettings.ChunkDimensions.X)
            .Concat(Enumerable.Repeat(0f, scaleSize *
                terrain.TerrainSettings.ChunkDimensions.X * (terrain.TerrainSettings.ChunkDimensions.Y - 1)));
        
        Assert.That(chunk.DataGrid.Data.Values,Is.EqualTo(expected)); //Accessible value
      
        var expectedDual = Enumerable.Repeat(0.5f,  terrain.TerrainSettings.ChunkDimensions.X-1)
            .Concat(Enumerable.Repeat(0f, (terrain.TerrainSettings.ChunkDimensions.X-1) * (terrain.TerrainSettings.ChunkDimensions.Y - 2)));
        
        Assert.That(chunk.TerrainDualGrid.CompleteCells.Values
                .Select(c=>c.AverageHeight).ToList(),
            Is.EqualTo(expectedDual)); //Accessible avg, equal to 0f
                                                                    //(full cells not affected by X=0 )

    }
    
    [GdUnit4.TestCase]
    public void TestEditLocalTwiceOnSamePoints()
    {
        var plugin = AddNode(new MarchingTrianglesTerrainPlugin());
        var terrain = AddNode(new MarchingTrianglesTerrain());
        
        terrain.AddNewChunk(Vector2I.Zero, plugin);
        var chunk = terrain.Chunks[Vector2I.Zero].Underlying;
        var cellCount = (terrain.TerrainSettings.ChunkDimensions.X - 1) *
                        (terrain.TerrainSettings.ChunkDimensions.Y - 1);
        //Try editing the terrain

        var edition = new Godot.Collections.Dictionary<
            string,
            Godot.Collections.Dictionary<
                Vector2I,
                Godot.Collections.Dictionary<
                    Vector3I,
                    Variant>>>();

        var heightEdition = new Godot.Collections.Dictionary<
            Vector2I,
            Godot.Collections.Dictionary<
                Vector3I,
                Variant>>();

        var chunkHeightEdition = new Godot.Collections.Dictionary<
            Vector3I,
            Variant>();
        
        heightEdition.Add(Vector2I.Zero, chunkHeightEdition);
        edition.Add("height",heightEdition);

        for (int i = 0; i < 1; i++) //Only X=0 is affected
        {
            for (int j = 0; j < terrain.TerrainSettings.ChunkDimensions.Y; j++)
            {
                for (int k = 0; k < DoubleDeltaTileOrientationSystem.polygonCount; k++)
                {
                    chunkHeightEdition.Add(new Vector3I(i,j,k),1f);
                }
            }
        }

        Assert.That(plugin.PluginHelper, Is.Not.Null);
        //Apply edition
        Assert.DoesNotThrow(()=>plugin.PluginHelper!.ApplyCompositePatternAction(terrain, edition));
        
        var edition2 = new Godot.Collections.Dictionary<
            string,
            Godot.Collections.Dictionary<
                Vector2I,
                Godot.Collections.Dictionary<
                    Vector3I,
                    Variant>>>();

        var heightEdition2 = new Godot.Collections.Dictionary<
            Vector2I,
            Godot.Collections.Dictionary<
                Vector3I,
                Variant>>();

        var chunkHeightEdition2 = new Godot.Collections.Dictionary<
            Vector3I,
            Variant>();
        
        heightEdition2.Add(Vector2I.Zero, chunkHeightEdition2);
        edition2.Add("height",heightEdition2);

        for (int i = 0; i < 1; i++) //Only X=0 is affected
        {
            for (int j = 0; j < terrain.TerrainSettings.ChunkDimensions.Y; j++)
            {
                for (int k = 0; k < DoubleDeltaTileOrientationSystem.polygonCount; k++)
                {
                    chunkHeightEdition2.Add(new Vector3I(i,j,k),2f);
                }
            }
        }
        
        Assert.That(plugin.PluginHelper, Is.Not.Null);
        //Apply edition
        Assert.DoesNotThrow(()=>plugin.PluginHelper!.ApplyCompositePatternAction(terrain, edition2));

        var scaleSize = DoubleDeltaTileOrientationSystem.polygonCount;
        var expected = Enumerable.Repeat(2f, scaleSize* terrain.TerrainSettings.ChunkDimensions.X)
            .Concat(Enumerable.Repeat(0f, scaleSize *
                                          terrain.TerrainSettings.ChunkDimensions.X * (terrain.TerrainSettings.ChunkDimensions.Y - 1)));
        
        Assert.That(chunk.DataGrid.Data.Values,Is.EqualTo(expected)); //Accessible value
      
        var expectedDual = Enumerable.Repeat(1f,  terrain.TerrainSettings.ChunkDimensions.X-1)
            .Concat(Enumerable.Repeat(0f, (terrain.TerrainSettings.ChunkDimensions.X-1) * (terrain.TerrainSettings.ChunkDimensions.Y - 2)));
        
        Assert.That(chunk.TerrainDualGrid.CompleteCells.Values
                .Select(c=>c.AverageHeight).ToList(),
            Is.EqualTo(expectedDual)); //Accessible avg, equal to 0f
        //(full cells not affected by X=0 )

    }
}