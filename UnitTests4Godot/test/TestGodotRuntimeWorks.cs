using GdUnit4;
using static GdUnit4.Assertions;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.test;

//Dummy class ensuring the unit/integration tests requiring GodotRuntime
// have a chance to pass
[TestSuite]
public sealed class TestGodotRuntimeWorks
{
    [TestCase]
    [RequireGodotRuntime]
    [GodotExceptionMonitor]
    public void PluginStartupTest()
    {
        AddNode(new MarchingTrianglesTerrainPlugin());
    }
}