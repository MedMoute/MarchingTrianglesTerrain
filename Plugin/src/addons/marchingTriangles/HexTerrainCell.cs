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

    // TODO : Cell based ?
    public static double A = 0.5f;
    public static double Theta = 0.1d;

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
    /// The coordinates of the current cell in the paren chunk's hex frame.
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

    public static Vector2I GetChunkOffsetForDualCell(Vector2I chunkDimension, Vector3I dualIndex)
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

    private void DoMarchingTrianglesV2(List<TriangleInfo> processedTriangles, GdPluginHexTerrainChunk chunk)
    {
        ProcessTrianglesIntoPoints(processedTriangles, chunk);
        chunk.ProcessPointsIntoMeshTriangles(this);
        TempDataArrays.Clear();
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

    private void ProcessTrianglesIntoPoints(List<TriangleInfo> trianglesWithWallEdges,
        GdPluginHexTerrainChunk chunk)
    {
        foreach (var trianglesWithWallEdge in trianglesWithWallEdges)
        {
            FloorMode = !trianglesWithWallEdge.IsWall;

            foreach (var point in trianglesWithWallEdge.Points)
            {
                chunk.AddPoint(point, Vector2.Zero, this);
            }
        }
    }

    private List<TriangleInfo> ProcessCellGeometry(
        Dictionary<Vector2D, float> dataArray,
        HexagonalTerrainChunk chunk)
    {
        var effectiveGeometryMode = GeometryModesOverride ?? chunk.DefaultGeometryModes;

        List<TriangleInfo> triangles = [];

        for (var i = 0; i < 6; i++)
        {
            var (tri, mask) = ComputeTriangleAndMask(i, dataArray, chunk);
            Func<int, GeometryMode> computeGeometryMode = ComputeEdgeGeometryMode(i, mask, effectiveGeometryMode);
            Dictionary<string, object> hints = new()
            {
                [AverageHeightHint] = AverageHeight,
                [NextTriangleEdgeAvgHeight] = GetEdgeAvgHeight!(mod(i + 1, VertexCount)),
                [NextTriangleVertexPos] = new Vector3(
                    (float)VertexPositionsInPlane[mod(i + 1, VertexCount)].X,
                    GetVertexData!(mod(i + 1, VertexCount)),
                    (float)VertexPositionsInPlane[mod(i + 1, VertexCount)].Y)
            };

            triangles.AddRange(ProcessTriangle(this, tri, computeGeometryMode, hints));
        }

        return triangles;
    }

    public static Func<int, GeometryMode> ComputeEdgeGeometryMode(
        int triangleIdx,
        int mask,
        Tuple<GeometryMode, GeometryMode> cellGeometryBehaviour)
    {
        if (cellGeometryBehaviour == null)
        {
            throw new ArgumentNullException(nameof(cellGeometryBehaviour));
        }

        return edgeIdx =>
        {
            var implicitGeometryMode =
                (mask & (1 << edgeIdx)) == 0 ? cellGeometryBehaviour.Item1 : cellGeometryBehaviour.Item2;
            //TODO fetch neighbor cell if edgeIdx ==1 and apply logic
            return implicitGeometryMode;
        };
    }

    /// <summary>
    /// Returns the sub triangles created by applying the transformation algorithm defined by the cellGeometryBehaviour argument.
    /// </summary>
    public static List<TriangleInfo> ProcessTriangle(
        HexTerrainCell cell,
        Vector3[] triangle,
        Func<int, GeometryMode> cellGeometryBehaviour,
        Dictionary<string, object>? additionalHints = null)
    {
        // We create a Triangulation instance based on the triangle
        // that will receive all the transformations from the algorithms
        var triangles = new Triangulation(
            triangle,
            cell.Verbose,
            cell.RunIntegrityChecks,
            additionalHints);

        for (var i = 0; i <= 2; i++)
        {
            var algo = cellGeometryBehaviour.Invoke(i);

            ProcessTriangleEdge(i,
                triangles,
                algo);
        }

        triangles.Debug("Triangle done", true);
        Console.WriteLine("Triangle Processing done");


        var tInfos = triangles.ToTriangleInfoList();
        return tInfos;
    }


    /// <summary>
    /// Edits the geometry of an edge of the cell's sub-triangle following a transformation defined by the provided algorithm
    /// </summary>
    private static void ProcessTriangleEdge(
        int edgeIdx,
        Triangulation triangles,
        GeometryMode algorithm)
    {
        switch (algorithm)
        {
            case GeometryMode.FlatHexagons:
                if (triangles._additionalHints == null || !triangles._additionalHints.ContainsKey(AverageHeightHint))
                {
                    throw new InvalidOperationException("Missing hints entry for computing the cell average height");
                }

                ProcessFlatHexagonEdge(edgeIdx, triangles);
                return;
            case GeometryMode.FlatTriangles:
                if (triangles._additionalHints == null
                    || !triangles._additionalHints.ContainsKey(AverageHeightHint)
                    || !triangles._additionalHints.ContainsKey(NextTriangleEdgeAvgHeight))
                {
                    throw new InvalidOperationException("Missing hints entry for" +
                                                        " computing the cell average" +
                                                        " height or the next triangle height");
                }

                ProcessFlatTriangleEdge(edgeIdx, triangles);
                return;
            case GeometryMode.SmoothLinear:
                ProcessLinearEdge(edgeIdx, triangles);
                return;
            case GeometryMode.Plateau:
                ProcessPlateauEdge(edgeIdx, triangles);
                return;
            case GeometryMode.Foothill:
                ProcessFoothillEdge(edgeIdx, triangles);
                return;
            case GeometryMode.BendingEdge:
                ProcessBendingEdge(edgeIdx, triangles);
                return;
            default:
                throw new NotSupportedException();
        }
    }

    private static void ProcessFlatHexagonEdge(int edgeIdx, Triangulation triangles)
    {
        var setLevel = (float)triangles._additionalHints![AverageHeightHint];
        //Move the triangle along the edge
        new DisplaceEdgeAlongYAxis(edgeIdx, setLevel, setLevel).Apply(triangles);

        if (edgeIdx != 1) return;
        //If outer edge (edgeIdx =1) add fans to match the previous height
        // The center of the cell is the first point of the triangulation as per
        // ComputeTriangleAndMask()
        AddEdgeFans(1, triangles, setLevel);
    }


    private static void ProcessFlatTriangleEdge(int edgeIdx, Triangulation triangles)
    {
        var setLevel = (triangles.SourceTriangle[1].Y + triangles.SourceTriangle[2].Y) / 2;
        var nextTriLevel = (float)triangles._additionalHints![NextTriangleEdgeAvgHeight];
        var nextTriVertex = (Vector3)triangles._additionalHints![NextTriangleVertexPos];
        //Move the triangle along the edge
        new DisplaceEdgeAlongYAxis(
            edgeIdx,
            setLevel,
            setLevel).Apply(triangles);
        switch (edgeIdx)
        {
            // If right inner edge (edgeIdx =0)  :NOOP
            case 0:
                break;
            //If outer edge (edgeIdx =1) add fans to match the previous height
            // The center of the cell is the first point of the triangulation as per
            // ComputeTriangleAndMask()
            case 1:
                AddEdgeFans(edgeIdx, triangles, setLevel);
                break;
            // If right inner edge (edgeIdx =2) add fans to the level of the next triangle , hinted in the dictionary
            case 2:
            {
                var pos1 = new Vector3(
                    triangles.SourceTriangle[edgeIdx].X,
                    nextTriLevel,
                    triangles.SourceTriangle[edgeIdx].Z);
                
                var pos2 = new Vector3(
                    triangles.SourceTriangle[mod(edgeIdx + 1, 3)].X,
                    nextTriLevel,
                    triangles.SourceTriangle[mod(edgeIdx + 1, 3)].Z);

                var newPoint = new AddTrianglesOnBorderEdge(edgeIdx, pos1).Apply(triangles);
                new AddTrianglesOnBorderEdge(edgeIdx, pos2).Apply(triangles);
                var hstart = Math.Abs(triangles.SourceTriangle[edgeIdx].Y - nextTriLevel);
                var hend = Math.Abs(nextTriVertex.Y - nextTriLevel);

                var pos3 = triangles.SourceTriangle[edgeIdx].Lerp(nextTriVertex, hstart / (hstart + hend));
                new AddTriangleFan((edgeIdx, newPoint), pos3).Apply(triangles);
            }
                break;
        }
    }

    private static void AddEdgeFans(int edgeIdx, Triangulation triangles, float setLevel)
    {
        if (triangles.Edges[edgeIdx].Count > 1)
        {
            throw new NotSupportedException("TODO : support Adding fans when the edge itself is already split");
        }

        //If both initial points are over or under the setLevel create 
        if (setLevel <= triangles.SourceTriangle[edgeIdx].Y &&
            setLevel <= triangles.SourceTriangle[mod(edgeIdx + 1, 3)].Y ||
            setLevel >= triangles.SourceTriangle[edgeIdx].Y &&
            setLevel >= triangles.SourceTriangle[mod(edgeIdx + 1, 3)].Y)
        {
            new AddTrianglesOnBorderEdge(edgeIdx, triangles.SourceTriangle[edgeIdx]).Apply(triangles);
            new AddTrianglesOnBorderEdge(edgeIdx, triangles.SourceTriangle[mod(edgeIdx + 1, 3)]).Apply(triangles);
        }
        else
        {
            var subEdge = triangles.SubEdges.ElementAt(triangles.Edges[edgeIdx].First!.Value).Key;
            var yStart = Math.Abs(triangles.SourceTriangle[edgeIdx].Y - setLevel);
            var yEnd = Math.Abs(triangles.SourceTriangle[mod(edgeIdx + 1, 3)].Y - setLevel);

            var newPoint =
                new SplitSubEdgeAction(
                    1,
                    subEdge.Item1,
                    subEdge.Item2,
                    yStart / (yStart + yEnd)).Apply(triangles);
            var pos1 = triangles.SourceTriangle[edgeIdx];
            var pos2 = triangles.SourceTriangle[mod(edgeIdx + 1, 3)];
            new AddTriangleFan((subEdge.Item1, newPoint), pos1).Apply(triangles);
            new AddTriangleFan((subEdge.Item2, newPoint), pos2).Apply(triangles);
        }
    }


    private static void ProcessLinearEdge(int _0, Triangulation _1)
    {
        //NOOP
    }

    private static void ProcessPlateauEdge(int edgeIdx, Triangulation triangles)
    {
        //TODO
    }

    private static void ProcessFoothillEdge(int edgeIdx, Triangulation triangles)
    {
        //TODO
    }

    private static void ProcessBendingEdge(int edgeIdx, Triangulation triangles)
    {
        //TODO
    }


    // <summary>
    // Returns the sub triangles created by applying the marching triangles' algorithm on triangles that have
    // at least one edge with a height delta superior to the algorithm threshold.
    // </summary>
    // <p>
    // This method is the core function of the spatial transformation
    // made by the Marching Triangles algorithm.
    //</p><p>
    // Since we are aware that at least one edge is to be processed, we can create all the required points.
    // </p><p>
    // This method processes a set of 3 non-equal points, therefore forming a triangle, into
    // a set of non-coplanar subtriangles.
    // In order to do so, we process each edge of the triangle, in order to determine the
    // positions of the points of the subtriangles.
    // </p><p>
    // For each edge that has a height (Δ=2*δ) over the algorithm threshold, the position of
    // the newly created points defined by a set of two parameters :
    //</p>
    // <ul>
    // <li> The "Ledge size" α , a real value in ]0,1[ representing how close the new points are
    // to the middle of the edge</li>
    // <li> The "Height bleed angle" θ, a real value defined in ]0, (π/2)- atan(α/δ)[</li>
    //</ul>
    // The (x,y) coordinates of the newly created points are defined by linear interpolation
    // between one of the edge vertices and its middle vertex, α being the lerp factor.
    //
    // <p>
    // The z coordinate is computed by adding a value f(α,θ) to the middle vertex z value, where :
    // </p>
    // <code>
    //  f(α,θ) = α * (δ - D * tan(θ))
    // </code>
    // and D is the length of the Edge when projected on the [xOz] plane
    // <p>
    // If the edge's height is below the threshold, the newly created point for the edge is simply the middle vertex. 
    // </p>
    // <p>
    // Once all the points have been determined, we trivially rebuild a set of triangles using the Triangles Fan
    // algorithm, since the polygon is convex. (https://en.wikipedia.org/wiki/Fan_triangulation)
    // </p>
    // <returns>An enumeration of triangles flagged on whether the triangles represent wall or not</returns>


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

    /// <summary>
    /// Processes the temporary data of the cell to generate the expected surface mesh.
    /// </summary>
    public Action CopyCellDataToPending(GdPluginHexTerrainChunk chunk)
    {
        // TODO actually copy the data => cf. chunk.gd ll. 314 -> 335 (dont forget the lock)
        return PlanCellProcessing(chunk);
    }


    public Action PlanCellProcessing(GdPluginHexTerrainChunk chunk)
    {
        var tempHexagonData = ExtractDataFromCell();

        return () => { DoMarchingTrianglesOnFullCell(tempHexagonData, chunk); };
    }

    public Dictionary<Vector2D, float> ExtractDataFromCell()
    {
        Dictionary<Vector2D, float> tempHexagonData = new();

        for (int i = 0; i < VertexCount; i++)
        {
            tempHexagonData.Add(
                _orientationSystem.GetVertex(_cellCoordsImplicit, i, 0),
                GetVertexData!(i));
        }

        return tempHexagonData;
    }

    private void DoMarchingTrianglesOnFullCell(Dictionary<Vector2D, float> tempHexagonData,
        GdPluginHexTerrainChunk chunk)
    {
        if (tempHexagonData.Count != 6)
        {
            throw new ArgumentException(
                "We expect 6 values for this code path. Aborting.");
        }

        List<TriangleInfo> triangles = ProcessCellGeometry(tempHexagonData, chunk.Underlying);
        DoMarchingTrianglesV2(triangles, chunk);
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