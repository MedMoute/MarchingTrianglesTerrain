using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MathNet.Spatial.Euclidean;
using static MarchingTrianglesTerrain.addons.marchingTriangles.utils.EngineUtils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class HexTerrainCell
{
    // Temp constants
    public static float a = 0.33f;
    public static double theta = 0.2d;

    /// <summary>
    /// The coordinates of the current cell in the paren chunk's hex frame.
    /// Since the Hexagonal tiling only have one cell, the Z component can be skipped 
    /// </summary>
    public Vector2I CellCoordsImplicit
    {
        get => _cellCoordsImplicit;
        set
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
    public Func<int, float> GetVertexData;

    public bool FloorMode { get; private set; }

    public float AverageHeight
    {
        //TODO save value in cache
        get
        {
            float sum = 0;
            for (int i = 0; i < VertexCount; i++)
            {
                sum += GetVertexData(i);
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
        DualCellsMapping = new();
        VertexPositionsInPlane = new();
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
            throw new Exception(String.Format(
                "Something is wrong, a hexagonal cell should always be affected by 6 dual triangles, got {0}instead ",
                DualCellsMapping.Count));
        }
    }


    public void SetDataFetchingFunction(
        Vector2I dimensions2D,
        Func<Vector2I, TriangleGrid> dataProviderProvider,
        Func<Vector2I, bool> doesNeighboringChunkExist)
    {
        /// TODO Memoize
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
    }

    public static Vector2I GetChunkOffsetForDualCell(Vector2I chunkDimension, Vector3I dualIndex)
    {
        var offset = new Vector2I(
            Mathf.FloorToInt(dualIndex.X / (float)chunkDimension.X),
            Mathf.FloorToInt(dualIndex.Y / (float)chunkDimension.Y));
        return offset;
    }

    //TODO : use flag ?
    public bool IsReady()
    {
        if (_visits.Count != VertexCount)
        {
            return false;
        }

        return true;
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


    private void DoMarchingTriangles(int i,
        Dictionary<Vector2D, float> dataArray, GdPluginHexTerrainChunk chunk)
    {
        var center = CenterPosition;
        var posB = VertexPositionsInPlane[i];
        int index = (i + 1) % 6;
        var posC = VertexPositionsInPlane[index];

        var A = new Vector3((float)center.X, AverageHeight, (float)center.Y);
        var B = new Vector3((float)posB.X, dataArray[posB], (float)posB.Y);
        var C = new Vector3((float)posC.X, dataArray[posC], (float)posC.Y);

        Vector3[] tri = [A, B, C];

        int Mask = (Math.Abs(A.Y - C.Y) > chunk.Underlying.MergeThreshold ? 1 : 0) * 4 +
                   (Math.Abs(B.Y - C.Y) > chunk.Underlying.MergeThreshold ? 1 : 0) * 2 +
                   (Math.Abs(A.Y - B.Y) > chunk.Underlying.MergeThreshold ? 1 : 0) * 1;

        List<TriangleInfo> trianglesWithWallEdges = ProcessTriangle(tri, Mask);
        ProcessTrianglesIntoPoints(trianglesWithWallEdges, chunk);
        chunk.ProcessPointsIntoMeshTriangles(this);
        TempDataArrays.Clear();
    }

    private void ProcessTrianglesIntoPoints(List<TriangleInfo> trianglesWithWallEdges,
        GdPluginHexTerrainChunk chunk)
    {
        foreach (var trianglesWithWallEdge in trianglesWithWallEdges)
        {
            if (trianglesWithWallEdge.IsWall)
            {
                FloorMode = false;
            }
            else
            {
                FloorMode = true;
            }

            foreach (var point in trianglesWithWallEdge.Points)
            {
                chunk.AddPoint(point, Vector2.Zero, this);
            }
        }
    }

    /// <summary>
    /// Returns the sub triangles created by applying the marching triangles' algorithm on triangles that have
    /// at least one edge with a height delta superion to the algorithm threshold.
    /// </summary>
    /// <p>
    /// This method is the core function of the spatial transformation
    /// made by the Marching Triangles algorithm.
    ///</p><p>
    /// Since we are aware that at least one edge is to be processed, we can create all the required points.
    /// </p><p>
    /// This method processes a set of 3 non-equal points, therefore forming a triangle, into
    /// a set of non-coplanar subtriangles.
    /// In order to do so, we process each edge of the triangle, in order to determine the
    /// positions of the points of the subtriangles.
    /// </p><p>
    /// For each edge that has a height (Δ=2*δ) over the algorithm threshold, the position of
    /// the newly created points defined by a set of two parameters :
    ///</p>
    /// <ul>
    /// <li> The "Ledge size" α , a real value in ]0,1[ representing how close the new points are
    /// to the middle of the edge</li>
    /// <li> The "Height bleed angle" θ, a real value defined in ]0, (π/2)- atan(α/δ)[</li>
    ///</ul>
    /// The (x,y) coordinates of the newly created points are defined by linear interpolation
    /// between one of the edge vertices and its middle vertex, α being the lerp factor.
    ///
    /// <p>
    /// The z coordinate is computed by adding a value f(α,θ) to the middle vertex z value, where :
    /// </p>
    /// <code>
    ///                 (1-α-α²)*tan(θ)
    ///  f(α,θ) = δ * -------------------
    ///               (1-2α)*tan(θ)+δ(1-α)
    /// </code>
    /// <p>
    /// If the edge's height is below the threshold, the newly created point for the edge is simply the middle vertex. 
    /// </p>
    /// <p>
    /// Once all the points have been determined, we trivially rebuild a set of triangles using the Triangles Fan
    /// algorithm, since the polygon is convex. (https://en.wikipedia.org/wiki/Fan_triangulation)
    /// </p>
    /// <returns>An enumeration of triangles flagged on whether the triangles represent wall or not</returns>
    public static List<TriangleInfo> ProcessTriangle(
        Vector3[] triangle,
        int mask)
    {
        //
        Vector3[] tri = triangle;
        Dictionary<int, Tuple<Vector3, Vector3?>> newPoints = new Dictionary<int, Tuple<Vector3, Vector3?>>();
        List<Vector3> flatNewPoints = new List<Vector3>();
        List<TriangleInfo> res = new List<TriangleInfo>();
        // We loop over the edges of the initial triangle.
        // the Edge #i => [P(i),P(i+1)]
        for (int i = 0; i <= 2; i++)
        {
            Vector3 start = tri[i];
            Vector3 end = tri[mod(i + 1, 3)];
            var midpoint = (start + end) / 2;
            if ((mask & (1 << i)) == 0)
            {
                // The edge is not over the threshold, we add the middle point
                newPoints.Add(i, new Tuple<Vector3, Vector3?>(midpoint, null));
                flatNewPoints.Add(midpoint);
            }
            else
            {
                var delta = (start.Y - end.Y) / 2;
                var alpha = a;
                var newPoint = start + alpha * (midpoint - start);
                // Recompute z value according to the formula
                float D = MathF.Sqrt((start.X - midpoint.X) * (start.X - midpoint.X) +
                                     (start.Z - midpoint.Z) * (start.Z - midpoint.Z));
                var v = (float)(alpha * (delta - D * Math.Tan(theta)));
                newPoint.Y += -Math.Sign(delta)*v;
                Console.WriteLine("{4} = [{5},{6}] : f({0},{1},{2}) = {3}", delta, alpha, theta, v, i, start, end);

                newPoints.Add(i, new Tuple<Vector3, Vector3?>(
                    newPoint,
                    2 * midpoint - newPoint));
                flatNewPoints.Add(newPoint);
                flatNewPoints.Add(2 * midpoint - newPoint);
            }
        }
        // From these new points, and the ordered list of those,
        // It is possible to recreate a list of ordered triangles that we will return

        // Step 1 :
        // Find the 3 triangles containing one pre-existing vertex :
        // The other points forming the triangle are the computed new points of the previous point,
        // the second if there are two, the first otherwise.
        //
        // TODO : clarify what is the condition for wall = true
        for (int i = 0; i <= 2; i++)
        {
            var tInfo = new TriangleInfo
            {
                IsWall = false,
                SeedPointIdx = i,
                SeedPoint = tri[i],
                Points =
                {
                    [0] = newPoints[mod(i - 1, 3)].Item2.HasValue
                        ? newPoints[mod(i - 1, 3)].Item2.Value
                        : newPoints[mod(i - 1, 3)].Item1,
                    [1] = tri[i],
                    [2] = newPoints[i].Item1
                },
                edgeBorderFlags =
                {
                    [0] = true,
                    [1] = true,
                    [2] = false
                }
            };
            res.Add(tInfo);
        }

        // Part 2 :
        // Get the other triangles as part of a triangle fan originated for the first
        // new point of the list of new points;
        for (int i = 0; i < flatNewPoints.Count - 2; i++)
        {
            //Debug
            var tInfo = new TriangleInfo
            {
                IsWall = true,
                SeedPointIdx = null,
                SeedPoint = flatNewPoints[0],
                Points =
                {
                    //
                    [0] = flatNewPoints[0],
                    [1] = flatNewPoints[i + 1],
                    [2] = flatNewPoints[i + 2]
                },
            };
            switch (mask, i) // Easier to bruteforce :3
            {
                case (0, 0):
                    tInfo.edgeBorderFlags = [false, false, false];
                    break;

                case (1, 0):
                    tInfo.edgeBorderFlags = [true, false, false];
                    break;
                case (1, 1):
                    tInfo.edgeBorderFlags = [false, false, false];
                    break;

                case (2, 0):
                    tInfo.edgeBorderFlags = [false, true, false];
                    break;
                case (2, 1):
                    tInfo.edgeBorderFlags = [false, false, false];
                    break;

                case (3, 0):
                    tInfo.edgeBorderFlags = [true, false, false];
                    break;
                case (3, 1):
                    tInfo.edgeBorderFlags = [false, true, false];
                    break;
                case (3, 2):
                    tInfo.edgeBorderFlags = [false, false, false];
                    break;

                case (4, 0):
                    tInfo.edgeBorderFlags = [false, false, false];
                    break;
                case (4, 1):
                    tInfo.edgeBorderFlags = [false, true, false];
                    break;

                case (5, 0):
                    tInfo.edgeBorderFlags = [true, false, false];
                    break;
                case (5, 1):
                    tInfo.edgeBorderFlags = [false, false, false];
                    break;
                case (5, 2):
                    tInfo.edgeBorderFlags = [false, true, false];
                    break;

                case (6, 0):
                    tInfo.edgeBorderFlags = [false, true, false];
                    break;
                case (6, 1):
                    tInfo.edgeBorderFlags = [false, false, false];
                    break;
                case (6, 2):
                    tInfo.edgeBorderFlags = [false, true, false];
                    break;

                case (7, 0):
                    tInfo.edgeBorderFlags = [true, false, false];
                    break;
                case (7, 1):
                    tInfo.edgeBorderFlags = [false, true, false];
                    break;
                case (7, 2):
                    tInfo.edgeBorderFlags = [false, false, false];
                    break;
                case (7, 3):
                    tInfo.edgeBorderFlags = [false, true, false];
                    break;
                default:
                    throw new Exception("Illegal state");
            }

            res.Add(tInfo);
        }

        return res;
    }


    public class TriangleInfo
    {
        public bool IsWall { get; set; } = false;
        public Vector3 SeedPoint { get; set; } = Vector3.One * float.MaxValue;
        public int? SeedPointIdx { get; set; } = null;

        public Vector3[] Points { get; set; } = new Vector3[3];
        public bool[] edgeBorderFlags { get; set; } = new bool[3];

        public List<Tuple<Vector3, Vector3>> GetBorderEdges()
        {
            var res = new List<Tuple<Vector3, Vector3>>();

            for (int i = 0; i <= 2; i++)
            {
                if (edgeBorderFlags[i])
                {
                    res.Add(new Tuple<Vector3, Vector3>(Points[i], Points[mod(i + 1, 3)]));
                }
            }

            return res;
        }

        public List<Tuple<Vector3, Vector3>> GetEdges()
        {
            var res = new List<Tuple<Vector3, Vector3>>();

            for (int i = 0; i <= 2; i++)
            {
                res.Add(new Tuple<Vector3, Vector3>(Points[i], Points[mod(i + 1, 3)]));
            }

            return res;
        }


    }
    public static double GetSignedArea(Vector3[] tri)
    {
        if (tri.Length !=3)
            throw new Exception("Illegal Argument");
        var z =  Vector3.Up.Dot((tri[1] - tri[0]).Cross(tri[2] - tri[0])) / 2;
        Console.WriteLine(z);
        return z;
    }
    // TODO : buffer recycling
    /// <summary>
    /// Splits a triangle into 4 sub-triangles that will have the same area.
    /// We assume the triangle is already ordered
    /// </summary>
    /// <param name="tri"></param>
    private List<Vector3[]> SplitTriangle(Vector3[] tri)
    {
        var A = tri[0];
        var B = tri[1];
        var C = tri[2];

        var ABMiddle = (A + B) / 2;
        var ACMiddle = (A + C) / 2;
        var BCMiddle = (B + C) / 2;

        return
        [
            [A, ABMiddle, ACMiddle],
            [B, BCMiddle, ABMiddle],
            [C, ACMiddle, BCMiddle],
            [ABMiddle, BCMiddle, ACMiddle]
        ];
    }

    /// <summary>
    /// Processes the temporary data of the cell to generate the expected surface mesh.
    /// </summary>
    /// <param name="surfaceTool"></param>
    public Action CopyCellDataToPending(GdPluginHexTerrainChunk chunk)
    {
        // TODO actually copy the data => cf. chunk.gd ll. 314 -> 335 (dont forget the lock)
        return PlanCellProcessing(chunk);
    }


    public Action PlanCellProcessing(GdPluginHexTerrainChunk chunk)
    {
        Dictionary<Vector2D, float> tempHexagonData = new();

        for (int i = 0; i < VertexCount; i++)
        {
            tempHexagonData.Add(
                _orientationSystem.GetVertex(_cellCoordsImplicit, i, 0),
                GetVertexData(i));
        }

        return () => { DoMarchingTrianglesOnFullCell(tempHexagonData, chunk); };
    }

    private void DoMarchingTrianglesOnFullCell(Dictionary<Vector2D, float> tempHexagonData,
        GdPluginHexTerrainChunk chunk)
    {
        if (tempHexagonData.Count != 6)
        {
            throw new ArgumentException(
                "We expect 6 values for this codepath. Aborting.");
        }


        for (var i = 0; i < 6; i++)
        {
            DoMarchingTriangles(i, tempHexagonData, chunk);
        }
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
    public readonly List<Color> MatBlend = new();
    public readonly List<bool> Floor = new();

    public readonly Vector2I cellCoord = cellCoord;

    public void EnsureProcessable()
    {
        if (Pt.Count % 3 != 0
            || Pt.Count != Uv.Count
            || Pt.Count != Uv2.Count
            || Pt.Count != Color0.Count
            || Pt.Count != Color1.Count
            || Pt.Count != MatBlend.Count
            || Pt.Count != Floor.Count)
        {
            throw new ArgumentOutOfRangeException("The cell data array is wrongly shaped.");
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
        MatBlend.Clear();
        Floor.Clear();
    }
}