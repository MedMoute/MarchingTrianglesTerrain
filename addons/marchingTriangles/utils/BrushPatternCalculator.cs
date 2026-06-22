using Godot;
using MathNet.Spatial.Euclidean;

namespace Localproto.addons.marchingTriangles.utils;

[Tool]
public class BrushPatternCalculator
{
    //TODO : unit test this ?
    /// <summary>
    /// Computes the theoretical global bounding boxes for the plugin's brush
    /// </summary>
    /// <param name="position"></param>
    /// <param name="brushDiameter"></param>
    /// <param name="terrain"></param>
    /// <returns></returns>
    public static BrushBounds CalculateBounds(Vector3 position, float brushDiameter, MarchingTrianglesTerrain terrain)
    {
        float minZPosition = position.Z - brushDiameter / 2;
        float maxZPosition = position.Z + brushDiameter / 2;

        float minXPosition = position.X - brushDiameter / 2;
        float maxXPosition = position.X + brushDiameter / 2;
        // We compute the global cell indexes 
        Vector2I cellIdxAtMinPosition = TerrainSettings.OrientationSystem.GetCell(
            new Vector2D(minXPosition, minZPosition));

        Vector2I cellIdxAtMaxPosition = TerrainSettings.OrientationSystem.GetCell(
            new Vector2D(maxXPosition, maxZPosition));


        int minXChunkIdx = Mathf.FloorToInt((float)cellIdxAtMinPosition.X / terrain.TerrainSettings.ChunkDimensions.X);
        int maxXChunkIdx = Mathf.FloorToInt((float)cellIdxAtMaxPosition.X / terrain.TerrainSettings.ChunkDimensions.X);
        int minYChunkIdx = Mathf.FloorToInt((float)cellIdxAtMinPosition.Y / terrain.TerrainSettings.ChunkDimensions.Y);
        int maxYChunkIdx = Mathf.FloorToInt((float)cellIdxAtMaxPosition.Y / terrain.TerrainSettings.ChunkDimensions.Y);

        int minXCell = mod(cellIdxAtMinPosition.X, terrain.TerrainSettings.ChunkDimensions.X);
        int maxXCell = mod(cellIdxAtMaxPosition.X, terrain.TerrainSettings.ChunkDimensions.X);
        int minYCell = mod(cellIdxAtMinPosition.Y, terrain.TerrainSettings.ChunkDimensions.Y);
        int maxYCell = mod(cellIdxAtMaxPosition.Y, terrain.TerrainSettings.ChunkDimensions.Y);


        Vector2I blChunk = new Vector2I(minXChunkIdx, minYChunkIdx);
        Vector2I trChunk = new Vector2I(maxXChunkIdx, maxYChunkIdx);

        Vector2I blCell = new Vector2I(minXCell, minYCell);
        Vector2I trCell = new Vector2I(maxXCell, maxYCell);
        return new BrushBounds(
            new Tuple<Vector2I, Vector2I>(blChunk, trChunk),
            new Tuple<Vector2I, Vector2I>(blCell, trCell));
    }

    public static float CalculateMaxSqDistance(float brushSize, int brushIndex)
    {
        switch (brushIndex)
        {
            case 0: //Round brush
                return brushSize*brushSize;
            case 1: //Square Brush
                return 2*brushSize*brushSize;
            default:
                throw new NotImplementedException("Brush Index Not supported");
        }
    }

    public static Tuple<Vector2I, Vector2I> GetCellRangeForChunk(
        Vector2I chunkCoords,
        BrushBounds brushBounds,
        MarchingTrianglesTerrain terrain)
    {
        var xMin = chunkCoords.X == brushBounds.ChunkAABB.Item1.X ? brushBounds.CellAABB.Item1.X : 0;
        var xMax = chunkCoords.X == brushBounds.ChunkAABB.Item2.X
            ? brushBounds.CellAABB.Item2.X
            : terrain.TerrainSettings.ChunkDimensions.X-1;
        var zMin = chunkCoords.Y == brushBounds.ChunkAABB.Item1.Y ? brushBounds.CellAABB.Item1.Y : 0;
        var zMax = chunkCoords.Y == brushBounds.ChunkAABB.Item2.Y
            ? brushBounds.CellAABB.Item2.Y
            : terrain.TerrainSettings.ChunkDimensions.Y-1;
        return new Tuple<Vector2I, Vector2I>(new Vector2I(xMin, zMin), new Vector2I(xMax, zMax));
    }

    public static float CalculateFalloffSample(
        Vector2 worldPosition,
        Vector2 brushPos,
        double brushSize,
        int brushIndex,
        float maxDistance,
        bool useFalloff,
        Curve falloffCurve)
    {
        var distanceSquared = brushPos.DistanceSquaredTo(worldPosition);
        if (distanceSquared > maxDistance)
        {
            return -1; // Outside of brush effect
        }

        if (!useFalloff)
        {
            return 1;
        }

        float t;
        switch (brushIndex)
        {
            case 0: //Round
                var d = (maxDistance - distanceSquared) / maxDistance;
                t = Mathf.Clamp(d, 0.0f, 1.0f);
                break;
            case 1: //Square brush
                var local = worldPosition - brushPos;
                var uv = local / (float)(brushSize / 2f);
                d = Mathf.Max(Mathf.Abs(uv.X), Mathf.Abs(uv.Y));
                t = 1f - Mathf.Clamp(d, 0.2f, 1.0f);
                break;
            default:
                throw new NotImplementedException("Brush Index Not supported");
        }

        return falloffCurve.Sample(Mathf.Clamp(t, 0.001f, 0.999f));
    }

    /// Returns the cartesian coordinates of the origin of the triangular cell matching the provided chunk and cell indexes. 
    public static Vector2 CellToWorldPosition(Vector2I chunkCoords, Vector2I cellCoords,
        MarchingTrianglesTerrain terrain)
    {
        var chunk = terrain.Chunks[chunkCoords];
        var res = chunk.Underlying.DataGrid.GetCartesianOriginForCellIndex(cellCoords);
        return new Vector2((float)res.X, (float)res.Y);
    }

    public static int mod(int x, int m)
    {
        return (x % m + m) % m;
    }
}

///Brush bounds in the triangular grid space
/// Each tuple entry corresponds respectively to the Top-Left and Bottom-Right coordinates for the entry. 
public record BrushBounds(Tuple<Vector2I, Vector2I> ChunkAABB, Tuple<Vector2I, Vector2I> CellAABB);