namespace Kazo.Os.MarketPlace.Tests;

/// <summary>The "rebuild only" check (ADR 0271 §5 of Kazo-ca/Os).</summary>
public sealed class RebuildCheckTests : IDisposable
{
    private const string BumpDiff = """
        diff --git a/external/KazoOs b/external/KazoOs
        index 1111111..2222222 160000
        --- a/external/KazoOs
        +++ b/external/KazoOs
        @@ -1 +1 @@
        -Subproject commit 1111111111111111111111111111111111111111
        +Subproject commit 2222222222222222222222222222222222222222
        diff --git a/src/Notes/NotesManifest.cs b/src/Notes/NotesManifest.cs
        index 3333333..4444444 100644
        --- a/src/Notes/NotesManifest.cs
        +++ b/src/Notes/NotesManifest.cs
        @@ -12 +12 @@ public sealed class NotesManifest
        -    public string Version => "1.0.0";
        +    public string Version => "1.0.1-beta.1";
        diff --git a/src/Notes/manifest.json b/src/Notes/manifest.json
        index 5555555..6666666 100644
        --- a/src/Notes/manifest.json
        +++ b/src/Notes/manifest.json
        @@ -4 +4 @@
        -  "version": "1.0.0",
        +  "version": "1.0.1-beta.1",
        \ No newline at end of file
        """;

    private readonly TestCatalog catalog = new();

    public void Dispose() => catalog.Dispose();

    [Fact]
    public void A_new_submodule_and_version_is_a_rebuild_only()
    {
        (AppFolder previous, AppFolder submitted) = Pair();

        Assert.Empty(RebuildCheck.Check(previous, submitted, BumpDiff));
    }

