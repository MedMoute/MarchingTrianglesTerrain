using System;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.utils;

public class FileUtils
{
    public static int GetDirectorySizeRecursive(string dirPath)
    {
        var totalSize = 0;
        var dir = DirAccess.Open(dirPath);
        if (dir == null)
        {
            return 0;
        }

        dir.ListDirBegin();
        var fileName = dir.GetNext();
        while (fileName.Length > 0)
        {
            var nextPath = dirPath.PathJoin(fileName);
            if (dir.CurrentIsDir())
            {
                if (fileName != "." && fileName != "..") // Ignore self and parent directories
                {
                    totalSize += GetDirectorySizeRecursive(nextPath);
                }
            }
            else
            {
                totalSize += Godot.FileAccess.GetFileAsBytes(nextPath).Length;
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd();
        return totalSize;
    }

    /// <summary>
    /// Edits a provided path to apply the dotnet/project/custom_folder_path property
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string ApplyCustomDirectoryPath(string path)
    {
        if (ProjectSettings.HasSetting("dotnet/project/use_custom_folder_path") &&
            (bool)ProjectSettings.GetSetting("dotnet/project/use_custom_folder_path"))
        {
            var folder = ProjectSettings.GetSetting("dotnet/project/custom_folder_path").AsString();
            if (folder.Length == 0)
            {
                throw new Exception(
                    "Can't find custom folder path property : the \"dotnet/project/custom_folder_path\" property is empty ");
            }

            return !folder.StartsWith("res://")
                ? throw new Exception(
                    "The custom folder path property \"dotnet/project/custom_folder_path\" is invalid." +
                    " It must start with \"res://\" ")
                : path.Replace("res://", folder);
        }

        return path;
    }

    /// <summary>
    /// Wrapper around FileUtils.Load<T> to apply the Custom Directory path if needed.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static T Load<T>(string path) where T : class
    {
        return GD.Load<T>(ApplyCustomDirectoryPath(path));
    }
    
    /// <summary>
    /// Wrapper around FileUtils.Load to apply the Custom Directory path if needed.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static Resource Load(string path)
    {
        return Load<Resource>(ApplyCustomDirectoryPath(path));
    }
}