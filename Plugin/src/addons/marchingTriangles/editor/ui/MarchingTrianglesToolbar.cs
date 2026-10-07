using System;
using System.Collections.Generic;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.ui;

/// <summary>
/// The plugin toolbar.
/// This component is the main UI component of the plugin, and renders 
/// </summary>
[Tool]
public partial class MarchingTrianglesToolbar : VFlowContainer
{
    public MarchingTrianglesToolbar()
    {
        SetCustomMinimumSize(new Vector2(35, 35));
    }

    [Signal]
    public delegate void ToolChangedEventHandler(int toolIndex);

    /// <summary>
    /// The plugin toolbox, i.e. the underlying tools of the toolbar
    /// </summary>
    public MarchingTrianglesToolbox ToolBox = new();

    /// <summary>
    /// Position of the horizontal separators in the toolbar.
    /// </summary>
    private readonly int[] _separatorIndexes = [0, 4, 7];

    private readonly Func<int[], Queue<int>> _queueCreator = ints => new Queue<int>(ints);

    public override void _Ready()
    {
        ToolBox.ButtonGroup.Pressed += OnToolSelected;
        AddTools();
    }

    private void OnToolSelected(BaseButton toolAttributes)
    {
        EmitSignal(nameof(ToolChanged), toolAttributes.GetMeta("Index"));
    }

    private void AddTools()
    {
        Alignment = AlignmentMode.Center;
        for (int i = 0; i < MarchingTrianglesToolbox.Tools.Count; i++)
        {
            var queue = _queueCreator.Invoke(_separatorIndexes);
            if (i == queue.Peek())
            {
                queue.Dequeue();
                AddChild(new HSeparator());
            }

            var tool = MarchingTrianglesToolbox.Tools[(TerrainToolMode)i];
            Button button = new();

            button.SetName(tool.Label);
            button.SetTooltipText(tool.Tooltip);
            button.SetButtonIcon(tool.Icon);
            button.SetMeta("Index", i);
            button.SetFlat(true);
            button.SetToggleMode(true);
            float scale = EditorInterface.Singleton.GetEditorScale();
            button.CustomMinimumSize = new Vector2(30f, 30f) * scale;
            button.ExpandIcon = true;
            button.SetButtonGroup(ToolBox.ButtonGroup);

            var centeringContainer = new CenterContainer();
            centeringContainer.CustomMinimumSize = new Vector2(35, 35);
            centeringContainer.AddChild(button, true);
            AddChild(centeringContainer);
            ToolBox.Buttons[i] = button;
        }

        AddChild(new HSeparator());
    }
}