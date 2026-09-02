using System.Collections.Generic;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

public class Vector2INaturalComparer : IComparer<Vector2I>
{
    public static Vector2INaturalComparer Instance = new Vector2INaturalComparer();

    public int Compare(Vector2I a, Vector2I b)
    {
        return a.X.CompareTo(b.X) == 0 ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
    }
}

public class Vector3INaturalComparer : IComparer<Vector3I>
{
    public static Vector3INaturalComparer Instance = new Vector3INaturalComparer();

    public int Compare(Vector3I a, Vector3I b)
    {
        return a.X.CompareTo(b.X) == 0 ? (a.Y.CompareTo(b.Y) == 0 ? a.Z.CompareTo(b.Z) : a.Y.CompareTo(b.Y) ): a.X.CompareTo(b.X);
    }
}