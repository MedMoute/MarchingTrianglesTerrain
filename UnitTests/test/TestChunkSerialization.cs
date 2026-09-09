using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace UnitTests.test;

public class TestChunkSerialization
{
    [Test]
    public void TestCanWriteChunkDataToStructure()
    {
        var chunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null!);
        var dataStructImpl = new ChunkDataStructImpl();

        Assert.DoesNotThrow(() => MttDataHandler.FillDataStructFromChunk(dataStructImpl, chunk));
    }

    [Test]
    public void TestCanReadChunkDataFromStructure()
    {
        var chunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null!);
        var dataStructImpl = new ChunkDataStructImpl();
        MttDataHandler.FillDataStructFromChunk(dataStructImpl, chunk);
        var newChunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null!);

        Assert.DoesNotThrow(() => MttDataHandler.FillChunkFromData(dataStructImpl, newChunk)
        );
    }

    [Test]
    public void TestRoundTripToStructureEquals()
    {
        var chunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null!);
        var dataStructImpl = new ChunkDataStructImpl();

        MttDataHandler.FillDataStructFromChunk(dataStructImpl, chunk);

        var newChunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(10, 10), _ => null!);
        MttDataHandler.FillChunkFromData(dataStructImpl, newChunk);

        Assert.That(newChunk.Coordinates, Is.EqualTo(chunk.Coordinates));
        Assert.That(newChunk.Dimensions, Is.EqualTo(chunk.Dimensions));
        Assert.That(newChunk.MergeMode, Is.EqualTo(chunk.MergeMode));
        Assert.That(newChunk.MergeThreshold, Is.EqualTo(chunk.MergeThreshold));
        //
        Assert.That(newChunk.DataGrid.OrientationSystem.GetHashCode(),
            Is.EqualTo(chunk.DataGrid.OrientationSystem.GetHashCode()));

        Assert.That(newChunk.DataGrid.Data, Is.EqualTo(chunk.DataGrid.Data));
        Assert.That(newChunk.DataGrid.Points, Is.EqualTo(chunk.DataGrid.Points));
        Assert.That(newChunk.DataGrid.Size, Is.EqualTo(chunk.DataGrid.Size));

        Assert.That(newChunk.ExistingNeighbors, Is.EqualTo(chunk.ExistingNeighbors));

        // No easy Equals check on TerrainColorMaps as the arrays are not exposed : instead used a looped getter
        foreach (var cellIdx in chunk.DataGrid.Data.Keys)
        {
            Assert.That(newChunk.ColorMaps.GetGroundColor0(cellIdx),
                Is.EqualTo(chunk.ColorMaps.GetGroundColor0(cellIdx)));
            Assert.That(newChunk.ColorMaps.GetGroundColor1(cellIdx),
                Is.EqualTo(chunk.ColorMaps.GetGroundColor1(cellIdx)));
            Assert.That(newChunk.ColorMaps.GetWallColor0(cellIdx),
                Is.EqualTo(chunk.ColorMaps.GetWallColor0(cellIdx)));
            Assert.That(newChunk.ColorMaps.GetWallColor1(cellIdx),
                Is.EqualTo(chunk.ColorMaps.GetWallColor1(cellIdx)));
        }
    }
}