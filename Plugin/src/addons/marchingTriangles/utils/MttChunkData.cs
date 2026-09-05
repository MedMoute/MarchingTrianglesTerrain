using Godot;
using Godot.Collections;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

/// <summary>
/// Resource -based classd used for the serialization of a Chunk's metadata
/// </summary>
[Tool]
// Chunk Data Exported by the plugin
public partial class MttChunkData : Resource
{
    [Export] public DataContent Data { get; internal set; }
    [Export] public string ParentTerrainId { get; internal set; }
    [Export] public Vector2I ChunkCoords { get; internal set; }
    [Export] public int MergeMode { get; internal set; }
    
    //TODO -> move these out of the metadata file
    [Export] public Array<byte> GroundTexturesIdx { get; internal set; }
    [Export] public Array<byte> WallTexturesIdx { get; internal set; }
    [Export] public Mesh Mesh { get; internal set; }
    [Export] public Vector3[] CollisionFaces { get; internal set; }

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

/// <summary>
/// Enumeration for disclaiming the format used by the serialization.
/// </summary>
public enum DataContent
{
    V1_STRUCT
}