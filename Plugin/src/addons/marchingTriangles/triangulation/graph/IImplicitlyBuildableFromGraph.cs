using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.graph;

/// <summary>
/// Interface for components that can be built from the graph
/// </summary>
public interface  IImplicitlyBuildableFromGraph<in T>
{
    protected TerrainChunkGraph ParentGraph { get; }

}