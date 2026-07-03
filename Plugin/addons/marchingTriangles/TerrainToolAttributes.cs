using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using Godot.Collections;
using MarchingTrianglesTerrain.addons.marchingTriangles.ui;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Tool Attribute values. Those values are the ones actually used by the algorithm.
/// </summary>
public partial class TerrainToolAttributes : Node
{
    [Signal]
    public delegate void TexturesUpdatedEventHandler();

    public event TexturesUpdatedEventHandler TextureUpdated
    {
        add => Connect(nameof(TexturesUpdatedEventHandler),
            Callable.From(() => { throw new NotImplementedException(); }));
        remove => Disconnect(nameof(TexturesUpdatedEventHandler),
            Callable.From(() => { throw new NotImplementedException(); }));
    }


    public int BrushIndex { get; set; } = 0;
    public double BrushSize { get; set; } = 5D;
    public double EaseValue { get; set; } = -1; // No ease
    public double Strength { get; set; } = 1;
    public double Height { get; set; } = 0;
    public bool Flatten { get; set; } = true;
    public bool Falloff { get; set; } = true;

    public bool MaskGrass { get; set; } = false;
    
    public Curve FalloffCurve { get; set; } = GD.Load<Curve>("res://addons/marchingTriangles/editor/resources/plugin_materials/curve_falloff.tres");

    // 3D point whe the tool dragging started
    public Vector3 DragBasePosition { get; set; }

    public Vector2I SelectedChunk { get; set; } = new Vector2I();

    // Only relevant for vertex painting
    public bool PaintWalls { get; set; } = false;

    private int _vertexColorIndex;

    public int VertexColorIndex
    {
        get => _vertexColorIndex;
        set
        {
            _vertexColorIndex = value;
            SetVertexColorIndex(value);
        }
    }

    private MarchingTrianglesTexturesPreset _preset = new();

    public MarchingTrianglesTexturesPreset CurrentTexturePreset
    {
        get => _preset;
        set
        {
            _preset = value;
            CurrentQuickPaint = null;
            if (!SyncFromTerrain)
                SetNewTextures(value);
        }
    }

    private void SetNewTextures(MarchingTrianglesTexturesPreset value)
    {
        if (_preset == null)
        {
            //FIXME => DEFAULT.Copy or smthg
            _preset = new();
            //FIXME : TODO
            EmitSignal(nameof(TextureUpdated));
        }
    }

    public MarchingTrianglesTexturesPreset CurrentQuickPaint { get; set; } = null;

    private void SetVertexColorIndex(int vertexColorIndex)
    {
        switch (vertexColorIndex)
        {
            case 0: // rr
                _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                break;
            case 1: // rg
                _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                break;
            case 2: // rb
                _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                break;
            case 3: // ra
                _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
            case 4: // gr
                _vertexColor0 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                break;
            case 5: // gg
                _vertexColor0 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                break;
            case 6: // gb
                _vertexColor0 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                break;
            case 7: // ga
                _vertexColor0 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
            case 8: // br
                _vertexColor0 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                break;
            case 9: // bg
                _vertexColor0 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                break;
            case 10: // bb
                _vertexColor0 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                break;
            case 11: // ba
                _vertexColor0 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
            case 12: // ar
                _vertexColor0 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);
                break;
            case 13: // ag
                _vertexColor0 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                _vertexColor1 = new Color(0.0f, 1.0f, 0.0f, 0.0f);
                break;
            case 14: // ab
                _vertexColor0 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 1.0f, 0.0f);
                break;
            case 15: // aa
                _vertexColor0 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                _vertexColor1 = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
        }
    }

    private Color _vertexColor0 = new Color(1.0f, 0.0f, 0.0f, 0.0f);

    private Color _vertexColor1 = new Color(1.0f, 0.0f, 0.0f, 0.0f);

    // Only relevant for Bridge building
    public Vector3 BridgeStartPos { get; set; }
    
    public Variant TerrainSettings { get; }

    // Flag to prevent _set_new_textures() when syncing preset from terrain node
    public bool SyncFromTerrain = false;
}

public partial class MarchingTrianglesTexturesPreset : Resource
{
    [Export] public String PresetName { get; set; }
    [Export] public MarchingTrianglesTextureNames NewTexturesNames { get; set; } = new();
    [Export] public MarchingTrianglesTextureList NewTextures { get; set; }
    [Export] public Array<MarchingTrianglesQuickPaint> QuickPaints { get; set; }
}

public partial class MarchingTrianglesQuickPaint : Resource
{
    internal static MarchingTrianglesTextureNames TextureNames = new();

    [Export] public String PaintName { get; set; } = "New Paint";

    private int _wallTextureSlot = 0;
    private int _groundTextureSlot = 0;

    [ExportGroup("Textures")] [Export] public bool HasGrass = false;

    public override Array<Dictionary> _GetPropertyList()
    {
        var properties = new Array<Dictionary>();
        List<String> tmpList = new();
        StringBuilder sb = new();
        tmpList.AddRange(TextureNames.TextureNames);
        tmpList.ForEach(name => sb.Append(name));
        string textureNamesAsString = sb.ToString();
        // Wall texture dropdown content
        properties.Add(new Dictionary
        {
            { "name", "wall_texture_slot" },
            { "type", (int)Variant.Type.Int },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", "," + textureNamesAsString },
            { "usage", (int)PropertyUsageFlags.Default }
        });

        properties.Add(new Dictionary
        {
            { "name", "ground_texture_slot" },
            { "type", (int)Variant.Type.Int },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", "," + textureNamesAsString },
            { "usage", (int)PropertyUsageFlags.Default }
        });
        return properties;
    }
}

/// <summary>
/// Class listing the content properties of each of the 16 supported textures.
/// </summary>
public partial class MarchingTrianglesTextureList : Resource
{
    [Export] public Array<Texture2D> TerrainTextures =
    [
        new(), new(), new(), new(),
        new(), new(), new(), new(),
        new(), new(), new(), new(),
        new(), new(), new(), new()
    ];

    [Export] public Array<float> TextureScales =
    [
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
    ];

    [Export] public Array<bool> HasGrass =
        [];

    [Export] public Array<Texture2D> GrassTextures =
        [];

    [Export] public Array<Color> GrassColors =
        [];
}