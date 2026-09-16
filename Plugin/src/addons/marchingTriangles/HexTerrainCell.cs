using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Text;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MathNet.Numerics;
using MathNet.Spatial.Euclidean;
using static MarchingTrianglesTerrain.addons.marchingTriangles.utils.EngineUtils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class HexTerrainCell
{
    public Tuple<GeometryMode, GeometryMode>? GeometryModesOverride;
    public float DefaultGeometryParam0;

    public float DefaultGeometryParam1;

    // TODO : Cell based ?
    // Temp constants
    public static double a = 0.5f;
    public static double theta = 0.1d;

    private float _tempDataHintForFlatTriangleCase;

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

    private void DoMarchingTrianglesV2(List<TriangleInfo> processedTriangles, GdPluginHexTerrainChunk chunk)
    {
        ProcessTrianglesIntoPoints(processedTriangles, chunk);
        chunk.ProcessPointsIntoMeshTriangles(this);
        TempDataArrays.Clear();
    }

    //private void DoMarchingTriangles(int i,
    //    Dictionary<Vector2D, float> dataArray, GdPluginHexTerrainChunk chunk)
    //{
    //    var (tri, Mask) = ComputeTriangleAndMask(i, dataArray, chunk.Underlying);
    //
    //    List<TriangleInfo> trianglesWithWallEdges = ProcessTriangle(tri, Mask);
    //    ProcessTrianglesIntoPoints(trianglesWithWallEdges, chunk);
    //    chunk.ProcessPointsIntoMeshTriangles(this);
    //    TempDataArrays.Clear();
    //}

    private (Vector3[] tri, int Mask) ComputeTriangleAndMask(int i, Dictionary<Vector2D, float> dataArray,
        HexagonalTerrainChunk chunk)
    {
        var center = CenterPosition;
        var posB = VertexPositionsInPlane[i];
        int index = (i + 1) % 6;
        var posC = VertexPositionsInPlane[index];

        var A = new Vector3((float)center.X, AverageHeight, (float)center.Y);
        var B = new Vector3((float)posB.X, dataArray[posB], (float)posB.Y);
        var C = new Vector3((float)posC.X, dataArray[posC], (float)posC.Y);

        Vector3[] tri = [A, B, C];
        _tempDataHintForFlatTriangleCase = dataArray[VertexPositionsInPlane[mod(i - 1, 6)]];

        int Mask = (Math.Abs(A.Y - C.Y) > chunk.MergeThreshold ? 1 : 0) * 4 +
                   (Math.Abs(B.Y - C.Y) > chunk.MergeThreshold ? 1 : 0) * 2 +
                   (Math.Abs(A.Y - B.Y) > chunk.MergeThreshold ? 1 : 0) * 1;
        return (tri, Mask);
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
            triangles.AddRange(ProcessTriangle(this, tri, computeGeometryMode));
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
        Func<int, GeometryMode> cellGeometryBehaviour)
    {
        // We create a Triangulation instance based on the triangle
        // that will receive all the transformations from the algorithms
        var triangles = new Triangulation(triangle);
        List<TriangulationEditAction> actions = [];
        
        for (var i = 0; i <= 2; i++)
        {
            ProcessTriangleEdge(
                cell,
                i,
                cellGeometryBehaviour.Invoke(i),
                triangles.sourceTriangle,
                triangle,
                out var collectedActions);
            actions.AddRange(collectedActions);
            //TODO apply all at once ?
            collectedActions.ForEach(action => { action.Apply(triangles); });
        }
        var tInfos = triangles.ToTriangleInfoList();
        return tInfos;
    }


    /// <summary>
    /// Edits the geometry of an edge of the cell's sub-triangle following a transformation defined by the provided algorithm
    /// </summary>
    private static void ProcessTriangleEdge(
        HexTerrainCell cell,
        int edgeIdx,
        GeometryMode algorithm,
        ImmutableArray<Vector3> triangleSource,
        Vector3[] triangle, // TODO : warn that content may have been mutated
        out List<TriangulationEditAction> collectedActions)
    {
        Console.WriteLine("Processing Edge "+edgeIdx  + " : "+algorithm);
        switch (algorithm)
        {
            case GeometryMode.FlatHexagons:
                ProcessFlatHexagonEdge(triangleSource, triangle, edgeIdx, out collectedActions);
                return;
            case GeometryMode.FlatTriangles:
                ProcessFlatTriangleEdge(triangleSource, cell, triangle, edgeIdx, out collectedActions);
                return;
            case GeometryMode.SmoothLinear:
                ProcessLinearEdge(triangleSource, triangle, edgeIdx, out collectedActions);
                return;
            case GeometryMode.Plateau:
                ProcessPlateauEdge(triangleSource, triangle, edgeIdx, out collectedActions);
                return;
            case GeometryMode.Foothill:
                ProcessFoothillEdge(triangleSource, triangle, edgeIdx, out collectedActions);
                return;
            case GeometryMode.BendingEdge:
                ProcessBendingEdge(triangleSource, triangle, edgeIdx, out collectedActions);
                return;
            default:
                throw new NotSupportedException();
        }
    }

    private static void ProcessBendingEdge(ImmutableArray<Vector3> triangleSource, Vector3[] triangle,
        int edgeIdx,
        out List<TriangulationEditAction> collectedActions)
    {
        throw new NotImplementedException();
    }

    private static void ProcessFoothillEdge(ImmutableArray<Vector3> triangleSource, Vector3[] triangle,
        int edgeIdx,
        out List<TriangulationEditAction> collectedActions)
    {
        throw new NotImplementedException();
    }

    private static void ProcessPlateauEdge(
        ImmutableArray<Vector3> triangleSource,
        Vector3[] triangle,
        int edgeIdx,
        out List<TriangulationEditAction> collectedActions)
    {
        throw new NotImplementedException();
    }

    private static void ProcessLinearEdge(
        ImmutableArray<Vector3> triangleSource,
        Vector3[] triangle,
        int edgeIdx,
        out List<TriangulationEditAction> collectedActions)
    {
        collectedActions = [];
        Vector3 start = triangle[edgeIdx];
        Vector3 end = triangle[mod(edgeIdx + 1, 3)];
        collectedActions.Add(new SplitEdgeAction(start, end, 1f / 2));
    }

    private static void ProcessFlatHexagonEdge(
        ImmutableArray<Vector3> triangleSource,
        Vector3[] triangle,
        int edgeIdx,
        out List<TriangulationEditAction> collectedActions)
    {
        collectedActions = [];


        if (edgeIdx != 1) // Not on exterior edge, for flat hexagons, there is no geometry to consider for other
            // edges since the triangles of the same cell will all have the same height value. 
        {
            return;
        }

        Vector3 start = triangle[edgeIdx];
        Vector3 end = triangle[mod(edgeIdx + 1, 3)];
        Vector3 opposite = triangle[mod(edgeIdx + 2, 3)];

        // the triangle is built in a way that the first point is the center of the Hexagonal cell.
        float heightValue = triangleSource[0].Y;

        if (Math.Abs(start.Y - end.Y) < 1e-5 && Math.Abs(start.Y - heightValue) < 1e-5)
        {
            //Fast branch exit nothing to do.
            return;
        }

        if (start.Y > heightValue && end.Y > heightValue || start.Y < heightValue && end.Y < heightValue)
        {
            
            
            // Both points are higher (or lower) than the triangle height value : we add a span of two triangles
            collectedActions.Add(new MovePointAlongYAxisAction(start, heightValue, triangle));
            collectedActions.Add(new MovePointAlongYAxisAction(end, heightValue, triangle));
            Vector3 newStartPos = new Vector3(start.X, heightValue, start.Z);
            Vector3 newEndPos = new Vector3(end.X, heightValue, end.Z);
            collectedActions.Add(new AddTriangleFan(opposite, newStartPos, start));
            collectedActions.Add(new AddTriangleFan(opposite, newEndPos, end));
        }
        else if (Math.Abs(start.Y - heightValue) < 1e-5)
        {
            //Start point is on the height value, we only add one triangle
            collectedActions.Add(new MovePointAlongYAxisAction(end, heightValue, triangle));
            Vector3 newEndPos = new Vector3(end.X, heightValue, end.Z);
            collectedActions.Add(new AddTriangleFan(start, newEndPos, end));
        }
        else if (Math.Abs(end.Y - heightValue) < 1e-5)
        {
            //End point is on the height value, we only add one triangle

            collectedActions.Add(new MovePointAlongYAxisAction(start, heightValue, triangle));
            Vector3 newStartPos = new Vector3(start.X, heightValue, start.Z);
            collectedActions.Add(new AddTriangleFan(start, newStartPos, end));
        }
        else
        {
            // One point is over the value, one is below
            CollectActionsForFlatExteriorEdge(triangle, collectedActions, end, heightValue, start);
        }
    }

    private static void CollectActionsForFlatExteriorEdge(Vector3[] triangle,
        List<TriangulationEditAction> collectedActions, Vector3 end,
        float heightValue, Vector3 start)
    {
        float delta_end = Math.Abs(end.Y - heightValue);
        float delta_start = Math.Abs(start.Y - heightValue);
        collectedActions.Add(new MovePointAlongYAxisAction(start, heightValue, triangle));
        collectedActions.Add(new MovePointAlongYAxisAction(end, heightValue, triangle));
        Vector3 newStartPos = new Vector3(start.X, heightValue, start.Z);
        Vector3 newEndPos = new Vector3(end.X, heightValue, end.Z);

        collectedActions.Add(new SplitEdgeAction(newStartPos, newEndPos,
            delta_start / (delta_end + delta_start)));

        Vector3 c = newStartPos.Lerp(newEndPos, delta_start / (delta_end + delta_start));

        collectedActions.Add(new AddTriangleFan(c, newStartPos, start));
        collectedActions.Add(new AddTriangleFan(c, newEndPos, end));
    }

    private static void ProcessFlatTriangleEdge(
        ImmutableArray<Vector3> triangleSource,
        HexTerrainCell cell,
        Vector3[] triangle,
        int edgeIdx,
        out List<TriangulationEditAction> collectedActions)
    {
        collectedActions = [];


        float previousTriangleVertexHeight;
        if (cell == null)
        {
            // Test only
            previousTriangleVertexHeight = 0;
        }
        else
        {
            previousTriangleVertexHeight = cell._tempDataHintForFlatTriangleCase;
        }

        Vector3 start = triangleSource[edgeIdx];
        Vector3 end = triangleSource[mod(edgeIdx + 1, 3)];
        Vector3 opposite = triangleSource[mod(edgeIdx + 2, 3)];

        // the triangle is built in a way that the first point is the center of the Hexagonal cell.
        float heightValue = (start.Y + end.Y) / 2f;

        if (Math.Abs(start.Y - end.Y) < 1e-5 && Math.Abs(start.Y - heightValue) < 1e-5)
        {
            //Fast exit
            return;
        }

        Vector3 c = start.Lerp(end, 1f / 2);
        if (c.Y - heightValue > 1e-5)
        {
            throw new Exception("Error in the algorithm");
        }

        //For a flattened triangle, the geometry to generate will be :
        // - On the "sides" of the triangle : the fans between each newly flattened triangles.
        // We have access to the cell's previous value thanks to the cell _tempDataHintForFlatTriangleCase field :
        // we can generate the fans without worrying about creating mesh singularities
        // Each triangle only generates one of its two sides fan so that we do not have duplicated geometry
        //
        // -On the "exterior" side of the triangle the behaviour corresponds to ProcessFlatHexagonEdge's case except
        // we already know that one point is over the value, one is below since it's the average
        if (edgeIdx == 0)
        {
            var previousEdgeAvgHeightValue = (previousTriangleVertexHeight + start.Y) / 2f;
            collectedActions.Add(new MovePointAlongYAxisAction(start, heightValue, triangle));
            collectedActions.Add(new MovePointAlongYAxisAction(end, heightValue, triangle));
            if (previousEdgeAvgHeightValue.AlmostEqual(heightValue)) // Both triangles have almost the same height
                // no need to create the triangle fans
            {
                return;
            }

            var editStart = new Vector3(start.X, heightValue, start.Z);
            var editStartPrevTri = new Vector3(start.X, previousEdgeAvgHeightValue, start.Z);

            var editEnd = new Vector3(editStart.X, heightValue, end.Z);
            var editEndPrevTri = new Vector3(editStart.X, previousEdgeAvgHeightValue, end.Z);

            collectedActions.Add(new AddTriangleFan(editStart, editStartPrevTri, editEnd));
            collectedActions.Add(new AddTriangleFan(editStartPrevTri, editEndPrevTri, editEnd));
        }
        else if (edgeIdx == 1)
        {
            CollectActionsForFlatExteriorEdge(triangle, collectedActions, end, heightValue, start);
        }
    }

    /// <summary>
    /// Returns the sub triangles created by applying the marching triangles' algorithm on triangles that have
    /// at least one edge with a height delta superior to the algorithm threshold.
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
    ///  f(α,θ) = α * (δ - D * tan(θ))
    /// </code>
    /// and D is the length of the Edge when projected on the [xOz] plane
    /// <p>
    /// If the edge's height is below the threshold, the newly created point for the edge is simply the middle vertex. 
    /// </p>
    /// <p>
    /// Once all the points have been determined, we trivially rebuild a set of triangles using the Triangles Fan
    /// algorithm, since the polygon is convex. (https://en.wikipedia.org/wiki/Fan_triangulation)
    /// </p>
    /// <returns>An enumeration of triangles flagged on whether the triangles represent wall or not</returns>
    //public static List<TriangleInfo> ProcessTriangle(
    //    Vector3[] triangle,
    //    int mask)
    //{
    //    // //Debug statement
    //    // Console.WriteLine("Processing triangle [mask = "+mask+"]");
    //
    //    Vector3[] tri = triangle;
    //    Dictionary<int, Tuple<Vector3, Vector3?>> newPoints = new Dictionary<int, Tuple<Vector3, Vector3?>>();
    //    List<Vector3> flatNewPoints = new List<Vector3>();
    //    List<TriangleInfo> res = new List<TriangleInfo>();
    //    var comparer = new V3Comp();
    //    // We loop over the edges of the initial triangle.
    //    // the Edge #i => [P(i),P(i+1)]
    //    for (int i = 0; i <= 2; i++)
    //    {
    //        Vector3 start = tri[i];
    //        Vector3 end = tri[mod(i + 1, 3)];
    //        var midpoint = (start + end) / 2;
    //        if ((mask & (1 << i)) == 0)
    //        {
    //            // The edge is not over the threshold, we add the middle point
    //            newPoints.Add(i, new Tuple<Vector3, Vector3?>(midpoint, null));
    //            flatNewPoints.Add(midpoint);
    //        }
    //        else
    //        {
    //            var delta = (start.Y - end.Y) / 2;
    //            var alpha = a;
    //            var newPoint = new Vector3(
    //                start.X + (float)alpha * (midpoint - start).X,
    //                start.Y + (float)alpha * (midpoint - start).Y,
    //                start.Z + (float)alpha * (midpoint - start).Z);
    //            // Recompute Y value according to the formula
    //            double D = Math.Sqrt((start.X - midpoint.X) * (start.X - midpoint.X) +
    //                                 (start.Z - midpoint.Z) * (start.Z - midpoint.Z));
    //            var v = (float)(alpha * (delta - D * comparer.Compare(start, midpoint) * Math.Tan(theta)));
    //            newPoint.Y += v;
    //            // //Debug statement
    //            // Console.WriteLine("Edge#{4} = [{5}] : f({0},{1},{2}) = {3}", delta, alpha, theta, v, i, newPoint);
    //            // Console.WriteLine("Edge#{4} = [{5}] : f({0},{1},{2}) = {3}", delta, alpha, theta, -v, i, 2 * midpoint - newPoint);
    //
    //            newPoints.Add(i, new Tuple<Vector3, Vector3?>(
    //                newPoint,
    //                2 * midpoint - newPoint));
    //            flatNewPoints.Add(newPoint);
    //            flatNewPoints.Add(2 * midpoint - newPoint);
    //        }
    //    }
    //    // From these new points, and the ordered list of those,
    //    // It is possible to recreate a list of ordered triangles that we will return
    //
    //    // Step 1 :
    //    // Find the 3 triangles containing one pre-existing vertex :
    //    // The other points forming the triangle are the computed new points of the previous point,
    //    // the second if there are two, the first otherwise.
    //    //
    //    // TODO : clarify what is the condition for wall = true
    //    for (int i = 0; i <= 2; i++)
    //    {
    //        var tInfo = new TriangleInfo
    //        {
    //            IsWall = false,
    //            Points =
    //            {
    //                [0] = newPoints[mod(i - 1, 3)].Item2.HasValue
    //                    ? newPoints[mod(i - 1, 3)].Item2.Value
    //                    : newPoints[mod(i - 1, 3)].Item1,
    //                [1] = tri[i],
    //                [2] = newPoints[i].Item1
    //            },
    //            edgeBorderFlags =
    //            {
    //                [0] = true,
    //                [1] = true,
    //                [2] = false
    //            }
    //        };
    //        res.Add(tInfo);
    //    }
    //
    //    // Part 2 :
    //    // Get the other triangles as part of a triangle fan originated for the first
    //    // new point of the list of new points;
    //    for (int i = 0; i < flatNewPoints.Count - 2; i++)
    //    {
    //        //Debug
    //        var tInfo = new TriangleInfo
    //        {
    //            IsWall = true,
    //            Points =
    //            {
    //                //
    //                [0] = flatNewPoints[0],
    //                [1] = flatNewPoints[i + 1],
    //                [2] = flatNewPoints[i + 2]
    //            },
    //        };
    //        switch (mask, i) // Easier to bruteforce :P
    //        {
    //            case (0, 0):
    //                tInfo.edgeBorderFlags = [false, false, false];
    //                break;
    //
    //            case (1, 0):
    //                tInfo.edgeBorderFlags = [true, false, false];
    //                break;
    //            case (1, 1):
    //                tInfo.edgeBorderFlags = [false, false, false];
    //                break;
    //
    //            case (2, 0):
    //                tInfo.edgeBorderFlags = [false, true, false];
    //                break;
    //            case (2, 1):
    //                tInfo.edgeBorderFlags = [false, false, false];
    //                break;
    //
    //            case (3, 0):
    //                tInfo.edgeBorderFlags = [true, false, false];
    //                break;
    //            case (3, 1):
    //                tInfo.edgeBorderFlags = [false, true, false];
    //                break;
    //            case (3, 2):
    //                tInfo.edgeBorderFlags = [false, false, false];
    //                break;
    //
    //            case (4, 0):
    //                tInfo.edgeBorderFlags = [false, false, false];
    //                break;
    //            case (4, 1):
    //                tInfo.edgeBorderFlags = [false, true, false];
    //                break;
    //
    //            case (5, 0):
    //                tInfo.edgeBorderFlags = [true, false, false];
    //                break;
    //            case (5, 1):
    //                tInfo.edgeBorderFlags = [false, false, false];
    //                break;
    //            case (5, 2):
    //                tInfo.edgeBorderFlags = [false, true, false];
    //                break;
    //
    //            case (6, 0):
    //                tInfo.edgeBorderFlags = [false, true, false];
    //                break;
    //            case (6, 1):
    //                tInfo.edgeBorderFlags = [false, false, false];
    //                break;
    //            case (6, 2):
    //                tInfo.edgeBorderFlags = [false, true, false];
    //                break;
    //
    //            case (7, 0):
    //                tInfo.edgeBorderFlags = [true, false, false];
    //                break;
    //            case (7, 1):
    //                tInfo.edgeBorderFlags = [false, true, false];
    //                break;
    //            case (7, 2):
    //                tInfo.edgeBorderFlags = [false, false, false];
    //                break;
    //            case (7, 3):
    //                tInfo.edgeBorderFlags = [false, true, false];
    //                break;
    //            default:
    //                throw new Exception("Illegal state");
    //        }
    //
    //        res.Add(tInfo);
    //    }
    //
    //    return res;
    //}
    //
    public class TriangleInfo
    {
        public bool IsWall { get; set; } = false;
        public Vector3[] Points { get; set; } = new Vector3[3];
        public bool[] edgeBorderFlags { get; set; } = new bool[3];

        public List<Tuple<Vector3, Vector3>> GetBorderEdges()
        {
            var res = new List<Tuple<Vector3, Vector3>>();

            for (int i = 0; i <= 2; i++)
            {
                if (edgeBorderFlags[i])
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
            throw new Exception("Illegal Argument");
        var z = Vector3.Up.Dot((tri[1] - tri[0]).Cross(tri[2] - tri[0])) / 2;
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
                GetVertexData(i));
        }

        return tempHexagonData;
    }

    private void DoMarchingTrianglesOnFullCell(Dictionary<Vector2D, float> tempHexagonData,
        GdPluginHexTerrainChunk chunk)
    {
        if (tempHexagonData.Count != 6)
        {
            throw new ArgumentException(
                "We expect 6 values for this codepath. Aborting.");
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

    public readonly Vector2I cellCoord = cellCoord;

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

class UnorderedTupleComparer : IComparer<(int, int)>, IEqualityComparer<(int, int)>
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