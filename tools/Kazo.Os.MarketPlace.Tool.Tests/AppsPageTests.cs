namespace Kazo.Os.MarketPlace.Tests;

public sealed class AppsPageTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 15, 30, 0, TimeSpan.Zero);
    private readonly TestCatalog catalog = new();

    public void Dispose() => catalog.Dispose();

    private string Built => Path.Combine(catalog.Root, "built");

    private string Output => Path.Combine(catalog.Root, "packages-branch");

    [Fact]
    public void Publishing_writes_the_english_and_french_pages_next_to_catalog_json()
    {
        Build(catalog.AddApp(fileHandlers: """[{"mediaType":"text/markdown","extensions":[".md",".markdown"],"actions":["open"],"priority":10}]"""));
        List<CatalogError> errors = [];

        CatalogPublisher.Publish(catalog.Root, Built, Output, 3, Now, errors);

        Assert.Empty(errors);
        string english = File.ReadAllText(Path.Combine(Output, "APPS.md"));
        Assert.StartsWith("> [Version française](APPS-fr.md)", english);
        Assert.Contains("1 app. Last publication: 2026-11-13.", english);
        Assert.Contains(
            "| <img src=\"assets/org.example.notes/1.2.0/icon.png\" width=\"48\" height=\"48\" alt=\"\"> | **Notes**<br>`org.example.notes` | Take notes in Markdown.<br>Opens: .md, .markdown | 1.2.0 | Productivity | [Try it](https://os.kazo.ca/?autolaunch=org.example.notes) · [Source](https://github.com/example/Os-App-Notes) · MIT |",
            english);

        string french = File.ReadAllText(Path.Combine(Output, "APPS-fr.md"));
        Assert.StartsWith("> [English version](APPS.md)", french);
        Assert.Contains("Prendre des notes en Markdown.<br>Ouvre : .md, .markdown", french);
        Assert.Contains("| Productivité |", french);
        Assert.Contains("[Essayer](https://os.kazo.ca/?autolaunch=org.example.notes)", french);
    }

    [Fact]
    public void Apps_are_listed_by_name_and_french_falls_back_to_english()
    {
        Build(catalog.AddApp("org.example.zeta"));
        string alpha = catalog.AddApp("org.example.alpha");
        string manifest = Path.Combine(alpha, "manifest.json");
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"name\":\"Notes\"", "\"name\":\"Alpha\"", StringComparison.Ordinal));
        File.Delete(Path.Combine(alpha, "short_description.fr.txt"));
        Build(alpha);
        List<CatalogError> errors = [];

        CatalogPublisher.Publish(catalog.Root, Built, Output, 3, Now, errors);

        Assert.Empty(errors);
        string french = File.ReadAllText(Path.Combine(Output, "APPS-fr.md"));
        Assert.Contains("2 apps.", french);
        Assert.True(french.IndexOf("**Alpha**", StringComparison.Ordinal) < french.IndexOf("**Notes**", StringComparison.Ordinal));
        Assert.Contains("**Alpha**<br>`org.example.alpha` | Take notes in Markdown. |", french);
    }

    [Fact]
    public void Rewriting_an_unchanged_catalog_leaves_the_page_unchanged()
    {
        Build(catalog.AddApp());
        List<CatalogError> errors = [];
        CatalogPublisher.Publish(catalog.Root, Built, Output, 3, Now, errors);
        string first = File.ReadAllText(Path.Combine(Output, "APPS.md"));
        Directory.Delete(Built, recursive: true);

        CatalogPublisher.Publish(catalog.Root, Built, Output, 3, Now.AddDays(5), errors);

        Assert.Empty(errors);
        Assert.Equal(first, File.ReadAllText(Path.Combine(Output, "APPS.md")));
    }

    [Fact]
    public void An_empty_catalog_says_no_app_is_published()
    {
        string page = AppsPage.Render(CatalogDocument.Empty, PageText.English);

        Assert.Contains("No app is published yet.", page);
        Assert.DoesNotContain("| --- |", page);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert\\(1\\)&lt;/script&gt;")]
    [InlineData("a | b\nc", "a \\| b c")]
    [InlineData("[click](https://evil.example)", "\\[click\\]\\(https://evil.example\\)")]
    [InlineData("**bold** & `code`", "\\*\\*bold\\*\\* &amp; \\`code\\`")]
    public void Author_text_cannot_inject_markup_or_break_the_table(string text, string expected) =>
        Assert.Equal(expected, AppsPage.Escape(text));

    private void Build(string folder)
    {
        string target = Path.Combine(Built, Path.GetFileName(folder), "1.0.0");
        Directory.CreateDirectory(target);
        File.WriteAllBytes(Path.Combine(target, "app.kazoapp"), TestCatalog.PackageOf(folder));
    }
}
