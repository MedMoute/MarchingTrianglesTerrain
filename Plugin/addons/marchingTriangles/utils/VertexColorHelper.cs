using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

/// <summary>
/// Helper component for computing/interpolating cell colors.
/// </summary>
public class VertexColorHelper
{
    public static float BlendEdgeSensitivity = 1.25f;

    //Cell height range for boundary detection (height-based color sampling)
    private float _minHeight;

    private float _maxHeight;

    //Height-based material colors for FLOOR boundary cells (prevents color bleeding between heights)
    private Color _floorLowerColor0;
    private Color _floorUpperColor0;

    private Color _floorLowerColor1;

    private Color _floorUpperColor1;

    // Height-based material colors for WALL/RIDGE boundary cells
    private Color _wallLowerColor0;
    private Color _wallUpperColor0;

    private Color _wallLowerColor1;
    private Color _wallUpperColor1;

    private bool _isBoundary;

    // Per-cell materials for to supports up to 3 textures
    private int _cellMatA;
    private int _cellMatB;
    private int _cellMatC;

    /// <summary>
    /// Provider of a function array that will read the data sources for a given Triangular Cell index
    /// </summary>
    private Func<HexTerrainCell, Func<Vector3I, Color>[]> ColorSourceGetterProvider { get; }

    public VertexColorHelper(Func<Vector2I, HexagonalTerrainChunk> neighborChunksProvider)
    {
        ColorSourceGetterProvider = cell =>
        [
            idx =>
            {
                var chunk = ComputeChunkAndOffset(neighborChunksProvider, cell, idx, out var offsetCoords);
                return !cell.FloorMode
                    ? chunk.ColorMaps.GetWallColor0(offsetCoords)
                    : chunk.ColorMaps.GetGroundColor0(offsetCoords);
            },
            idx =>
            {
                var chunk = ComputeChunkAndOffset(neighborChunksProvider, cell, idx, out var offsetCoords);
                return !cell.FloorMode
                    ? chunk.ColorMaps.GetWallColor1(offsetCoords)
                    : chunk.ColorMaps.GetGroundColor1(offsetCoords);
            },
            idx =>
            {
                var chunk = ComputeChunkAndOffset(neighborChunksProvider, cell, idx, out var offsetCoords);
                return chunk.ColorMaps.GetWallColor0(offsetCoords);
            },
            idx =>
            {
                var chunk = ComputeChunkAndOffset(neighborChunksProvider, cell, idx, out var offsetCoords);
                return chunk.ColorMaps.GetWallColor1(offsetCoords);
            }
        ];
    }

    private static HexagonalTerrainChunk ComputeChunkAndOffset(
        Func<Vector2I, HexagonalTerrainChunk> neighborChunksProvider, HexTerrainCell cell,
        Vector3I idx, out Vector3I offsetCoords)
    {
        var offset = cell.Visits[idx];
        var chunk = neighborChunksProvider(offset);
        var offsetCoords2D = offset * chunk.Dimensions2D;
        offsetCoords = idx - new Vector3I(offsetCoords2D.X, offsetCoords2D.Y, 0);
        return chunk;
    }

    public Dictionary<string, Color> BlendColors(GdPluginHexTerrainChunk chunk,
        HexTerrainCell cell,
        Vector3 vertex,
        Vector2 uv,
        bool diagMidPoint = false)
    {
        var colors = new Dictionary<string, Color>();
        // Tweaking the BLEND_EDGE_SENSITIVITY allows more "aggressive" Cliff vs Slope detection
        float blendThreshold = chunk.Underlying.MergeThreshold * BlendEdgeSensitivity;

        List<float> vertexHeights = new();
        float centerHeight = cell.AverageHeight;
        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            vertexHeights.Add(cell.GetVertexData(i));
        }

