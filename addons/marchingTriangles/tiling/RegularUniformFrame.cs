using Godot;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Spatial.Euclidean;

namespace Localproto.addons.marchingTriangles.tiling;

/// <summary>
/// A regular frame is an affine frame (i.e. Origin + Vectorial basis)
/// based on a regular (only regular polygons) uniform (vertex transitive) tiling of the
/// Euclidean plane, where the origin of the frame is the centroid of the tiling polygon.
/// </summary>
public interface RegularUniformFrame : RegularUniformTiling, ICloneable
{
    /// <summary>
    /// The list of the cartesian coordinates of the polygon centroids of the Origin Cell
    /// (i.e.the pattern numbered [0,0])
    /// </summary>
    Vector2D[] UnscaledOriginCellCentroidPositions { get; }

    public Matrix<double> Transform { get; }
    public Matrix<double> TransformSquare { get; }

    public Vector<double> TransformOffset { get; set; }
    public Vector2D CartesianOrigin => Vector2D.OfVector(Transform.Column(2));

    // TODO : make it a private field at some point
    public Matrix<double> TransformInverse { get; set; }
    public Matrix<double> TransformInverseSquare { get; }
    public Vector<double> TransformInverseOffset { get; set; }


    public int GetPolygonIndexFromCartesian(Vector2D pos, Vector2I cell) => 0;

    public int GetPolygonIndexFromLocal(Vector2D pos) => 0;

    public Vector2I GetCell(Vector2D pos);

    RegularUniformFrame GetDual();

    public List<Vector2D> GetGentroids(Vector2I idx)
    {
        var res = new List<Vector2D>();
        for (int i = 0; i < PolygonCount; i++)
        {
            res.Add(GetCellCentroid(idx, i));
        }

        return res;
    }

    Vector2D GetCellCentroid(Vector3I idx) => GetCellCentroid(new Vector2I(idx.X, idx.Y), idx.Z);


    /// <summary>
    /// Returns the cartesian coordinates a given centroid for a given cell index.
    /// </summary>
    /// <param name="idx">the cell index</param>
    /// <param name="polygonIdx">the polygon</param>
    /// <returns></returns>
    Vector2D GetCellCentroid(Vector2I idx, int polygonIdx)
    {
        //Do not explicitly apply the tiling's scaling since it's already taken into account when applying Transform
        var v1 = Transform.Column(0);
        var v2 = Transform.Column(1);
        double x = v1[0] * idx.X + v2[0] * idx.Y;
        double y = v1[1] * idx.X + v2[1] * idx.Y;
        x += UnscaledOriginCellCentroidPositions[polygonIdx].X;
        y += UnscaledOriginCellCentroidPositions[polygonIdx].Y;
        return new Vector2D(x, y);
    }

    Vector2D GetVertex(Vector2I idx, int vertexIdx, int polygonIdx)
    {
        var centroid = GetCellCentroid(idx, polygonIdx);
        var xDelta = Math.Cos(GetVertexAngleInRad(vertexIdx, polygonIdx)) * TilingScale;
        var yDelta = Math.Sin(GetVertexAngleInRad(vertexIdx, polygonIdx)) * TilingScale;
        return centroid + new Vector2D(xDelta, yDelta);
    }


    public Func<Vector2D, Vector2D> CartesianToLocal => d =>
        Vector2D.OfVector(
            TransformInverseSquare *
            (d.ToVector() - TransformOffset)
        );

    public Func<Vector2D, Vector2D> LocalToCartesian =>
        d => Vector2D.OfVector(TransformSquare * d.ToVector())
             + Vector2D.OfVector(TransformOffset);

    List<Vector2D> GetVertices(Vector3I cellIdx)
    {
        List<Vector2D> vertices = [];
        var cell2D = new Vector2I(cellIdx.X, cellIdx.Y);
        for (int j = 0; j < GetPolygonVertexCount(cellIdx.Z); j++)
        {
            vertices.Add(GetVertex(cell2D, j, cellIdx.Z));
        }

        return vertices;
    }

    
    //TODO : don't go through Cartesian coords
    Vector3I GetVertexIndexInDualSpace(Vector3I cellIdx, RegularUniformFrame dualFrame, int vertexIdx)
    {
        var centroid = GetCellCentroid(cellIdx);
        var xDelta = Math.Cos(GetVertexAngleInRad(vertexIdx, cellIdx.Z)) * TilingScale;
        var yDelta = Math.Sin(GetVertexAngleInRad(vertexIdx, cellIdx.Z)) * TilingScale;
        var pos2D = centroid + new Vector2D(xDelta, yDelta);

        Vector2I cell = dualFrame.GetCell(pos2D);
        Vector3I fullCell = new Vector3I(cell.X, cell.Y, dualFrame.GetPolygonIndexFromCartesian(pos2D,cell));
        return fullCell;
    }

    List<Tuple<Vector2D, int>> GetVertexPositions(Vector2I cellIdx)
    {
        List<Tuple<Vector2D, int>> vertices = [];
        for (int i = 0; i < PolygonCount; i++)
        {
            for (int j = 0; j < GetPolygonVertexCount(i); j++)
            {
                vertices.Add(new Tuple<Vector2D, int>(GetVertex(cellIdx, j, i), i));
            }
        }

        return vertices;
    }

    /// <summary>
    /// Computes the area of a tile of this frame.
    /// </summary>
    /// <returns></returns>
    public double GetTileArea()
    {
        double area = 0;
        for (int i = 0; i < PolygonCount; i++)
        {
            int n = GetPolygonVertexCount(i);
            for (int j = 0; j < n; j++)
            {
                var p = GetVertex(Vector2I.Zero, j, i);
                var p_next = GetVertex(Vector2I.Zero, (j + 1) % n, i);
                area += (p.X * p_next.Y - p.Y * p_next.X) / 2;
            }
        }

        return area;
    }

    /// <summary>
    /// Returns a similar affine frame with its cartesian origin coordinates translated by the provided vector
    /// </summary>
    /// <param name="cartesianOffset">the offset value, expressed in the cartesian frame.</param>
    /// <returns></returns>
    RegularUniformFrame OffsetBy(Vector2D cartesianOffset)
    {
        RegularUniformFrame newFrame = Clone() as RegularUniformFrame;
        var newOrigin = (Vector2D.OfVector(newFrame.TransformOffset) + cartesianOffset).ToVector();
        newFrame.TransformOffset = newOrigin;
        newFrame.TransformInverseOffset = newOrigin;

        for (var i = 0; i < UnscaledOriginCellCentroidPositions.Length; i++)
        {
            newFrame.UnscaledOriginCellCentroidPositions[i] += cartesianOffset;
        }

        return newFrame;
    }
}