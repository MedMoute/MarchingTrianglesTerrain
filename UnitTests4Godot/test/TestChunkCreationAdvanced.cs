using GdUnit4;
using Godot;
using NUnit.Framework;
using static GdUnit4.Assertions;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.test;

/// <summary>
/// Advanced testing cases or features
/// </summary>
[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class TestChunkCreationAdvanced
{
    [GdUnit4.TestCase]
    public void TestChunkCreation()
    {
        var plugin = AddNode(new MarchingTrianglesTerrainPlugin());
        var terrain = AddNode(new MarchingTrianglesTerrain());
        
        Assert.That(terrain.Chunks.Count,Is.Zero);
        //Generic checks
        terrain.AddNewChunk(Vector2I.Zero,plugin);
        Assert.That(terrain.Chunks.Count ,Is.EqualTo(1));
        
        var gdChunk = terrain.Chunks[Vector2I.Zero];

        Assert.That(gdChunk, Is.Not.Null);
        Assert.That(gdChunk.Underlying,Is.Not.Null);
        
        var chunk = gdChunk.Underlying;
        
        Assert.That(chunk.Coordinates,Is.EqualTo(Vector2I.Zero));
        Assert.That(chunk.Dimensions2D, Is.EqualTo(terrain.TerrainSettings.ChunkDimensions));
        
        // Frame sanity checks
        Assert.That(chunk.DataGrid,Is.Not.Null);
        Assert.That(chunk.DataGrid.OrientationSystem,Is.Not.Null);
        Assert.That(chunk.TerrainDualGrid,Is.Not.Null);
        Assert.That(chunk.TerrainDualGrid.Frame,Is.Not.Null);  
        //Frame duality equivalence checks
        Assert.That( chunk.DataGrid.OrientationSystem.GetDual().Transform.ToArray(),
            Is.EqualTo(chunk.TerrainDualGrid.Frame.Transform.ToArray()).AsCollection.Within(1E-5));
        Assert.That(chunk.TerrainDualGrid.Frame.GetDual().Transform.ToArray(),
            Is.EqualTo(chunk.DataGrid.OrientationSystem.Transform.ToArray()).AsCollection.Within(1E-5));
        // Data Grid content initialization checks
        Assert.That(chunk.DataGrid.Data.Count,Is.EqualTo( 2*
            terrain.TerrainSettings.ChunkDimensions.X*terrain.TerrainSettings.ChunkDimensions.Y));
        Assert.That(chunk.DataGrid.Data.Values,Is.All.EqualTo(0f)); //Accessible value, equal to 0f
        // Hexagonal Grid content initialization checks
        var cellCount = (terrain.TerrainSettings.ChunkDimensions.X - 1) *
                        (terrain.TerrainSettings.ChunkDimensions.Y - 1);
        Assert.That(chunk.TerrainDualGrid.CompleteCells.Count,Is.EqualTo(cellCount));
        
        Assert.That(chunk.TerrainDualGrid.CompleteCells.Values
            .Select(c=>c.AverageHeight).ToList(),
            Is.EqualTo(Enumerable.Repeat(0f, cellCount).ToList())); //Accessible avg, equal to 0f
        
        // Graph content checks
        Assert.That(chunk.Graph.Vertices.Count(), Is.GreaterThan(0));
        Assert.That(chunk.Graph.Edges.Count(), Is.GreaterThan(0));
        
    }

    [GdUnit4.TestCase]
    public void TestMultipleChunksCreation()
    {
        var plugin = AddNode(new MarchingTrianglesTerrainPlugin());
        var terrain = AddNode(new MarchingTrianglesTerrain());
        var cellCount = (terrain.TerrainSettings.ChunkDimensions.X - 1) *
                        (terrain.TerrainSettings.ChunkDimensions.Y - 1);
        
        Assert.That(terrain.Chunks.Count,Is.Zero);
        //Generic checks
        terrain.AddNewChunk(Vector2I.Zero,plugin);
        //Ensure failure on same point
        Assert.Throws<ArgumentException>(() => terrain.AddNewChunk(Vector2I.Zero, plugin));
        //Check success on neighbor addition
        Assert.DoesNotThrow(() => terrain.AddNewChunk(Vector2I.Up, plugin));

        var chunk0 = terrain.Chunks[Vector2I.Zero].Underlying;
        var chunk1 = terrain.Chunks[Vector2I.Up].Underlying;
        
        Assert.That(chunk0.TerrainDualGrid.CompleteCells.Count,Is.EqualTo(cellCount));
        
        Assert.That(chunk1.TerrainDualGrid.CompleteCells.Count,Is.GreaterThan(cellCount));
        var expectedCellCountWithBorder = (terrain.TerrainSettings.ChunkDimensions.X ) *
                        (terrain.TerrainSettings.ChunkDimensions.Y - 1);
        Assert.That(chunk1.TerrainDualGrid.CompleteCells.Count,Is.EqualTo(expectedCellCountWithBorder));

    }
}