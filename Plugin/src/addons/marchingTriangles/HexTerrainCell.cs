using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MarchingTrianglesTerrain.addons.marchingTriangles.triangulation.action;
using MathNet.Spatial.Euclidean;
using static MarchingTrianglesTerrain.addons.marchingTriangles.utils.EngineUtils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class HexTerrainCell
{
    public Tuple<GeometryMode, GeometryMode>? GeometryModesOverride;

    // DO NOT USE : Not implemented : having a  per-cell threshold will create non-conformal meshes.
    // FIXME : Threshold override should be done on cell edge level, not @per-cell level
    public Tuple<float, ThresholdComputationMode>? ThresholdOverride;

    public bool Verbose;
    public bool RunIntegrityChecks;

    /// <summary>
    /// The coordinates of the current cell in the paren chunk's hex frame.
    /// Since the Hexagonal tiling only have one cell, the Z component can be skipped 
    /// </summary>
    public Vector2I CellCoordsImplicit
    {
        get => _cellCoordsImplicit;
        private init
        {
            _cellCoordsImplicit = value;
            _cellCoords = new Vector3I(value.X, value.Y, 0);
        }
    }

    /// <summary>
    /// The coordinates of the current cell in the parent chunk's hex frame.
    /// </summary>
    public Vector3I CellCoords
    {
        get => _cellCoords;
        set
        {
            _cellCoordsImplicit = new Vector2I(value.X, value.Y);
            _cellCoords = value;
        }
    }

    /// Field backing the coordinates property
    private Vector3I _cellCoords;

    /// Field backing the implicit coordinates property
    private Vector2I _cellCoordsImplicit;

    /// <summary>
    /// The mapping between the current cell vertex indices and indexes of dual cells (e.g. double triangle cells) 
    /// that have one of their centroids as one vertex of this cell. 
    /// </summary>
    public Dictionary<int, Vector3I> DualCellsMapping { get; }

    /// <summary>
    /// Public property for the amount of vertices in this cell.
    /// </summary>
    public static int VertexCount => 6;

    public readonly List<Vector2D> VertexPositionsInPlane;

    /// <summary>
    ///  The underlying frame used for coordinates computations
    /// </summary>
    private readonly RegularUniformFrame _orientationSystem;


    /// <summary>
    /// The dictionary of "visits" performed by dual cells, with the offset coordinates of the chunk that visited the cell.
    /// <p>
    /// A hexagonal cell is visited by dual triangular cells to notify that a vertex of the
    /// hex cell has a corresponding dual coordinate.
    /// When a cell is visited by a chunk, we register the offset of the chunk that actually holds the data of the cell
    /// </p>
    /// </summary>
    public Dictionary<Vector3I, Vector2I> Visits => _visits;

    private readonly Dictionary<Vector3I, Vector2I> _visits = new();


    /// <summary>
    /// The cartesian position of the center of this cell
    /// </summary>
    public Vector2D CenterPosition { get; }

    internal CellDataArrays TempDataArrays { get; }

    // TODO support object[]
    public Func<int, float>? GetVertexData;

    public bool FloorMode { get; private set; }

    public const String AverageHeightHint = "AvgHeight";

    public const String NextTriangleEdgeAvgHeight = "EdgeAvgHeight";

    public const String NextTriangleVertexPos = "NextTriPos";

    public Func<int, float>? GetEdgeAvgHeight;

    public float AverageHeight
    {
        //TODO save value in cache
        get
        {
            float sum = 0;
            for (int i = 0; i < VertexCount; i++)
            {
                if (GetVertexData != null) sum += GetVertexData(i);
            }

            return sum / VertexCount;
        }
    }


    public HexTerrainCell(
        Vector2I cellCoordsImpl,
        RegularUniformFrame orientationSystem,
        RegularUniformFrame dualFrame)
    {
        CellCoordsImplicit = cellCoordsImpl;
        _orientationSystem = orientationSystem;
        TempDataArrays = new CellDataArrays(cellCoordsImpl);
        DualCellsMapping = new Dictionary<int, Vector3I>();
        VertexPositionsInPlane = [];
        CenterPosition = _orientationSystem.GetCellCentroid(CellCoords);


        for (int i = 0; i < VertexCount; i++)
        {
            VertexPositionsInPlane.Add(
                _orientationSystem.GetVertex(
                    CellCoordsImplicit, i, 0));

            DualCellsMapping.Add(
                i,
                _orientationSystem.GetVertexIndexInDualSpace(
                    CellCoords, // Set by CellCoordsImplicit setter
                    dualFrame,
                    i));
        }

        if (DualCellsMapping.Count != 6)
        {
            throw new Exception($"Something is wrong, a hexagonal cell should always" +
                                $" be affected by 6 dual triangles, got {DualCellsMapping.Count} instead ");
        }
    }


    public void SetDataFetchingFunction(
        Vector2I dimensions2D,
        Func<Vector2I, TriangleGrid> dataProviderProvider,
        Func<Vector2I, bool> doesNeighboringChunkExist)
    {
        // TODO Memoize
        GetVertexData = i =>
        {
            Vector3I vertexIdxInDual = DualCellsMapping[i];
            var offset = GetChunkOffsetForDualCell(dimensions2D, vertexIdxInDual);
            if (!doesNeighboringChunkExist(offset))
                throw new InvalidOperationException(
                    "Attempting to fetch data from a chunk that does not seem to exist");

            var scaledOffset = new Vector3I(offset.X * dimensions2D.X, offset.Y * dimensions2D.Y, 0);

            var success = dataProviderProvider(offset).Data.TryGetValue(vertexIdxInDual - scaledOffset, out var value);
            if (success)
            {
                return value;
            }

            return float.NaN;
        };

        GetEdgeAvgHeight = i =>
        {
            if (GetVertexData != null) return (GetVertexData(i) + GetVertexData(mod(i + 1, VertexCount))) / 2f;
            return float.NaN;
        };
    }

    internal static Vector2I GetChunkOffsetForDualCell(Vector2I chunkDimension, Vector3I dualIndex)
    {
        return GetChunkOffsetForDualCell(chunkDimension, new Vector2I(dualIndex.X,dualIndex.Y));
    }
    
    private static Vector2I GetChunkOffsetForDualCell(Vector2I chunkDimension, Vector2I dualIndex)
    {
        var offset = new Vector2I(
            Mathf.FloorToInt(dualIndex.X / (float)chunkDimension.X),
            Mathf.FloorToInt(dualIndex.Y / (float)chunkDimension.Y));
        return offset;
    }

    public bool IsReady()
    {
        return _visits.Count == VertexCount;
    }


    /// <summary>
    ///  Returns the hexagonal coordinates of a given cartesian point.
    /// </summary>
    public Vector2D GetHexCoordsOfPoint(Vector2D pos)
    {
        return _orientationSystem.CartesianToLocal(pos);
    }

    public void VisitedBy(Vector3I triangleCellVisitor, Vector2I chunkDimensions)
    {
        if (!DualCellsMapping.ContainsValue(triangleCellVisitor))
        {
            throw new InvalidOperationException(String.Format(
                "Hexagonal cell {0} cannot allow registering a visit by dual cell {1}." +
                " Allowed cells are in {2} ",
                CellCoords,
                triangleCellVisitor,
                string.Join(",", DualCellsMapping.Values)));
        }

        var offset = GetChunkOffsetForDualCell(chunkDimensions, triangleCellVisitor);

        _visits.Add(triangleCellVisitor, offset);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        _orientationSystem.GetVertexPositions(CellCoordsImplicit).ForEach
        (param =>
            sb.Append(" - ").Append(TerrainToolPluginHelper.FormatVector2(param.Item1))
        );
        return string.Format("{0} (Centered in {2})\n     Points : {1}", CellCoordsImplicit, sb,
            TerrainToolPluginHelper.FormatVector2(CenterPosition));
    }

    private (Vector3[] tri, int Mask) ComputeTriangleAndMask(
        int i, Dictionary<Vector2D,
            float> dataArray,
        HexagonalTerrainChunk chunk)
    {
        var center = CenterPosition;
        var posB = VertexPositionsInPlane[i];
        int index = (i + 1) % 6;
        var posC = VertexPositionsInPlane[index];

        var a = new Vector3((float)center.X, AverageHeight, (float)center.Y);
        var b = new Vector3((float)posB.X, dataArray[posB], (float)posB.Y);
        var c = new Vector3((float)posC.X, dataArray[posC], (float)posC.Y);

        Vector3[] tri = [a, b, c];

        int mask = (Math.Abs(a.Y - c.Y) > chunk.MergeThreshold ? 1 : 0) * 4 +
                   (Math.Abs(b.Y - c.Y) > chunk.MergeThreshold ? 1 : 0) * 2 +
                   (Math.Abs(a.Y - b.Y) > chunk.MergeThreshold ? 1 : 0) * 1;
        return (tri, mask);
    }

    internal void ProcessTrianglesIntoPoints(
        List<TriangleInfo> triangles,
        HexagonalTerrainChunk chunk)
    {
        foreach (var triangleInfo in triangles)
        {
            FloorMode = !triangleInfo.IsWall;

            foreach (var point in triangleInfo.Points)
            {
                chunk.CopyPointDataToCellStructures(point, Vector2.Zero, this);
            }
        }
    }
    
    /// <summary>
    /// Triangle processing output.
    /// </summary>
    public class TriangleInfo
    {
        public bool IsWall { get; set; } = false;
        public Vector3[] Points { get; init; } = new Vector3[3];
        public bool[] EdgeBorderFlags { get; init; } = new bool[3];

        public List<Tuple<Vector3, Vector3>> GetBorderEdges()
        {
            var res = new List<Tuple<Vector3, Vector3>>();

            for (int i = 0; i <= 2; i++)
            {
                if (EdgeBorderFlags[i])
                {
                    res.Add(new Tuple<Vector3, Vector3>(Points[mod(i, 3)], Points[mod(i + 1, 3)]));
                }
            }

            return res;
        }

        public List<Tuple<Vector3, Vector3>> GetEdges()
        {
            var res = new List<Tuple<Vector3, Vector3>>();

            for (int i = 0; i <= 2; i++)
            {
                res.Add(new Tuple<Vector3, Vector3>(Points[mod(i - 1, 3)], Points[mod(i, 3)]));
            }

            return res;
        }
    }

    public static double GetSignedArea(Vector3[] tri)
    {
        if (tri.Length != 3)
            throw new ArgumentException("Illegal Argument");
        var z = Vector3.Up.Dot((tri[1] - tri[0]).Cross(tri[2] - tri[0])) / 2;
        return z;
    }

    public Dictionary<Vector2D, float> ExtractDataFromCell()
    {
        if (GetVertexData == null)
        {
            throw new InvalidOperationException("GetVertexData is null");
        }
        Dictionary<Vector2D, float> tempHexagonData = new();

        for (int i = 0; i < VertexCount; i++)
        {
            tempHexagonData.Add(
                _orientationSystem.GetVertex(_cellCoordsImplicit, i, 0),
                GetVertexData(i));
        }

        return tempHexagonData;
    }

    internal List<Vector2I> FetchNeighboringCellsData(Dictionary<Vector2I, HexTerrainCell> pendingDataDictionary,HexagonalTerrainChunk chunk)
    {
        var neighbors = GetNeighborCellsCoordinates();

        var neighborCells = chunk.GetHexCells(cell => neighbors.Contains(cell.CellCoords));
        foreach (var cell in neighborCells)
        {
            pendingDataDictionary.TryAdd(cell.CellCoordsImplicit, cell);
        }

        return [.. neighbors.Select(v => new Vector2I(v.X, v.Y))];
    }

    /// <summary>
    /// Returns the list of the cells indexes that touch this cell. 
    /// </summary>
    /// <returns></returns>
    public List<Vector3I> GetNeighborCellsCoordinates()
    {
        // Trivial with cube coordinates
        return [CellCoords+Vector3I.Right-Vector3I.Up,
            CellCoords-Vector3I.Right+Vector3I.Up,
            CellCoords+Vector3I.Back-Vector3I.Up,
            CellCoords-Vector3I.Back+Vector3I.Up,
            CellCoords+Vector3I.Back-Vector3I.Right,
            CellCoords-Vector3I.Back+Vector3I.Right];  
    }
}

