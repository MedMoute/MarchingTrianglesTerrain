using System;
using System.Text;
using Godot;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Spatial.Euclidean;
using MathNet.Spatial.Units;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.tiling;

public class HexTileOrientationSystem : RegularUniformFrame
{
    public int PolygonCount => 1;
    public double TilingScale { get; }

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

    public Angle TilingAngle { get; }

    public int GetPolygonIndexFromCartesian(Vector2D pos, Vector2I cell) => 0;

    public HexTileOrientationSystem(Vector2D origin, Vector2D seedVector)
    {
        Vector2D j = seedVector - origin;
        if (j.Length < 1E-5)
        {
            throw new ArgumentException("Cannot create such a small grid. Something is wrong");
        }

        TilingScale = j.Length;
        UnscaledOriginCellCentroidPositions = [origin];
        Transform = BuildBasis(origin, seedVector);
        TilingAngle =
            Angle.FromRadians(Math.Atan2(-(seedVector - origin).Y, -(seedVector - origin).X) - 2 * Math.PI / 3);
        var inverse = TransformSquare.Inverse();
        TransformInverse = inverse.Append(TransformOffset.ToColumnMatrix());
    }

    /// <summary>
    /// Clone constructor
    /// </summary>
   private HexTileOrientationSystem(
        double tilingScale,
        Angle tilingAngle,
        Vector2D[] unscaledOriginCellCentroidPositions,
        Matrix<double> transform,
        Matrix<double> transformInverse)
    {
        TilingAngle = tilingAngle;
        TilingScale = tilingScale;
        UnscaledOriginCellCentroidPositions = unscaledOriginCellCentroidPositions; 
        Transform = transform;
        TransformInverse = transformInverse;
    }

    private Matrix<double> BuildBasis(Vector2D origin, Vector2D seedVector)
    {
        var buildVector = seedVector - origin;
        var rotatedVector = buildVector.Rotate(Angle.FromRadians(-Math.PI / 3));
        var basisVector1 = buildVector + rotatedVector;
        var basisVector2 = basisVector1.Rotate(Angle.FromRadians(Math.PI / 3));
        // The provided information is enough to build a basis for the Hexagonal tiling
        // as rotating the [Origin SeedPoint] gives enough information 
        var col1 = basisVector1.ToVector().AsArray();
        var col2 = basisVector2.ToVector().AsArray();
        var col3 = origin.ToVector().AsArray();
        return Matrix.Build.DenseOfColumnArrays(col1, col2, col3);
    }

    public int GetPolygonVertexCount(int q) => 6;

    // From Transform2D.Rotation
    public double GetVertexAngleInRad(int i, int polygonIndex)
    {
        return TilingAngle.Radians + Math.PI * i / 3;
    }

    public Vector2I GetCell(Vector2D cartesianPos)
    {
        var localPos = ((RegularUniformFrame)this).CartesianToLocal.Invoke(cartesianPos);
        return HexagonGrid.CubeRound(localPos);
    }

    public RegularUniformFrame GetDual()
    {
        Console.WriteLine("Dual prep :");
        Console.WriteLine(" Hex center [0,0] =" +
                          TerrainToolPluginHelper.FormatVector2(
                              ((RegularUniformFrame)this).GetCellCentroid(Vector2I.Zero, 0)));

        var sb = new StringBuilder();

        ((RegularUniformFrame)this).GetVertexPositions(Vector2I.Zero).ForEach
        (param =>
            sb.Append(" - ").Append(TerrainToolPluginHelper.FormatVector2(param.Item1))
        );
        Console.WriteLine("{0} \n     Points : {1}", Vector2I.Zero, sb);

        return new DoubleDeltaTileOrientationSystem(
            ((RegularUniformFrame)this).GetVertex(Vector2I.Zero, GetPolygonVertexCount(0) - 2, 0),
            ((RegularUniformFrame)this).GetVertex(Vector2I.Zero, GetPolygonVertexCount(0) - 1, 0)
        );
    }
    
    public object Clone()
    {
        var newTransform = Matrix.Build.DenseOfMatrix(Transform);
        var newTransformInverse = Matrix.Build.DenseOfMatrix(TransformInverse);
        var newCentroidList = new Vector2D[1];
        UnscaledOriginCellCentroidPositions.CopyTo(newCentroidList,0);
        var clone = new HexTileOrientationSystem(TilingScale,
            Angle.FromRadians(TilingAngle.Radians),
            newCentroidList,
            newTransform,
            newTransformInverse);
        return clone;
    }

    private bool Equals(HexTileOrientationSystem other)
    {
        return TilingScale.Equals(other.TilingScale) &&
               Equals(UnscaledOriginCellCentroidPositions, other.UnscaledOriginCellCentroidPositions) &&
               Equals(Transform, other.Transform) && Equals(TransformInverse, other.TransformInverse) &&
               TilingAngle.Equals(other.TilingAngle);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((HexTileOrientationSystem)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TilingScale, UnscaledOriginCellCentroidPositions, Transform, TransformInverse,
            TilingAngle);
    }
}