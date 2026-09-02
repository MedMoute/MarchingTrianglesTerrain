using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace UnitTests;

public class TestChunkSerialization
{
    [Test]
    public void TestCanWriteChunkDataToStructure()
    {
        var chunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null);
        var dataStructImpl = new MttChunkData.ChunkDataStructImpl();

        Assert.DoesNotThrow(() => MttDataHandler.FillDataStructFromChunk(dataStructImpl, chunk));
    }

    [Test]
    public void TestCanWriteChunkDataFromStructure()
    {
        var chunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null);
        var dataStructImpl = new MttChunkData.ChunkDataStructImpl();
        MttDataHandler.FillDataStructFromChunk(dataStructImpl, chunk);
        var newChunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null);

        Assert.DoesNotThrow(() => MttDataHandler.FillChunkFromData(dataStructImpl, newChunk)
        );
    }

    [Test]
    public void TestRoundTripEquals()
    {
        var chunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null);
        var dataStructImpl = new MttChunkData.ChunkDataStructImpl();

        MttDataHandler.FillDataStructFromChunk(dataStructImpl, chunk);

        var newChunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null);
        MttDataHandler.FillChunkFromData(dataStructImpl, newChunk);
        
        Assert.That(newChunk.Coordinates,Is.EqualTo(chunk.Coordinates));
        Assert.That(newChunk.Dimensions, Is.EqualTo(chunk.Dimensions));
        Assert.That(newChunk.MergeMode, Is.EqualTo(chunk.MergeMode));
        Assert.That(newChunk.MergeThreshold, Is.EqualTo(chunk.MergeThreshold));
        //
        Assert.That(newChunk.DataGrid.OrientationSystem.GetHashCode(),
            Is.EqualTo(chunk.DataGrid.OrientationSystem.GetHashCode()));
        
        Assert.That(newChunk.DataGrid.Data,Is.EqualTo(chunk.DataGrid.Data));
        Assert.That(newChunk.DataGrid.Points,Is.EqualTo(chunk.DataGrid.Points));
        Assert.That(newChunk.DataGrid.Size, Is.EqualTo(chunk.DataGrid.Size));
        
        Assert.That(newChunk.existingNeighbors,Is.EqualTo(chunk.existingNeighbors));
        
        //No Equals impl on TerrainColorMaps
        //Assert.That(newChunk.ColorMaps, Is.EqualTo(chunk.ColorMaps));
    }
}