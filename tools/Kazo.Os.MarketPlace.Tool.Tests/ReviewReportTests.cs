namespace Kazo.Os.MarketPlace.Tests;

public sealed class ReviewReportTests : IDisposable
{
    private readonly TestCatalog catalog = new();

    public void Dispose() => catalog.Dispose();

    [Fact]
    public void Dangerous_capabilities_come_first_and_are_highlighted()
    {
        AppFolder folder = Load(catalog.AddApp(capabilities: ["clipboard.read", "filesystem.system.write"]));
        byte[] bytes = TestCatalog.PackageOf(folder.Path);
        List<CatalogError> errors = [];

        string report = ReviewReport.Write(folder, PackageCheck.Check(folder, bytes, errors), bytes, [], errors);

        int dangerous = report.IndexOf("⚠️ **`filesystem.system.write`** (dangerous)", StringComparison.Ordinal);
        int normal = report.IndexOf("- `clipboard.read`", StringComparison.Ordinal);
        Assert.True(dangerous >= 0 && normal > dangerous, report);
        Assert.Contains("**Embedded JavaScript** (0)", report, StringComparison.Ordinal);
        Assert.Contains("`Example.Notes.dll`", report, StringComparison.Ordinal);
        Assert.Contains($"tree/{TestCatalog.Commit}", report, StringComparison.Ordinal);
        Assert.Contains("**CodeQL**: no result", report, StringComparison.Ordinal);
    }

    [Fact]
    public void CodeQL_findings_and_problems_are_listed()
    {
        AppFolder folder = Load(catalog.AddApp());
        SarifFinding[] findings =
        [
            new("error", "cs/sql-injection", "Query built from user input.", "src/Db.cs:12"),
            new("warning", "js/xss", "Unsafe HTML.", "wwwroot/app.js:3"),
        ];
        CatalogError[] errors = [new("apps/org.example.notes_1.2.0", "No package was built.")];

        string report = ReviewReport.Write(folder, null, null, findings, errors);

        Assert.Contains("**❌ Problems**", report, StringComparison.Ordinal);
        Assert.Contains("**CodeQL**: 1 error, 1 warning", report, StringComparison.Ordinal);
        Assert.Contains("- error `cs/sql-injection` at `src/Db.cs:12`", report, StringComparison.Ordinal);
    }

    [Fact]
    public void The_drive_types_a_feature_provides_are_listed_with_their_hosts()
    {
        const string providers = """
            [{"type":"exampledav","name":"Example DAV","provider":"Example.Drives.ExampleDavProvider, Example.Drives","hosts":["*.example.org"],
              "connection":{"kind":"form","fields":[{"name":"address","label":"Address","kind":"url","secret":false,"required":true,"serverAddress":true}]}}]
            """;
        string manifest = TestCatalog.FeatureManifest("org.example.drives", "1.0.0", ["drives.provide"], providers);
        AppFolder folder = Load(catalog.AddApp("org.example.drives", "1.0.0", manifest, "feature"));

        string report = ReviewReport.Write(folder, null, null, [], []);

        Assert.Contains("⚠️ **`drives.provide`** (dangerous)", report, StringComparison.Ordinal);
        Assert.Contains("**Drive providers** (1)", report, StringComparison.Ordinal);
        Assert.Contains("- `exampledav` (Example DAV), class `Example.Drives.ExampleDavProvider, Example.Drives`: form; the host the user types in `address` is allowed too (ADR 0284); hosts `*.example.org`", report, StringComparison.Ordinal);
    }

    [Fact]
    public void An_app_without_drive_providers_has_no_drive_section()
    {
        string report = ReviewReport.Write(Load(catalog.AddApp()), null, null, [], []);

        Assert.DoesNotContain("**Drive providers**", report, StringComparison.Ordinal);
    }

    private AppFolder Load(string path)
    {
        List<CatalogError> errors = [];
        AppFolder? folder = AppFolderValidator.Validate(path, catalog.Categories(), errors);
        Assert.Empty(errors);
        return folder!;
    }
}