    [Fact]
    public void A_new_minimum_sdk_in_the_manifest_is_part_of_a_rebuild()
    {
        string folder = catalog.AddApp(version: "1.0.1-beta.1");
        string manifest = Path.Combine(folder, "manifest.json");
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"minimumVersion\":\"3.0.0\"", "\"minimumVersion\":\"3.5.0-preview.1\"", StringComparison.Ordinal));
        (AppFolder previous, AppFolder submitted) = Pair(submittedFolder: folder);

        Assert.Empty(RebuildCheck.Check(previous, submitted, BumpDiff));
    }

    [Fact]
    public void A_code_change_needs_a_review()
    {
        (AppFolder previous, AppFolder submitted) = Pair();
        string diff = BumpDiff + """

            diff --git a/src/Notes/Editor.cs b/src/Notes/Editor.cs
            index 7777777..8888888 100644
            --- a/src/Notes/Editor.cs
            +++ b/src/Notes/Editor.cs
            @@ -3,0 +4 @@
            +    Upload(notes);
            """;

        Assert.Contains(RebuildCheck.Check(previous, submitted, diff), r => r.Contains("src/Notes/Editor.cs", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("new file mode 100644")]
    [InlineData("deleted file mode 100644")]
    [InlineData("old mode 100644")]
    [InlineData("Binary files a/x and b/x differ")]
    public void A_new_deleted_or_binary_file_needs_a_review(string header)
    {
        List<string> reasons = [];
        RebuildCheck.CheckDiff($"diff --git a/x b/x\n{header}\n", "1.0.0", "1.0.1", reasons);

        Assert.Single(reasons);
    }

    [Fact]
    public void Another_submodule_needs_a_review()
    {
        List<string> reasons = [];
        RebuildCheck.CheckDiff("diff --git a/lib/engine b/lib/engine\nindex 1..2 160000\n@@ -1 +1 @@\n-Subproject commit 1\n+Subproject commit 2\n", "1.0.0", "1.0.1", reasons);

        Assert.Contains(reasons, r => r.Contains("lib/engine", StringComparison.Ordinal));
    }

    [Fact]
    public void A_line_changed_beyond_the_version_needs_a_review()
    {
        List<string> reasons = [];
        RebuildCheck.CheckDiff("diff --git a/a.cs b/a.cs\nindex 1..2 100644\n@@ -1 +1 @@\n-Version => \"1.0.0\"; // ok\n+Version => \"1.0.1\"; Run();\n", "1.0.0", "1.0.1", reasons);

        Assert.Single(reasons);
    }

    [Fact]
    public void New_capabilities_need_a_review()
    {
        (AppFolder previous, AppFolder submitted) = Pair(submittedFolder: catalog.AddApp(version: "1.0.1-beta.1", capabilities: ["clipboard.read"]));

        IReadOnlyList<string> reasons = RebuildCheck.Check(previous, submitted, BumpDiff);

        Assert.Contains("The capabilities change.", reasons);
    }

    [Fact]
    public void A_new_description_or_screenshot_needs_a_review()
    {
        string folder = catalog.AddApp(version: "1.0.1-beta.1");
        File.WriteAllText(Path.Combine(folder, "description.en.md"), "# Notes\n\nNow with sync.\n");
        (AppFolder previous, AppFolder submitted) = Pair(submittedFolder: folder);

        Assert.Contains(RebuildCheck.Check(previous, submitted, BumpDiff), r => r.Contains("description.en.md", StringComparison.Ordinal));
    }

    [Fact]
    public void Another_store_entry_needs_a_review()
    {
        string folder = catalog.AddApp(version: "1.0.1-beta.1");
        string store = Path.Combine(folder, "store.json");
        File.WriteAllText(store, File.ReadAllText(store).Replace("example/Os-App-Notes", "someone/Other", StringComparison.Ordinal));
        (AppFolder previous, AppFolder submitted) = Pair(submittedFolder: folder);

        Assert.Contains("`store.json` changes more than `commit`.", RebuildCheck.Check(previous, submitted, BumpDiff));
    }

    [Fact]
    public void The_previous_version_is_the_newest_lower_one_of_the_same_app()
    {
        catalog.AddApp(version: "0.9.0");
        catalog.AddApp(version: "1.0.0");
        catalog.AddApp(version: "1.0.1-beta.1");
        catalog.AddApp(version: "2.0.0");
        catalog.AddApp(appId: "org.example.other", version: "1.0.0");

        Assert.Equal("org.example.notes_1.0.1-beta.1", RebuildCheck.FindPrevious(catalog.Apps, "org.example.notes", Kazo.Os.App.Abstractions.SemanticVersion.Parse("1.0.1-beta.2")));
        Assert.Equal("org.example.notes_1.0.1-beta.1", RebuildCheck.FindPrevious(catalog.Apps, "org.example.notes", Kazo.Os.App.Abstractions.SemanticVersion.Parse("1.0.1")));
        Assert.Null(RebuildCheck.FindPrevious(catalog.Apps, "org.example.notes", Kazo.Os.App.Abstractions.SemanticVersion.Parse("0.9.0")));
        Assert.Null(RebuildCheck.FindPrevious(catalog.Apps, "org.example.new", Kazo.Os.App.Abstractions.SemanticVersion.Parse("1.0.0")));
    }

    [Fact]
    public void Check_rebuild_and_previous_version_run_from_the_command_line()
    {
        catalog.AddApp(version: "1.0.0");
        catalog.AddApp(version: "1.0.1-beta.1");
        string diff = Path.Combine(catalog.Root, "app.diff");
        File.WriteAllText(diff, BumpDiff);

        Assert.Equal(0, Program.Main(["previous-version", "--root", catalog.Root, "--folder", "org.example.notes_1.0.1-beta.1"]));
        Assert.Equal(0, Program.Main(["check-rebuild", "--root", catalog.Root, "--folder", "org.example.notes_1.0.1-beta.1", "--diff", diff]));
        Assert.Equal(1, Program.Main(["check-rebuild", "--root", catalog.Root, "--folder", "org.example.notes_1.0.0", "--diff", diff]));
    }

    private (AppFolder Previous, AppFolder Submitted) Pair(string? submittedFolder = null)
    {
        string previous = catalog.AddApp(version: "1.0.0");
        string submitted = submittedFolder ?? catalog.AddApp(version: "1.0.1-beta.1");
        return (Read(previous), Read(submitted));
    }

    private AppFolder Read(string folder)
    {
        List<CatalogError> errors = [];
        AppFolder? read = AppFolderValidator.Validate(folder, catalog.Categories(), errors);
        Assert.Empty(errors);
        return read!;
    }
}
