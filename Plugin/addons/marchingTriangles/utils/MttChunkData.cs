using Godot;
using Godot.Collections;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

[Tool]
internal partial class MttChunkData : Resource
{
    [field: Export] public Vector2I ChunkCoords { get; internal set; }
    [field: Export] public int MergeMode { get; internal set; }
    [field: Export] public TriangleGrid DataGrid { get; internal set; }
    [field: Export] public HexagonGrid DualGrid { get; internal set; }

    [field: Export] public Array<byte> GroundTexturesIdx { get; internal set; }

    [field: Export] public Array<byte> WallTexturesIdx { get; internal set; }

    // Transient data saved for caching but regenerated on load if missing
    [field: Export] public Mesh Mesh { get; internal set; }
    [field: Export] public Vector3[] CollisionFaces { get; internal set; }

    public void SetCollisionFromShape(ConcavePolygonShape3D shape)
    {
        if (shape != null)
        {
            CollisionFaces = shape.GetFaces();
        }
    }

    /// <summary>
    /// Helper to create ConcavePolyShape3D from Vector3[] data
    /// </summary>
    public ConcavePolygonShape3D GetCollisionShape()
    {
        if (CollisionFaces.IsEmpty())
        {
            return null;
        }

        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(CollisionFaces);
        return shape;
    }
}