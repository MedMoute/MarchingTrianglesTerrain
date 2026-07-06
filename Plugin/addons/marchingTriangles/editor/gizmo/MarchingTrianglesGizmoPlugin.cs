using Godot;
using Godot.Collections;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.gizmo;

public partial class MarchingTrianglesGizmoPlugin : EditorNode3DGizmoPlugin
{
    private readonly Dictionary<Node, MarchingTriangleTerrainChunkGizmo> _chunkGizmos = new();
    private readonly Dictionary<Node, MarchingTrianglesTerrainGizmo> _terrainGizmos = new();

    public static readonly PlaneMesh BrushMesh = GD.Load<PlaneMesh>("res://addons/marchingTriangles/editor/resources/plugin_materials/brush_visual.tres");

    public static Color HighlightColor = Colors.Blue;
    
    public MarchingTrianglesGizmoPlugin()
    {
        CreateMaterial(nameof(BrushMesh), Colors.White, false, true);
        CreateMaterial(nameof(MarchingTrianglesTerrain.RemoveChunk), Colors.Red, false, true);
        CreateMaterial(nameof(MarchingTrianglesTerrain.AddChunk), Colors.Green, false, true);
        CreateMaterial(nameof(HighlightColor), HighlightColor, false, true);
    }

    public override EditorNode3DGizmo _CreateGizmo(Node3D node3D)
    {
        if (node3D is GdPluginHexTerrainChunk) // Chunk gizmo
        {
            if (!_chunkGizmos.ContainsKey(node3D))
            {
                node3D.TreeExited += () => _chunkGizmos.Remove(node3D);
                var res = new MarchingTriangleTerrainChunkGizmo();
                _chunkGizmos.Add(node3D, res);
                return res;
            }
        }
        else if (node3D is MarchingTrianglesTerrain) // Terrain gizmo
        {
            if (!_terrainGizmos.ContainsKey(node3D))
            {
                node3D.TreeExited += () => _chunkGizmos.Remove(node3D);
                var res = new MarchingTrianglesTerrainGizmo();
                _terrainGizmos.Add(node3D, res);
                return res;
            }
        }

        return null;
    }

    public void TriggerRedraw(Node node)
    {
        if (node is MarchingTrianglesTerrain && _terrainGizmos.TryGetValue(node, out var terrainGizmo))
        {
            terrainGizmo._Redraw();
        }
        else if (node is GdPluginHexTerrainChunk && _chunkGizmos.TryGetValue(node, out var chunkGizmo))
        {
            chunkGizmo._Redraw();
        }
    }

    public void Clear()
    {
        _chunkGizmos.Clear();
        _terrainGizmos.Clear();
    }

    public override string _GetGizmoName()
    {
        return "Marching Triangles Terrain";
    }
}