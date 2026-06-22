using Godot;

namespace Localproto.addons.marchingTriangles;

/// <summary>
/// Inner Class for storing the chunk's color maps
/// </summary>
/// <param name="ground_0"></param>
/// <param name="ground_1"></param>
/// <param name="wall_0"></param>
/// <param name="wall_1"></param>
public class TerrainColorMaps(
    HexagonalTerrainChunk parent,
    Dictionary<Vector3I, Color> ground_0,
    Dictionary<Vector3I, Color> ground_1,
    Dictionary<Vector3I, Color> wall_0,
    Dictionary<Vector3I, Color> wall_1)
{
    public Color GetGroundColor0(Vector3I cellCoords)
    {
        EnsureRange(cellCoords,parent.Dimensions);
        return ground_0[cellCoords];
    }

    public void SetGroundColor0(Vector3I cellCoords, Color value)
    {
        EnsureRange(cellCoords,parent.Dimensions);
        ground_0[cellCoords] = value;
    }

    public void DrawGroundColor0(Vector3I cellCoords, Color value)
    {
        SetGroundColor0(cellCoords, value);
        parent.Dirty = true;
        NotifySelfAndNeighborsForUpdate(cellCoords);
    }

    public Color GetGroundColor1(Vector3I cellCoords)
    {
        EnsureRange(cellCoords,parent.Dimensions);
        return ground_1[cellCoords];
    }

    public void SetGroundColor1(Vector3I cellCoords, Color value)
    {
        EnsureRange(cellCoords,parent.Dimensions);
        ground_1[cellCoords] = value;
    }

    public void DrawGroundColor1(Vector3I cellCoords, Color value)
    {
        SetGroundColor1(cellCoords, value);
        parent.Dirty = true;
        NotifySelfAndNeighborsForUpdate(cellCoords);
    }

    public Color GetWallColor0(Vector3I cellCoords)
    {
        EnsureRange(cellCoords,parent.Dimensions);
        return wall_0[cellCoords];
    }

    public void SetWallColor0(Vector3I cellCoords, Color value)
    {
        EnsureRange(cellCoords,parent.Dimensions);
        wall_0[cellCoords] = value;
    }

    public void DrawWallColor0(Vector3I cellCoords, Color value)
    {
        SetWallColor0(cellCoords, value);
        parent.Dirty = true;
        NotifySelfAndNeighborsForUpdate(cellCoords);
    }

    public Color GetWallColor1(Vector3I cellCoords)
    {
        EnsureRange(cellCoords,parent.Dimensions);
        return wall_1[cellCoords];
    }

    public void SetHeight(Vector3I cell, float yValue)
    {
        EnsureRange(cell,parent.Dimensions);
        parent.DataGrid.Data[cell] = yValue;
    }

    public void SetWallColor1(Vector3I cellCoords, Color value)
    {
        EnsureRange(cellCoords,parent.Dimensions);
        wall_1[cellCoords] = value;
    }

    public void DrawWallColor1(Vector3I cellCoords, Color value)
    {
        SetWallColor1(cellCoords, value);
        parent.Dirty = true;
        NotifySelfAndNeighborsForUpdate(cellCoords);
    }

    public void DrawHeight(Vector3I cell, float yValue)
    {
        SetHeight(cell, yValue);
        parent.Dirty = true;
        NotifySelfAndNeighborsForUpdate(cell);
    }

    private void NotifySelfAndNeighborsForUpdate(Vector3I cell)
    {
        NotifyNeedUpdate(cell);
        NotifyNeedUpdate(cell + Vector3I.Down);
        NotifyNeedUpdate(cell + Vector3I.Up);
        NotifyNeedUpdate(cell + Vector3I.Left);
        NotifyNeedUpdate(cell + Vector3I.Right);
    }

    private void NotifyNeedUpdate(Vector3I cellCoords)
    {
        if (EnsureRange(cellCoords, parent.Dimensions,false))
            parent.NeedUpdate[cellCoords] = true;
    }

    public static bool EnsureRange(Vector3I cellCoords, Vector3I allowedDimensions, bool throwOnBadRange = true)
    {
        if (cellCoords.X < 0 || cellCoords.X >= allowedDimensions.X)
        {
            if (throwOnBadRange)
                throw new ArgumentOutOfRangeException(nameof(cellCoords),
                    "cellCoords.X must be between 0 and the allowed X dimension ");
            return false;
        }

        if (cellCoords.Y < 0 || cellCoords.Y >= allowedDimensions.Y)
        {
            if (throwOnBadRange)
                throw new ArgumentOutOfRangeException(nameof(cellCoords),
                    "cellCoords.Y must be between 0 and the allowed Y dimension ");
            return false;
        }

        if (cellCoords.Z < 0 || cellCoords.Z >= allowedDimensions.Z)
        {
            if (throwOnBadRange)
                throw new ArgumentOutOfRangeException(nameof(cellCoords),
                    "cellCoords.Z must be between 0 and (strictly) the parent Chunk.Z dimension" +
                    " (the amount of polygons in the cell)");
            return false;
        }

        return true;
    }
}