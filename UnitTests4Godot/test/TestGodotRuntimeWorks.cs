using GdUnit4;

namespace UnitTests4Godot;

//Dummy class ensuring the unit/integration tests requiring GodotRuntime
// have a chance to pass
[TestSuite]
public sealed class TestGodotRuntimeWorks
{
    [TestCase]
    [RequireGodotRuntime]
    public void DummyTest()
    {
    }
}