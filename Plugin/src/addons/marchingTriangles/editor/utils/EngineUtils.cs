using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.data;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;

/// <summary>
/// Utility class for Godot Engine-specific tasks.
/// </summary>
public static class EngineUtils
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
   public static void SetOwnerAsSceneRoot(Node node)
    {
        node.SetOwner(GetRootNode(node));
    }

    public static int Mod(int x, int m)
    {
        return (x % m + m) % m;
    }

    public class V2DComp : IEqualityComparer<Vector2D>
    {

        private readonly double _eps;
        
        public V2DComp(double eps)
        {
            _eps = eps;
        }
        public bool Equals(Vector2D v1, Vector2D v2)
        {
            return v1.Equals(v2, _eps);
        }

        public int GetHashCode(Vector2D obj)
        {
            return 1;
        }
    }
    
    public static Texture2D Resize2DTexture(string imagePath,int targetX,int targetY)
    {
        var image = GD.Load<Image>(FileUtils.ApplyCustomDirectoryPath(imagePath));
        image.Resize(targetX,targetY);
        return ImageTexture.CreateFromImage(image);
    }
}