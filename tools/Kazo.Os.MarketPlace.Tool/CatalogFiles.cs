namespace Kazo.Os.MarketPlace;

/// <summary>Files of a published version on the <c>packages</c> branch.</summary>
internal static class CatalogFiles
{
    /// <summary>Folder of the package of a version, relative to the branch root.</summary>
    public static string PackageFolder(string appId, string version) => $"packages/{appId}/{version}";

    /// <summary>Folder of the icon, screenshots and descriptions of a version, relative to the branch root.</summary>
    public static string AssetsFolder(string appId, string version) => $"assets/{appId}/{version}";

    /// <summary>Removes the package and assets of a version that leaves the catalog.</summary>
    /// <remarks>Its folder stays in the history of the branch it was submitted to.</remarks>
    public static void DeleteVersion(string outputRoot, string appId, string version)
    {
        DeleteDirectory(Path.Combine(outputRoot, PackageFolder(appId, version)));
        DeleteDirectory(Path.Combine(outputRoot, AssetsFolder(appId, version)));
    }

    /// <summary>Writes a file, creating its folder.</summary>
    public static void WriteFile(string root, string relativePath, byte[] bytes)
    {
        string path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
