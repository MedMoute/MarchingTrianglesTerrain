using GdUnit4;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using static GdUnit4.Assertions;
using NUnit.Framework;

namespace UnitTests4Godot.test;

//Dummy class ensuring the unit/integration tests requiring GodotRuntime
// have a chance to pass
[TestSuite]
public sealed class TestGodotRuntimeWorks
{
    [GdUnit4.TestCase]
    [RequireGodotRuntime]
    [GodotExceptionMonitor]
    public void PluginStartupTest()
    {
        AddNode(new MarchingTrianglesTerrainPlugin());
    }
}