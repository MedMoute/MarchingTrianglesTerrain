using Godot;
using Godot.Collections;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.data;

/// <summary>
/// Resource -based class used for the serialization of a Chunk's metadata
/// </summary>
[Tool]
// Chunk Data Exported by the plugin
public partial class MttChunkData : Resource
{
    [Export] public DataContent Data { get; internal set; }
    [Export] public string ParentTerrainId { get; internal set; } = null!;
    [Export] public Vector2I ChunkCoords { get; internal set; }
    [Export] public int MergeMode { get; internal set; }

    //TODO -> move these out of the metadata file
    [Export] public Array<byte> GroundTexturesIdx { get; internal set; } = null!;
    [Export] public Array<byte> WallTexturesIdx { get; internal set; } = null!;
    [Export] public Mesh Mesh { get; internal set; } = null!;
    [Export] public Vector3[] CollisionFaces { get; internal set; } = null!;

    public void SetCollisionFromShape(ConcavePolygonShape3D? shape)
    {
        if (shape != null)
        {
            CollisionFaces = shape.GetFaces();
        }
    }

    /// <summary>
    /// Helper to create ConcavePolyShape3D from Vector3[] data
    /// </summary>
    public ConcavePolygonShape3D? GetCollisionShape()
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

/// <summary>
/// Enumeration for disclaiming the format used by the serialization.
/// </summary>
public enum DataContent
{
    V1Struct
}