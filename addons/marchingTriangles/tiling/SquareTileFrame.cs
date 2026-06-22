using Godot;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Spatial.Euclidean;
using MathNet.Spatial.Units;

namespace Localproto.addons.marchingTriangles.tiling;

/// <summary>
/// The Square Tile pattern is a Regular Uniform pattern where the pattern tile
/// is a single square.
/// </summary>
public class SquareTileFrame : RegularUniformFrame
{
    /// <summary>
    /// Constructive constructor using a provided origin, assumed to be the center of the
    /// centroid of the lattice pattern, and a seed point assumed to be the first vertex
    /// of the cell [0,0] of the frame.
    /// </summary>
    public SquareTileFrame(Vector2D origin, Vector2D seedPoint)
    {
        Vector2D halfDiag = seedPoint - origin;
        if (halfDiag.Length < 1E-5)
        {
            throw new ArgumentException(
                "Cannot create such a small grid , the origgin and seed vertices are too close");
        }

        Vector2D halfDiag2 = halfDiag.Rotate(Angle.FromRadians(Math.PI / 2));

        var v1 = halfDiag + halfDiag2;
        var v2 = halfDiag2 - halfDiag;
        var col1 = v1.ToVector().AsArray();
        var col2 = v2.ToVector().AsArray();
        var col3 = origin.ToVector().AsArray();
        Transform = Matrix.Build.DenseOfColumnArrays(col1, col2, col3);
        TilingScale = halfDiag.Length;

        var inverse = TransformSquare.Inverse();
        TransformInverse = inverse.Append(TransformOffset.ToColumnMatrix());

        UnscaledOriginCellCentroidPositions = [origin];
        _offsetAngle = Math.Atan2(v1.Y, v1.X);
    }

    public int PolygonCount => 1;
    public double TilingScale { get; }
    public Angle TilingAngle => Angle.FromRadians(_offsetAngle);
    private readonly double _offsetAngle;
    public Vector2D[] UnscaledOriginCellCentroidPositions { get; }
    public Matrix<double> Transform
    {
        get => _transform;
        private set
        {
            _transform = value;
            _transformSquare = _transform.SubMatrix(0, 2, 0, 2);
            _transformOffset = _transform.Column(2);
        }
    }

    public Matrix<double> TransformSquare => _transformSquare;

    public Vector<double> TransformOffset
    {
        get => _transformOffset;
        set
        {
            _transformOffset = value;
            _transform.SetColumn(2, value);
        }
    }

    private Matrix<double> _transform;
    private Matrix<double> _transformSquare;
    private Vector<double> _transformOffset;

    public Matrix<double> TransformInverse
    {
        get => _transformInverse;
        set
        {
            _transformInverse = value;
            _transformInverseSquare = _transformInverse.SubMatrix(0, 2, 0, 2);
            _transformInverseOffset = _transformInverse.Column(2);
        }
    }

    public Matrix<double> TransformInverseSquare => _transformInverseSquare;
    public Vector<double> TransformInverseOffset
    {
        get => _transformInverseOffset;
        set
        {
            _transformInverseOffset = value;
            _transformInverse.SetColumn(2, value);
        }
    }

    private Matrix<double> _transformInverse;
    private Matrix<double> _transformInverseSquare;
    private Vector<double> _transformInverseOffset;

    public int GetPolygonIndexFromCartesian(Vector2D pos, Vector2I cell) => 0;

    public int GetPolygonVertexCount(int q) => 4;

    public double GetVertexAngleInRad(int i, int polygonIndex) => _offsetAngle - Math.PI / 4 + (Math.PI / 2) * i;

    public RegularUniformFrame GetDual()
    {
        var dualOrigin = ((RegularUniformFrame)this).GetVertex(
            Vector2I.Zero,
            2,
            0);
        var dualSeedVertex = ((RegularUniformFrame)this).GetCellCentroid(
            Vector2I.Zero,
            0);
        return new SquareTileFrame(dualOrigin, dualSeedVertex);
    }

    public Vector2I GetCell(Vector2D cartesianPos)
    {
        var localPos = ((RegularUniformFrame)this).CartesianToLocal.Invoke(cartesianPos);
        return new Vector2I((int)Math.Round(localPos.X), (int)Math.Round(localPos.Y));
    }

    public object Clone()
    {
        var clone = new SquareTileFrame(UnscaledOriginCellCentroidPositions[0] +
                                        Vector2D.OfVector(TransformOffset),
            ((RegularUniformFrame)this).GetVertex(Vector2I.Zero, 0, 0));
        return clone;
    }
}