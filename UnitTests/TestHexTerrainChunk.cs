using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using NUnit.Framework;

namespace UnitTests;

public class TestHexTerrainChunk
{
    [Test]
    public void TestCanCreateBasicChunk()
    {
        Assert.DoesNotThrow(() =>
        {
            var hexTerrainChunk = new HexagonalTerrainChunk(Vector2I.Zero, new Vector2I(3, 3), _=>null);
        });
    }

    [Test] // TODO still an issue somewhere
    public void TestCenterCellIsAvailableWhenAllNeighborsWereAdded()
    {
        // Make sure the 1,-1 hex cell centered on [0,0] is not full in any of the chunks before border processing.
        // Make sure the 1,-1 hex cell centered on [0,0] is now full in one chunk after border processing.

        // Check for every permutation of border processing order
        List<int[]> result = new List<int[]>();
        HeapsAlgorithm.GeneratePermutations([-1, 0], 2, result);
        foreach (var itemOrder in result)
        {
            foreach (var itemOrder2 in result)
            {
                var dims = new Vector2I(6, 6);
                var dico = new Dictionary<Vector2I, HexagonalTerrainChunk>();
                
                var neighborChunkProvider = MarchingTrianglesTerrain.addons.marchingTriangles.MarchingTrianglesTerrain.BuildNeighborChunkProvider(dico.GetValueOrDefault);

                foreach (var i in itemOrder)
                {
                    foreach (var j in itemOrder2)
                    {
                        var pt = new Vector2I(i, j);
                        var newChunk = new HexagonalTerrainChunk(
                            pt, 
                            dims,
                            v=>neighborChunkProvider(pt,v));
                        dico[pt] = newChunk;
                        newChunk.ProcessChunkBorderCells();
                    }
                }

                var centerHexCell = new List<HexTerrainCell>();


                foreach (var terrainChunk in dico)
                {
                    var cellsAtOrigin = terrainChunk.Value.GetHexCells()
                        .Where(cell => cell.CenterPosition.Length < 1e-5).ToList();
                    centerHexCell.AddRange(cellsAtOrigin);
                    Assert.That(() => cellsAtOrigin.Count, Is.EqualTo(1));
                }

                Assert.That(() => centerHexCell.Count, Is.EqualTo(4));
                var readyCells = centerHexCell.Where(cell => cell.IsReady).ToList();
                Assert.That(() => readyCells.Count, Is.EqualTo(1));
            }
        }
    }
}

public static class HeapsAlgorithm
{
    public static void GeneratePermutations<T>(T[] array, int size, List<T[]> result)
    {
        if (size == 1)
        {
            result.Add((T[])array.Clone());
            return;
        }

        for (int i = 0; i < size; i++)
        {
            GeneratePermutations(array, size - 1, result);

            if (size % 2 == 1)
            {
                Swap(ref array[0], ref array[size - 1]);
            }
            else
            {
                Swap(ref array[i], ref array[size - 1]);
            }
        }
    }

    private static void Swap<T>(ref T a, ref T b)
    {
        (a, b) = (b, a);
    }
}