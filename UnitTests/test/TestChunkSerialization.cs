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
    }
}