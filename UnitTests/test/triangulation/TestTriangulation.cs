using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;

namespace UnitTests.triangulation;

/// <summary>
/// Tests on a basic triangulation
/// </summary>
public class TestTriangulation
{
    private Vector3 A = Vector3.Up;
    private Vector3 B = Vector3.Back;
    private Vector3 C = Vector3.Right;

    [Test]
    public void TestCanCreateTriangulation()
    {
        Assert.DoesNotThrow(() =>
        {
            var triangulation = new Triangulation([A, B, C]);
        });
    }

    [Test]
    public void TestCannotCreateTriangulationWithBadInput()
    {
        //Wrong amount of vectors
        //Not enough
        Assert.Throws<ArgumentException>(() =>
        {
            var triangulation = new Triangulation([A, B]);
        });
        //Too many
        Assert.Throws<ArgumentException>(() =>
        {
            var triangulation = new Triangulation([A, B, C, A]);
        });
        //Degenerated Triangle
        Assert.Throws<ArgumentException>(() =>
        {
            var triangulation = new Triangulation([A, B, B]);
        });
        //Degenerated Triangle in 3D space
        Assert.Throws<ArgumentException>(() =>
        {
            var triangulation = new Triangulation([A, B, A.Lerp(B, 0.5f)]);
        });
        //Degenerated Triangle in the 2D projection of the vertices on the xOz plane
        Assert.Throws<ArgumentException>(() =>
        {
            var triangulation = new Triangulation([
                A,
                B,
                new Vector3(
                    Mathf.Lerp(A.X, B.X, 0.5f),
                    3,
                    Mathf.Lerp(A.Z, B.Z, 0.5f))
            ]);
        });
    }
}