        List<float> edgeHeightDiffs = new();
        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            edgeHeightDiffs.Add(MathF.Abs(centerHeight - vertexHeights[i]));
            edgeHeightDiffs.Add(MathF.Abs(vertexHeights[i] - vertexHeights[(i + 1) % HexTerrainCell.VertexCount]));
        }

        List<bool> blendEdges = edgeHeightDiffs.Select(v => v < blendThreshold).ToList();

        bool cellHasWallsForBlend = !blendEdges.TrueForAll(b => b);

        // Detect ridge BEFORE selecting color maps (ridge needs wall colors, not ground colors)

        var isRidge = cell.FloorMode && uv.Y > 0f;
        var isLedge = cell.FloorMode && uv.X > 0f;

        bool FloorToWall(bool b) => !b;

        var sources = ColorSourceGetterProvider(cell);

        var useWallColors = FloorToWall(cell.FloorMode);
        //Calculate vertex colors using appropriate interpolation method
        Color lower0 = useWallColors ? _wallLowerColor0 : _floorLowerColor0;
        Color upper0 = useWallColors ? _wallUpperColor0 : _floorUpperColor0;
        colors["color_0"] = InterpolateVertexColor(chunk, cell, vertex, sources[0], diagMidPoint, lower0, upper0);
        Color lower1 = useWallColors ? _wallLowerColor1 : _floorLowerColor1;
        Color upper1 = useWallColors ? _wallUpperColor1 : _floorUpperColor1;
        colors["color_1"] = InterpolateVertexColor(chunk, cell, vertex, sources[1], diagMidPoint, lower1, upper1);

        // isRidge & isLedge are already calculated above
        var custom = Colors.Green;
        custom.G = isRidge ? 1f : 0f;
        custom.B = isLedge ? 1f : 0f;

        //Calculate and store the closest wall color to the ridge/ledge
        Color ridgeLedgeLower0 = _wallLowerColor0;
        Color ridgeLedgeUpper0 = _wallUpperColor0;
        var ridgeLedgeColor0 =
            InterpolateVertexColor(chunk, cell, vertex, sources[2], diagMidPoint, ridgeLedgeLower0, ridgeLedgeUpper0);
        Color ridgeLedgeLower1 = _wallLowerColor1;
        Color ridgeLedgeUpper1 = _wallUpperColor1;
        var ridgeLedgeColor1 =
            InterpolateVertexColor(chunk, cell, vertex, sources[3], diagMidPoint, ridgeLedgeLower1, ridgeLedgeUpper1);

        var ridgeLedgeTextureIdx = GetTextureIndexFromColors(ridgeLedgeColor0, ridgeLedgeColor1);

        custom.A = ridgeLedgeTextureIdx;
        colors["custom_1_value"] = custom;

        // Use edge connection to determine blending path
        // Avoid issues on weird Cliffs vs Slopes blending giving each a different path

        Color materialBlend = GetMaterialBlendData(cell, vertex, sources[0], sources[1]);
        if (cellHasWallsForBlend && cell.FloorMode)
        {
            materialBlend.A = 2f;
        }

        colors["mat_blend"] = materialBlend;

        return colors;
    }


    // Calculate CUSTOM2 blend data with 3 texture support 
    // Encoding: Color(packed_mats, mat_c/15, weight_a, weight_b)
    // R: (mat_a + mat_b * 16) / 255.0  (packs 2 indices, each 0-15)
    // G: mat_c / 15.0
    // B: weight_a (0.0 to 1.0)
    // A: weight_b (0.0 to 1.0), or 2.0 to signal use_vertex_colors
    private Color GetMaterialBlendData(
        HexTerrainCell cell,
        Vector3 vertex,
        Func<Vector3I, Color> sourceMap0,
        Func<Vector3I, Color> sourceMap1)
    {
        List<int> texturesIdx = new();

        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            var coord = cell.DualCellsMapping[i];
            texturesIdx.Add(GetTextureIndexFromColors(sourceMap0(coord), sourceMap1(coord)));
        }

        //  Position weights for linear interpolation
        List<float> vertexWeights = new();

        Vector2D qr = cell.GetHexCoordsOfPoint(new Vector2D(vertex.X, vertex.Z));
        float s = (float)(-qr.X - qr.Y);
        float S = -cell.CellCoordsImplicit.X - cell.CellCoordsImplicit.Y;
        vertexWeights.Add((float)(qr.X - cell.CellCoordsImplicit.X));
        vertexWeights.Add((float)(qr.Y - cell.CellCoordsImplicit.Y));
        vertexWeights.Add(s - S);
        vertexWeights.Add((float)(1 - qr.X + cell.CellCoordsImplicit.X));
        vertexWeights.Add((float)(1 - qr.Y + cell.CellCoordsImplicit.Y));
        vertexWeights.Add(1 - s + S);


        // Accumulate weights for all 3 cell materials
        float weightMatA = 0f;
        float weightMatB = 0f;
        float weightMatC = 0f;


        // Process Each corner
        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            if (texturesIdx[i] == _cellMatA) weightMatA += vertexWeights[i];
            else if (texturesIdx[i] == _cellMatB) weightMatB += vertexWeights[i];
            else if (texturesIdx[i] == _cellMatC) weightMatC += vertexWeights[i];
        }

        // Normalize weights
        var totalWeights = weightMatA + weightMatB + weightMatC;
        if (totalWeights > 0.001f)
        {
            weightMatA /= totalWeights;
            weightMatB /= totalWeights;
        }

        // Pack mat_a and mat_b into one channel (each is 0-15, so together 0-255)
        float packed_mats = (_cellMatA + _cellMatB * 16.0f) / 255.0f;

        return new Color(packed_mats, _cellMatC / 15.0f, weightMatA, weightMatB);
    }

    ///Converts vertex color pair to texture index.
    /// This trick allows us to support 16 textures by using 2 custom colors
    private int GetTextureIndexFromColors(Color color0, Color color1)
    {
        int c0Idx = 0;
        float c0Max = color0.R;
        if (color0.G > c0Max)
        {
            c0Max = color0.G;
            c0Idx = 1;
        }

        if (color0.B > c0Max)
        {
            c0Max = color0.B;
            c0Idx = 2;
        }

        if (color0.A > c0Max)
        {
            c0Idx = 3;
        }

        int c1Idx = 0;
        float c1Max = color1.R;
        if (color1.G > c1Max)
        {
            c1Max = color1.G;
            c1Idx = 1;
        }

        if (color1.B > c1Max)
        {
            c1Max = color1.B;
            c1Idx = 2;
        }

        if (color1.A > c0Max)
        {
            c1Idx = 3;
        }

        return c0Idx * 4 + c1Idx;
    }

    private Color InterpolateVertexColor(
        GdPluginHexTerrainChunk chunk,
        HexTerrainCell cell,
        Vector3 vertex,
        Func<Vector3I, Color> source,
        bool diagMidPoint,
        Color lower0,
        Color upper0)
    {
        if (diagMidPoint)
        {
            return CalcDiagonalColor(chunk, cell, source);
        }

        throw new NotImplementedException();
    }

    private Color CalcDiagonalColor(
        GdPluginHexTerrainChunk chunk,
        HexTerrainCell cell,
        Func<Vector3I, Color> source)
    {
        var idx = new Vector3I(cell.CellCoordsImplicit.X, cell.CellCoordsImplicit.Y, 0);
        // Check if the terrain uses hard edges or blend
        if (chunk.GetParent() != null
            && chunk.GetParent() is MarchingTrianglesTerrain terrain
            && terrain.TerrainSettings.BlendMode == 1)
        {
            // Hard edge mode uses same color as cell's top-left corner
            return source(idx);
        }

        // Smooth blend mode - lerp diagonal corners for smoother effect
        // TODO recycle ?
        Color[] colorArray = new Color[HexTerrainCell.VertexCount];
        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            //Find the index of the vertex in the triangle-based dual grid to fetch the required data
            colorArray[i] = source(cell.DualCellsMapping[i]);
        }

        var diag0 = colorArray[0].Lerp(colorArray[3], 0.5f);
        var diag1 = colorArray[1].Lerp(colorArray[4], 0.5f);
        var diag2 = colorArray[2].Lerp(colorArray[5], 0.5f);

        var result = new Color(
            MathF.Min(diag0.R, MathF.Min(diag1.R, diag2.R)),
            MathF.Min(diag0.G, MathF.Min(diag1.G, diag2.G)),
            MathF.Min(diag0.B, MathF.Min(diag1.B, diag2.B)),
            MathF.Min(diag0.A, MathF.Min(diag1.A, diag2.A))
        );
        // Cleanup for smoother effect
        if (diag0.R > 0.99f || diag1.R > 0.99f || diag2.R > 0.99f) result.R = 1f;
        if (diag0.G > 0.99f || diag1.G > 0.99f || diag2.G > 0.99f) result.G = 1f;
        if (diag0.B > 0.99f || diag1.B > 0.99f || diag2.B > 0.99f) result.B = 1f;
        if (diag0.A > 0.99f || diag1.A > 0.99f || diag2.A > 0.99f) result.A = 1f;
        return result;
    }
}