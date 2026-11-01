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
        var chunk = HexagonalTerrainChunk.NewChunkAtCoordinates(Vector2I.Zero, Vector2I.One*dimension,_=>null);
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
        List<(Vector3I indices, TriangulationVertex tvInstance)> cellectedVertexTri = CollectEdgeExtremities(graph);


        Assert.Multiple(() =>
        {
            for (int i = 0; i < HexTerrainCell.VertexCount; i++)
            {
                Assert.That(cellectedVertexTri
                    .Count(p => p.indices == new Vector3I(0, 1, i)), Is.EqualTo(3));
            }
        });
        
        Assert.That(cellectedVertexTri.Count,Is.EqualTo(6 * 3 * 1 ));
        


        List<(Vector3I indices, TriangulationVertex tvInstance)> CollectEdgeExtremities(TerrainChunkGraph graphTested)
        {
            return
            [
                .. from vertex in graphTested.Vertices
                from ee
                    in vertex.TriangulationVertices
                select (ee.Key, ee.Value)
            ];
        }
    }
}