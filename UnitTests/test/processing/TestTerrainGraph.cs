using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

namespace UnitTests.test.processing;

/// <summary>
/// Test class for <see cref="TerrainChunkGraph"/>
/// </summary>
public class TestTerrainGraph
{
    [Test]
    public void TestGraphCreationFromConstructor()
    {
        // Build graph with public constructor
        var graph = new TerrainChunkGraph(null);
        // Check graph is not populated
        Assert.That(graph, Is.Not.Null);
        
        Assert.Multiple(() =>
        {
            Assert.That(graph.Vertices, Is.Empty);
            Assert.That(graph.Edges, Is.Empty);
        });
    }

    [Test]
    public void TestGraphCreationFromChunkStaticCall()
    {
        var dimension = 2;

        var preCreation = Triangulation.PeekId();
        var chunk = NewChunk(Vector2I.Zero, dimension);
        var nbTriCreated = Triangulation.PeekId() - preCreation;
        
        var graph = chunk.Graph;
        Assert.That(graph, Is.Not.Null);
        
        Assert.Multiple(() =>
        {
            Assert.That(graph.Vertices, Is.Not.Empty);
            Assert.That(graph.Edges, Is.Not.Empty);
        });
        
        //For dimension = 2; only one cell is complete.
        Assert.Multiple(() =>
        {
            Assert.That(graph.Vertices.Count(), Is.EqualTo(7));//6+1
            Assert.That(graph.Edges.Count(), Is.EqualTo(12)); //6 int + 6 ext
        });
        
        Assert.That(nbTriCreated,Is.EqualTo(6));
        
        //Check that the EdgeExtremities and SubCellBorders have been created
        List<(Vector3I indices ,EdgeExtremity eeInstance)> collectedEdgeExtremities = CollectEdgeExtremities(graph);
        
        // Assert.That(collectedEdgeExtremities
        //     .Count(p => p.eeInstance.Triangulation!.Id == 0),Is.EqualTo(6));
        
        Assert.That(collectedEdgeExtremities
            .Count(p => p.indices == new Vector3I(0,1,-1)),Is.EqualTo(6));

        Assert.Multiple(() =>
        {
            for (int i = 0; i < HexTerrainCell.VertexCount; i++)
            {
                Assert.That(collectedEdgeExtremities
                    .Count(p => p.indices == new Vector3I(0, 1, i)), Is.EqualTo(3));
            }
        });
        
        Assert.That(collectedEdgeExtremities.Count,Is.EqualTo((6 * 2 * 2) + (1 * 6 * 2) ));
        


        List<(Vector3I indices ,EdgeExtremity eeInstance)> CollectEdgeExtremities(TerrainChunkGraph graphTested)
        {
            return
            [
                .. from vertex in graphTested.Vertices
                from ee
                    in vertex.RelatedEdgeExtremities
                select (ee.Key, ee.Value)
            ];
        }
    }

    private static HexagonalTerrainChunk NewChunk(Vector2I coordinates, int dimension)
    {
        return HexagonalTerrainChunk.NewChunkAtCoordinates(coordinates, Vector2I.One*dimension,_=>null);
    }
}