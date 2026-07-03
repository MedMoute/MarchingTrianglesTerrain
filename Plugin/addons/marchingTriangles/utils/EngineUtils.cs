using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

public class EngineUtils
{
    public static Node GetRootNode(Node node)
    {
        Node rootNode;
        if (Engine.IsEditorHint())
        {
            rootNode = EditorInterface.Singleton.GetEditedSceneRoot();
        }
        else
        {
            rootNode = node.GetTree().Root;
        }

        return rootNode;
    }

    /// <summary>
    /// Sets the owner of a node to the root node of the current scene;
    /// </summary>
    /// <param name="node"></param>
    /// <exception cref="NotImplementedException"></exception>
    public static void SetOwnerAsSceneRoot(Node node)
    {
        node.SetOwner(GetRootNode(node));
    }
}