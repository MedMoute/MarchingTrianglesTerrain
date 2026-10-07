using System;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.data;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.ui;

/// <summary>
/// UI-related properties of a plugin tool.
/// </summary>
// ReSharper disable once Godot.MissingParameterlessConstructor
public partial class MarchingTrianglesTool(
    Texture2D icon,
    String label,
    String tooltip,
    MarchingTrianglesToolAttributeSettings attributeSettings)
    : Resource
{
    internal MarchingTrianglesTool(
        string path,
        string label,
        string tooltip,
        MarchingTrianglesToolAttributeSettings attributeSettings
    ) : this(FileUtils.Load<Texture2D>(path),  label, tooltip, attributeSettings)
    { }
    
    [Export] public Texture2D Icon { get; set; } = icon;
    [Export] public string Label { get; set; } = label;
    [Export(PropertyHint.MultilineText)] public string Tooltip { get; set; } = tooltip;
    [Export] public MarchingTrianglesToolAttributeSettings AttributeSettings { get; set; } = attributeSettings;
}