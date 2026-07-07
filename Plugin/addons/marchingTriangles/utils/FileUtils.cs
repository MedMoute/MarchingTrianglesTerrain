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
            if (dir.CurrentIsDir()){
            
                if (fileName != "." && fileName != "..") // Ignore self and parent directories
                {
                    totalSize+=GetDirectorySizeRecursive(nextPath);
                }
            }
            else
            {
                totalSize+=FileAccess.GetFileAsBytes(nextPath).Length;
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
        return totalSize;
    }
}