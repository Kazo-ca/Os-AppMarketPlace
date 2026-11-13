using System.Buffers.Binary;
using System.Text;
using Kazo.Os.Platform.Core.Apps.Packages;

namespace Kazo.Os.MarketPlace.Tests;

/// <summary>A catalog checkout in a temporary folder, with helpers to write valid app folders.</summary>
internal sealed class TestCatalog : IDisposable
{
    public const string Commit = "3f2a9c1e0b7d4c5a8e6f1a2b3c4d5e6f7a8b9c0d";

    public TestCatalog()
    {
        Root = Path.Combine(Path.GetTempPath(), "kazoos-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "apps"));
        File.WriteAllText(Path.Combine(Root, "categories.json"), """
            [
              { "id": "productivity", "labels": { "en": "Productivity", "fr": "Productivité" } },
              { "id": "utilities", "labels": { "en": "Utilities", "fr": "Utilitaires" } }
            ]
            """);
    }

    public string Root { get; }

    public string Apps => Path.Combine(Root, "apps");

    /// <summary>Writes a valid folder for <paramref name="appId"/> at <paramref name="version"/>.</summary>
    /// <param name="fileHandlers">JSON array written as the manifest's <c>fileHandlers</c>.</param>
    public string AddApp(string appId = "org.example.notes", string version = "1.2.0", string[]? capabilities = null, string fileHandlers = "[]")
    {
        string folder = Path.Combine(Apps, $"{appId}_{version}");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "icon.png"), Png(256, 256));
        File.WriteAllText(Path.Combine(folder, "short_description.en.txt"), "Take notes in Markdown.\n");
        File.WriteAllText(Path.Combine(folder, "short_description.fr.txt"), "Prendre des notes en Markdown.\n");
        File.WriteAllText(Path.Combine(folder, "description.en.md"), "# Notes\n\nA note app.\n");
        File.WriteAllText(Path.Combine(folder, "manifest.json"), Manifest(appId, version, capabilities ?? [], fileHandlers));
        File.WriteAllText(Path.Combine(folder, "store.json"), $$"""
            {
              "repository": "https://github.com/example/Os-App-Notes",
              "commit": "{{Commit}}",
              "category": "productivity",
              "tags": ["notes", "markdown"],
              "license": "MIT"
            }
            """);
        return folder;
    }

    /// <summary>Builds the package our CI would build for a folder, with a fake assembly.</summary>
    public static byte[] Package(string manifestJson) =>
        AppPackageWriter.Write(Encoding.UTF8.GetBytes(manifestJson), _ => [0x4D, 0x5A, 0x90, 0x00], _ => []);

    public static byte[] PackageOf(string folder) => Package(File.ReadAllText(Path.Combine(folder, "manifest.json")));

    /// <summary>A manifest as the Kazo OS generator writes it.</summary>
    /// <remarks>Only window apps may declare file handlers: a manifest with some is a window app.</remarks>
    public static string Manifest(string appId, string version, string[] capabilities, string fileHandlers = "[]")
    {
        bool window = fileHandlers != "[]";
        string kind = window ? "window" : "terminal";
        string execution = window ? "mainThread" : "pool";
        string surface = window
            ? "\"window\":{\"defaultWidth\":800,\"defaultHeight\":600,\"minWidth\":400,\"minHeight\":300,\"resizable\":true}"
            : "\"terminal\":{\"name\":\"notes\",\"aliases\":[],\"usage\":\"notes\"}";
        return $$"""
        {"schemaVersion":1,"id":"{{appId}}","name":"Notes","version":"{{version}}","publisherId":"pub.example","description":"Notes","kind":"{{kind}}","entryPoint":{"assembly":"Example.Notes.dll","type":"Example.Notes.NotesCommand"},"sdkCompatibility":{"minimumVersion":"3.0.0"},"presentation":{"category":"utilities","launchVisibility":"hidden","iconAssetPaths":[]},"localizations":[],"capabilities":[{{string.Join(",", capabilities.Select(c => $"\"{c}\""))}}],"declaredTopicPermissions":[],"resources":{"baselineCpuWeight":0,"burstCpuWeight":0,"baselineMemoryWeight":0,"burstMemoryWeight":0,"baselineStorageIoWeight":0,"burstStorageIoWeight":0,"baselineNetworkIoWeight":0,"burstNetworkIoWeight":0},"intents":[],"dependencies":[],"assets":[],"fileHandlers":{{fileHandlers}},{{surface}},"singleInstancePerUser":false,"autoStart":false,"supportsBack":false,"execution":"{{execution}}"}
        """;
    }

    /// <summary>The header of a PNG image of the given size (the checks never decode pixels).</summary>
    public static byte[] Png(int width, int height, int padding = 0)
    {
        byte[] bytes = new byte[33 + padding];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)height);
        return bytes;
    }

    public CategoryList Categories()
    {
        List<CatalogError> errors = [];
        CategoryList list = CategoryList.Load(Path.Combine(Root, "categories.json"), "categories.json", errors);
        Assert.Empty(errors);
        return list;
    }

    public List<CatalogError> Validate(string folder)
    {
        List<CatalogError> errors = [];
        AppFolderValidator.Validate(folder, Categories(), errors);
        return errors;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
