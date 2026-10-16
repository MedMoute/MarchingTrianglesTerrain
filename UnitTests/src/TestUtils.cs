using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

namespace UnitTests;

public static class TestUtils
{
 
    /// <summary>
    /// Asserts that a list of triangles is a proper 2D manifold
    /// </summary>
    /// <param name="res"></param>
    internal static void AssertIsTriangleListManifold(List<HexTerrainCell.TriangleInfo> res)
    {
        var (dico, borderEdgesAsSets, _) = TriangleUtils.ReprocessEdgeGeometry(res);

        // This check ensures the border is bounded
        foreach (var kvp in dico)
        {
            Assert.That(
                EngineUtils.Mod(kvp.Value, 2) == 0 //inner edges 
                || kvp.Value == 1 && borderEdgesAsSets.Contains(kvp.Key, new FloatArrayComparer(1e-5)) //border edge
                , Is.True);
        }

        // This check ensures the surface is a 2 manifold
        foreach (var kvp in dico)
        {
            Assert.That(
                kvp.Value == 2 //inner edges are used once
                || kvp.Value == 1 && borderEdgesAsSets.Contains(kvp.Key, new FloatArrayComparer(1e-5)) //border edge
                , Is.True);
        }

    }
}