internal class CellDataArrays(Vector2I cellCoord)
{
    public readonly List<Vector3> Pt = new();
    public readonly List<Vector2> Uv = new();
    public readonly List<Vector2> Uv2 = new();
    public readonly List<Color> Color0 = new();
    public readonly List<Color> Color1 = new();
    public readonly List<Color> Custom1Value = new();
    public readonly List<Color> Custom3Value = new();
    public readonly List<Color> MatBlend = new();
    public readonly List<bool> Floor = new();

    public readonly Vector2I CellCoord = cellCoord;

    public void EnsureProcessable()
    {
        if (Pt.Count % 3 != 0
            || Pt.Count != Uv.Count
            || Pt.Count != Uv2.Count
            || Pt.Count != Color0.Count
            || Pt.Count != Color1.Count
            || Pt.Count != Custom1Value.Count
            || Pt.Count != Custom3Value.Count
            || Pt.Count != MatBlend.Count
            || Pt.Count != Floor.Count)
        {
            throw new ArgumentException("The cell data array is wrongly shaped.");
        }
    }

    public void Clear()
    {
        Pt.Clear();
        Uv.Clear();
        Uv2.Clear();
        Color0.Clear();
        Color1.Clear();
        Custom1Value.Clear();
        Custom3Value.Clear();
        MatBlend.Clear();
        Floor.Clear();
    }
}

public class V3Comp : IComparer<Vector3>
{
    public int Compare(Vector3 x, Vector3 y)
    {
        var xComparison = x.X.CompareTo(y.X);
        if (xComparison != 0) return xComparison;
        var yComparison = x.Y.CompareTo(y.Y);
        if (yComparison != 0) return yComparison;
        return x.Z.CompareTo(y.Z);
    }
}

public class UnorderedTupleComparer : IComparer<(int, int)>, IEqualityComparer<(int, int)>
{
    private UnorderedTupleComparer()
    {
    }

    public static readonly UnorderedTupleComparer Instance = new();

    public bool Equals((int, int) t1, (int, int) t2)
    {
        return (Math.Min(t1.Item1, t1.Item2) == Math.Min(t2.Item1, t2.Item2)) &&
               (Math.Max(t1.Item1, t1.Item2) == Math.Max(t2.Item1, t2.Item2));
    }

    public int GetHashCode((int, int) t)
    {
        // Order-independent hash code (e.g., XOR or sum of elements)
        return t.Item1.GetHashCode() ^ t.Item2.GetHashCode();
    }

    public int Compare((int, int) x, (int, int) y)
    {
        if (x.Equals(y))
            return 0;
        return x.CompareTo(y);
    }
}