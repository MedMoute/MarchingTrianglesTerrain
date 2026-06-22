using MathNet.Spatial.Units;

namespace Localproto.addons.marchingTriangles.tiling;

public interface RegularUniformTiling
{
    public int PolygonCount { get; }
    
    public double TilingScale { get; }
    
    public Angle TilingAngle { get; } 
    
    int GetPolygonVertexCount(int q);
    
    double GetVertexAngleInRad(int i, int polygonIndex);

}