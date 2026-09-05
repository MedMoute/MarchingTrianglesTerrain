using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.ui;

public class MarchingTriangleTerrainToolAttributesList
{
    public MarchingTrianglesTextureNames VpTexturePaints { get; } = new();

    public Godot.Collections.Dictionary<string, Variant> BrushType => new()
    {
        { "name", "brushType" },
        { "type", "option" },
        { "label", "Brush Type" },
        { "options", new[] { "Round", "Square" } },
        { "default", 0 }
    };

    public Godot.Collections.Dictionary<string, Variant> Size => new()
    {
        { "name", "size" },
        { "type", "slider" },
        { "label", "Size" },
        { "range", new Vector3(1.0f, 50.0f, 0.5f) },
        { "default", 10.0 }
    };

    public Godot.Collections.Dictionary<string, Variant> EaseValue => new()
    {
        { "name", "easeValue" },
        { "type", "slider" },
        { "label", "Ease Value" },
        { "range", new Vector3(-5.0f, 5.0f, 0.1f) },
        { "default", -1.0 } // No ease
    };

    public Godot.Collections.Dictionary<string, Variant> Height => new()
    {
        { "name", "height" },
        { "type", "slider" },
        { "label", "Height" },
        { "range", new Vector3(-50.0f, 100.0f, 0.1f) },
        { "default", 0.0 }
    };

    public Godot.Collections.Dictionary<string, Variant> Strength => new()
    {
        { "name", "strength" },
        { "type", "slider" },
        { "label", "Strength" },
        { "range", new Vector3(0.025f, 0.3f, 0.005f) },
        { "default", 0.05 }
    };

    public Godot.Collections.Dictionary<string, Variant> Flatten => new()
    {
        { "name", "flatten" },
        { "type", "checkbox" },
        { "label", "Flatten" },
        { "default", false }
    };

    public Godot.Collections.Dictionary<string, Variant> Falloff => new()
    {
        { "name", "falloff" },
        { "type", "checkbox" },
        { "label", "Falloff" },
        { "default", true }
    };

    public Godot.Collections.Dictionary<string, Variant> MaskMode => new()
    {
        { "name", "maskMode" },
        { "type", "checkbox" },
        { "label", "Mask Mode" },
        { "default", true }
    };
    
    public Godot.Collections.Dictionary<string, Variant> Material => new()
    {
        { "name", "material" },
        { "type", "option" },
        { "label", "Material" },
        { "options", VpTexturePaints.TextureNames },
        { "default", 0 }
    };

    public Godot.Collections.Dictionary<string, Variant> TextureName => new()
    {
        { "name", "textureName" },
        { "type", "text" },
        { "label", "Texture Name" },
        { "default", "New name here..." }
    };

    public Godot.Collections.Dictionary<string, Variant> TexturePreset => new()
    {
        { "name", "texturePreset" },
        { "type", "preset" },
        { "label", "Texture Preset" },
        { "default", "" }
    };

    public Godot.Collections.Dictionary<string, Variant> QuickPaintSelection => new()
    {
        { "name", "quickPaintSelection" },
        { "type", "quick_paint" },
        { "label", "Quick Paint" },
        { "default", "" }
    };

    public Godot.Collections.Dictionary<string, Variant> PaintWalls => new()
    {
        { "name", "paintWalls" },
        { "type", "checkbox" },
        { "label", "Paint Walls" },
        { "default", false }
    };

    public Godot.Collections.Dictionary<string, Variant> ChunkManagement => new()
    {
        { "name", "chunkManagement" },
        { "type", "chunk" },
        { "label", "Chunk Management" }
    };

    public Godot.Collections.Dictionary<string, Variant> TerrainSettings => new()
    {
        { "name", "terrainSettings" },
        { "type", "terrain" },
        { "label", "Terrain Settings" }
    };
}

public partial class MarchingTrianglesTextureNames : Resource
{
// Vertex painting texture display names (unified for both floor and wall painting)
    [Export] public string[] TextureNames =
    [
        "Base Grass", "Texture 2 (g)", "Texture 3 (g)", "Texture 4 (g)",
        "Texture 5 (g)", "Texture 6 (g)", "Texture 7", "Texture 8",
        "Texture 9", "Texture 10", "Texture 11", "Texture 12",
        "Texture 13", "Texture 14", "Texture 15", "Void"
    ];
}