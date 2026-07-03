using System;
using Godot;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Spatial.Euclidean;
using MathNet.Spatial.Units;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.tiling;

public class DoubleDeltaTileOrientationSystem : RegularUniformFrame
{
    public Angle TilingAngle => Angle.FromRadians(_offSetAngleInRad);

    private readonly double _offSetAngleInRad;

    public Tuple<Vector2D, Vector2D> DualSeeds { get; private set; }
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

    public int PolygonCount => 2;

    /// <summary>
    /// The size of the radius of the regular polygon's circumscribed circle .
    /// </summary>
    public double TilingScale { get; }

    /// <summary>
    /// Public constructor of the DoubleDelta-Tilling.
    /// Providing the centroids of the initial cell fully determines the pattern due to
    /// the polygon restrictions (Each being an equilateral triangle)
    /// </summary>
    /// <param name="c1">First </param>
    /// <param name="c2"></param>
    public DoubleDeltaTileOrientationSystem(Vector2D c1, Vector2D c2)
    {
        //The [C1,C2] segment will be enough to build the entire rhombus that consists of the two triangles :
        //
        //              B * ------* D    Rough Diagram (assume equilateral triangles)
        //               / \C2*  /
        //              /  E*   /
        //             / C1* \ /
        //          C * ----- * A
        Vector2D j = c2 - c1;
        if (j.Length < 1E-5)
        {
            throw new ArgumentException("Cannot create such a small grid. Something is wrong");
        }

        // Due to barycentric properties of equilateral triangles
        Vector2D dPos = c2 + j;
        Vector2D cPos = c1 - j;
        // Be E the Intersection point of [C1 C2] with [AB] :
        // E is the middle of [C1 C2]
        Vector2D ePos = c1 + j / 2;
        double ECnorm = (cPos - ePos).Length;
        // EA's length is half the length L of the triangles 
        // ABE is rectangle in E
        // ECnorm² + L²/4 = L²
        // => L²  = 4 * ECnorm²/3 
        // => L = 2*ECnorm/sqrt(3)
        var L = 2 * ECnorm / Math.Sqrt(3);
        var EBvec = j.Normalize().Orthogonal * (float)(L / 2);
        // Invert as Orthogonal is not the expected rotation
        EBvec = -EBvec;


        var aPos = EBvec + ePos;
        var bPos = ePos - EBvec;

        // The dual will be built from the [B C2 segment]
        DualSeeds = new Tuple<Vector2D, Vector2D>(bPos, c2);

        var CA = aPos - cPos;
        _offSetAngleInRad = Math.Atan2(CA.Y, CA.X);
        TilingScale = j.Length;

        UnscaledOriginCellCentroidPositions = [c1, c2];
        var col1 = CA.ToVector().AsArray();
        var col2 = (bPos - cPos).ToVector().AsArray();
        var col3 = cPos.ToVector().AsArray();
        Transform = Matrix.Build.DenseOfColumnArrays(col1, col2, col3);
        var inverse = TransformSquare.Inverse();
        TransformInverse = inverse.Append(TransformOffset.ToColumnMatrix());
    }

    /// <summary>
    ///  Cloning constructor.
    /// </summary>
    private DoubleDeltaTileOrientationSystem(double offsetAngle,
        double tilingScale,
        Tuple<Vector2D, Vector2D> dualSeeds,
        Matrix<double> transform,
        Matrix<double> transformInverse,
        Vector2D[] unscaledOriginCellCentroidPositions)
    {
        _offSetAngleInRad = offsetAngle;
        TilingScale = tilingScale;
        DualSeeds = dualSeeds;
        Transform = transform;
        TransformInverse = transformInverse;
        UnscaledOriginCellCentroidPositions = unscaledOriginCellCentroidPositions;
    }

    public int GetPolygonVertexCount(int q) => 3;

    public double GetVertexAngleInRad(int i, int polygonIndex)
    {
        return -Math.PI / 6 + 2 * Math.PI / 3 * i + _offSetAngleInRad + polygonIndex * Math.PI;
    }


    public int GetPolygonIndexFromCartesian(Vector2D cartesianPos, Vector2I cell1)
    {
        var localPos = ((RegularUniformFrame)this).CartesianToLocal.Invoke(cartesianPos);
        var cell = GetCell(cartesianPos);
        return localPos.X - cell.X > 1 - (localPos.Y - cell.Y) ? 1 : 0;
    }

    public int GetPolygonIndexFromLocal(Vector2D localPos)
    {
        var cell = new Vector2I((int)Math.Floor(localPos.X), (int)Math.Floor(localPos.Y));
        return localPos.X - cell.X > 1 - (localPos.Y - cell.Y) ? 1 : 0;
    }

    public Vector2I GetCell(Vector2D cartesianPos)
    {
        var localPos = ((RegularUniformFrame)this).CartesianToLocal.Invoke(cartesianPos);
        return new Vector2I((int)Math.Floor(localPos.X), (int)Math.Floor(localPos.Y));
    }


    public RegularUniformFrame GetDual()
    {
        return new HexTileOrientationSystem(DualSeeds.Item1, DualSeeds.Item2);
    }

    public object Clone()
    {
        var newTransform = Matrix.Build.DenseOfMatrix(Transform);
        var newTransformInverse = Matrix.Build.DenseOfMatrix(TransformInverse);
        var newCentroidList = new Vector2D[2];
        UnscaledOriginCellCentroidPositions.CopyTo(newCentroidList, 0);
        var newDualSeed = new Tuple<Vector2D, Vector2D>(DualSeeds.Item1, DualSeeds.Item2);
        var newFrame = new DoubleDeltaTileOrientationSystem(
            TilingAngle.Radians,
            TilingScale,
            newDualSeed,
            newTransform,
            newTransformInverse,
            newCentroidList);
        return newFrame;
    }

    public RegularUniformFrame OffsetBy(Vector2D cartesianOffset)
    {
        DoubleDeltaTileOrientationSystem newFrame = Clone() as DoubleDeltaTileOrientationSystem;
        var newOrigin = (Vector2D.OfVector(newFrame.TransformOffset) + cartesianOffset).ToVector();
        newFrame.TransformOffset = newOrigin;
        newFrame.DualSeeds =
            new Tuple<Vector2D, Vector2D>(DualSeeds.Item1 + cartesianOffset, DualSeeds.Item2 + cartesianOffset);
        newFrame.TransformInverseOffset = newOrigin;

        for (var i = 0; i < UnscaledOriginCellCentroidPositions.Length; i++)
        {
            newFrame.UnscaledOriginCellCentroidPositions[i] += cartesianOffset;
        }

        return newFrame;
    }

    protected bool Equals(DoubleDeltaTileOrientationSystem other)
    {
        return _offSetAngleInRad.Equals(other._offSetAngleInRad) && Equals(DualSeeds, other.DualSeeds) &&
               Equals(UnscaledOriginCellCentroidPositions, other.UnscaledOriginCellCentroidPositions) &&
               Equals(Transform, other.Transform) && Equals(TransformInverse, other.TransformInverse) &&
               TilingScale.Equals(other.TilingScale);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((DoubleDeltaTileOrientationSystem)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_offSetAngleInRad, DualSeeds, UnscaledOriginCellCentroidPositions, Transform,
            TransformInverse, TilingScale);
    }
}