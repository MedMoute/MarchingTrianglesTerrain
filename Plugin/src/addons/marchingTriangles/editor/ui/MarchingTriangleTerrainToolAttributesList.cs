using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.ui;

/// <summary>
/// List of entries for each possible attribute.
/// </summary>
public class MarchingTriangleTerrainToolAttributesList
{
    public Godot.Collections.Dictionary<string, Variant> BrushType => new()
    {
        { UiAttributeKey.Name, "brushType" },
        { UiAttributeKey.Type, (int)SettingType.Option },
        { UiAttributeKey.Label, "Brush Type" },
        { UiAttributeKey.Options, new[] { "Round", "Square" } },
        { UiAttributeKey.Default, 0 }
    };

    public Godot.Collections.Dictionary<string, Variant> Size => new()
    {
        { UiAttributeKey.Name, "size" },
        { UiAttributeKey.Type, (int)SettingType.Slider },
        { UiAttributeKey.Label, "Size" },
        { UiAttributeKey.Range, new Vector3(1.0f, 50.0f, 0.5f) },
        { UiAttributeKey.Default, 10.0 }
    };

    public Godot.Collections.Dictionary<string, Variant> EaseValue => new()
    {
        { UiAttributeKey.Name, "easeValue" },
        { UiAttributeKey.Type, (int)SettingType.Slider },
        { UiAttributeKey.Label, "Ease Value" },
        { UiAttributeKey.Range, new Vector3(-5.0f, 5.0f, 0.1f) },
        { UiAttributeKey.Default, -1.0 } // No ease
    };

    public Godot.Collections.Dictionary<string, Variant> Height => new()
    {
        { UiAttributeKey.Name, "height" },
        { UiAttributeKey.Type, (int)SettingType.Slider },
        { UiAttributeKey.Label, "Height" },
        { UiAttributeKey.Range, new Vector3(-50.0f, 100.0f, 0.1f) },
        { UiAttributeKey.Default, 0.0 }
    };


    public Godot.Collections.Dictionary<string, Variant> Flatten => new()
    {
        { UiAttributeKey.Name, "flatten" },
        { UiAttributeKey.Type, (int)SettingType.Checkbox },
        { UiAttributeKey.Label, "Flatten" },
        { UiAttributeKey.Default, false }
    };

    public Godot.Collections.Dictionary<string, Variant> Falloff => new()
    {
        { UiAttributeKey.Name, "falloff" },
        { UiAttributeKey.Type, (int)SettingType.Checkbox },
        { UiAttributeKey.Label, "Falloff" },
        { UiAttributeKey.Default, true }
    };

    public Godot.Collections.Dictionary<string, Variant> ChunkManagement => new()
    {
        { UiAttributeKey.Name, "chunkManagement" },
        { UiAttributeKey.Type, (int)SettingType.Chunk },
        { UiAttributeKey.Label, "Chunk Management" }
    };

    public Godot.Collections.Dictionary<string, Variant> TerrainSettings => new()
    {
        { UiAttributeKey.Name, "terrainSettings" },
        { UiAttributeKey.Type, (int)SettingType.Terrain },
        { UiAttributeKey.Label, "Terrain Settings" }
    };
}

public static class UiAttributeKey
{
    public static readonly StringName Name = "name";
    public static readonly StringName Type = "type";
    public static readonly StringName Label = "label";
    public static readonly StringName Default = "default";
    public static readonly StringName Range = "range";
    public static readonly StringName Options = "options";


}