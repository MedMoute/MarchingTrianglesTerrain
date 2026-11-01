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
public class TestChunkGlobalEdition
{
    [GdUnit4.TestCase]
    public void TestEditGlobal()
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

        for (int i = 0; i < terrain.TerrainSettings.ChunkDimensions.X; i++)
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
        
        Assert.That(chunk.DataGrid.Data.Values,Is.All.EqualTo(1f)); //Accessible value, equal to 1f
        Assert.That(chunk.TerrainDualGrid.CompleteCells.Values
                .Select(c=>c.AverageHeight).ToList(),
            Is.EqualTo(Enumerable.Repeat(1f, cellCount).ToList())); //Accessible avg, equal to 1f

    }
    
    [GdUnit4.TestCase]
    public void TestEditGlobalTwice()
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

        for (int i = 0; i < terrain.TerrainSettings.ChunkDimensions.X; i++)
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

        for (int i = 0; i < terrain.TerrainSettings.ChunkDimensions.X; i++)
        {
            for (int j = 0; j < terrain.TerrainSettings.ChunkDimensions.Y; j++)
            {
                for (int k = 0; k < DoubleDeltaTileOrientationSystem.polygonCount; k++)
                {
                    chunkHeightEdition2.Add(new Vector3I(i,j,k),2f);
                }
            }
        }
        Assert.DoesNotThrow(()=>plugin.PluginHelper!.ApplyCompositePatternAction(terrain, edition2));
        Assert.That(chunk.DataGrid.Data.Values,Is.All.EqualTo(2f)); //Accessible value, equal to 2f
        Assert.That(chunk.TerrainDualGrid.CompleteCells.Values
                .Select(c=>c.AverageHeight).ToList(),
            Is.EqualTo(Enumerable.Repeat(2f, cellCount).ToList())); //Accessible avg, equal to 2f
    }

    [GdUnit4.TestCase]
    public void TestEditGlobalOnTwoChunks()
    {
        var plugin = AddNode(new MarchingTrianglesTerrainPlugin());
        var terrain = AddNode(new MarchingTrianglesTerrain());
        terrain.AddNewChunk(Vector2I.Zero,plugin);
        terrain.AddNewChunk(Vector2I.Up, plugin);
        
        
        var chunk0 = terrain.Chunks[Vector2I.Zero].Underlying;
        var chunk1 = terrain.Chunks[Vector2I.Up].Underlying;
        
        
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
        heightEdition.Add(Vector2I.Up, chunkHeightEdition);
        edition.Add("height",heightEdition);

        for (int i = 0; i < terrain.TerrainSettings.ChunkDimensions.X; i++)
        {
            for (int j = 0; j < terrain.TerrainSettings.ChunkDimensions.Y; j++)
            {
                for (int k = 0; k < DoubleDeltaTileOrientationSystem.polygonCount; k++)
                {
                    chunkHeightEdition.Add(new Vector3I(i,j,k),1f);
                }
            }
        }
        
        var cellCountInit = (terrain.TerrainSettings.ChunkDimensions.X - 1) *
                        (terrain.TerrainSettings.ChunkDimensions.Y - 1);
        
        var cellCountWithBorder = (terrain.TerrainSettings.ChunkDimensions.X ) *
                            (terrain.TerrainSettings.ChunkDimensions.Y - 1);
        
        Assert.DoesNotThrow(()=>plugin.PluginHelper!.ApplyCompositePatternAction(terrain, edition));
        Assert.That(chunk0.DataGrid.Data.Values,Is.All.EqualTo(1f)); //Accessible value, equal to 1f
        Assert.That(chunk1.DataGrid.Data.Values,Is.All.EqualTo(1f)); //Accessible value, equal to 1f
        
        Assert.That(chunk0.TerrainDualGrid.CompleteCells.Values
                .Select(c=>c.AverageHeight).ToList(),
            Is.EqualTo(Enumerable.Repeat(1f, cellCountInit).ToList())); //Accessible avg, equal to 1f
        
        Assert.That(chunk1.TerrainDualGrid.CompleteCells.Values
                .Select(c=>c.AverageHeight).ToList(),
            Is.EqualTo(Enumerable.Repeat(1f, cellCountWithBorder).ToList())); //Accessible avg, equal to 1f
    }
    
    
}