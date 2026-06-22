using System.Text;
using Godot;
using Localproto.addons.marchingTriangles.tiling;
using MathNet.Spatial.Euclidean;

namespace Localproto.addons.marchingTriangles;

public class HexTerrainCell
{
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

        List<Vector3[]> newTriangles = SplitTriangle(tri);
        List<Tuple<Vector3[], bool>> trianglesWithWallEdges = ProcessWallEdges(newTriangles, Mask, chunk);
        ProcessTrianglesIntoPoints(trianglesWithWallEdges, chunk);
        chunk.ProcessPointsIntoMeshTriangles(trianglesWithWallEdges, this);
        TempDataArrays.Clear();
    }

    private void ProcessTrianglesIntoPoints(List<Tuple<Vector3[], bool>> trianglesWithWallEdges,
        GdPluginHexTerrainChunk chunk)
    {
        foreach (var trianglesWithWallEdge in trianglesWithWallEdges)
        {
            if (trianglesWithWallEdge.Item2)
            {
                FloorMode = false;
            }
            else
            {
                FloorMode = true;
            }

            foreach (var point in trianglesWithWallEdge.Item1)
            {
                chunk.AddPoint(point, Vector2.Zero, this);
            }
        }
    }

    private List<Tuple<Vector3[], bool>> ProcessWallEdges(
        List<Vector3[]> newTriangles,
        int mask,
        GdPluginHexTerrainChunk chunk)
    {
        if (newTriangles.Count != 4 || newTriangles.Any(triData => triData.Length != 3))
        {
            throw new ArgumentException("The provided list of triangles is malformed.", nameof(newTriangles));
        }

        var result = new List<Tuple<Vector3[], bool>>();

        switch (mask)
        {
            case 0:
                result.AddRange(newTriangles.Select(triangle => new Tuple<Vector3[], bool>(triangle, false)));
                break;
            case 7:
                result.AddRange(newTriangles.Select(triangle => new Tuple<Vector3[], bool>(triangle, true)));
                break;
            case 1:
            case 2:
            case 4:
                result.AddRange(newTriangles.Select(triangle => new Tuple<Vector3[], bool>(triangle, false)));
                GD.Print("Single Edge over threshold");
                break;
            case 3:
            case 5:
            case 6:
                result.AddRange(newTriangles.Select(triangle => new Tuple<Vector3[], bool>(triangle, false)));
                //GD.Print("Double Edges over threshold");
                break;
        }

        return result;
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