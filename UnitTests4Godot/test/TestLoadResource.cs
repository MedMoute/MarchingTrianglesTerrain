using GdUnit4;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using NUnit.Framework;
using FileAccess = Godot.FileAccess;

namespace UnitTests4Godot.test;

[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class TestLoadResources
{
    [GdUnit4.TestCase]
    public void TestLoadFromMetadataResourceFile()
    {
        //Read the resource file through FileAccess
        String str = FileAccess.GetFileAsString("res://resources/Chunks/chunk_1_1/"+MttDataHandler.MetadataFilename); 
        Assert.That(str.Length,Is.GreaterThan(0));
        // We're now sure the file exist : Attempt to load as a Resource
        DirAccess.Open("res://resources/Chunks/chunk_1_1");
        Resource res = ResourceLoader.Load("res://resources/Chunks/chunk_1_1/"+MttDataHandler.MetadataFilename, "", ResourceLoader.CacheMode.Ignore);
            
        if (res is MttChunkData data)
        {
            Assert.That(data.ChunkCoords, Is.EqualTo(new Vector2I(1,1)));
            Assert.That(data.Mesh, Is.Not.Null.And.InstanceOf<Mesh>());
            Assert.That(data.CollisionFaces, Is.Not.Null.And.Not.Empty);
            Assert.That(data.MergeMode,Is.Not.Null);
            Assert.That(data.Data,Is.Not.Null);
        }
        else
        {
            Assert.Fail();
        }
    }
    
    [GdUnit4.TestCase]
    public void TestLoadFromDatastuctResourceFile()
    {
        //Read the resource file through FileAccess
        String str = FileAccess.GetFileAsString("res://resources/Chunks/chunk_1_1/"+MttDataHandler.DataStructFilename); 
        Assert.That(str.Length,Is.GreaterThan(0));
        // We're now sure the file exist : Attempt to load as a Resource
        DirAccess.Open("res://resources/Chunks/chunk_1_1");
        Resource res = ResourceLoader.Load("res://resources/Chunks/chunk_1_1/"+MttDataHandler.DataStructFilename, "", ResourceLoader.CacheMode.Ignore);
            
        if (res is DelegatedChunkDataStruct data)
        {
            Assert.That(data.FrameDimensions, Is.EqualTo(new Vector3I(10,10,2)));
        }
        else
        {
            Assert.Fail();
        }
    }
}