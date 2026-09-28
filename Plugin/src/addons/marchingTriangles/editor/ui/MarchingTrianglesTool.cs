using System;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.ui;

/// <summary>
/// UI-related properties of a plugin tool.
/// </summary>
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