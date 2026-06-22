using Godot;

namespace Localproto.addons.marchingTriangles.utils;

public abstract class MttDataHandler
{
    /// <summary>
    /// Generates a default storing directory and returns its path
    /// </summary>
    /// <param name="instance"></param>
    /// <returns></returns>
    public static string GenerateDataDirectory(MarchingTrianglesTerrain terrain)
    {
        if (!terrain.IsInsideTree())
        {
            return "";
        }
        var rootNode = EngineUtils.GetRootNode(terrain);
        if (rootNode == null || rootNode.SceneFilePath.Length == 0)
        {
            return "";
        }

        var scenePath = rootNode.SceneFilePath;
        var sceneDir = scenePath.GetBaseDir();
        var sceneName = scenePath.GetBaseName();
        return sceneDir.PathJoin(sceneName + "_TerrainData").PathJoin(terrain.Name + "_" + GenerateTerrainUid());
    }

    private static string GenerateTerrainUid()
    {
        return Guid.NewGuid().ToString();
    }
}