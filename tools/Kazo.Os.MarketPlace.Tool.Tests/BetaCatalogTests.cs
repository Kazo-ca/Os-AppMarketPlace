using System.Text.Json.Nodes;

namespace Kazo.Os.MarketPlace.Tests;

/// <summary>catalog-beta.json (ADR 0271 §5 of Kazo-ca/Os), written by both publications.</summary>
public sealed class BetaCatalogTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private readonly TestCatalog catalog = new();

    public void Dispose() => catalog.Dispose();

    private string Built => Path.Combine(catalog.Root, "built");

    private string Output => Path.Combine(catalog.Root, "packages-branch");

    [Fact]
    public void A_stable_publication_writes_the_same_apps_in_catalog_beta_json()
    {
        Publish(ReleaseChannel.Stable, catalog.AddApp(version: "1.0.0"));

        JsonNode stable = Read("catalog.json");
        JsonNode beta = Read("catalog-beta.json");

        Assert.True(JsonNode.DeepEquals(stable, beta));
    }

    [Fact]
    public void A_beta_version_joins_the_stable_ones_without_changing_the_listing()
    {
        Publish(ReleaseChannel.Stable, catalog.AddApp(version: "1.0.0"));
        string catalogJson = File.ReadAllText(Path.Combine(Output, "catalog.json"));
        string beta = catalog.AddApp(version: "1.0.1-beta.1");
        File.WriteAllText(Path.Combine(beta, "short_description.en.txt"), "Beta text.");

        Publish(ReleaseChannel.Beta, beta);

        JsonNode app = Read("catalog-beta.json")["apps"]![0]!;
        Assert.Equal("1.0.0", (string?)app["latestVersion"]);
        Assert.Equal("Take notes in Markdown.", (string?)app["shortDescriptions"]!["en"]);
        Assert.Equal(["1.0.1-beta.1", "1.0.0"], Versions(app));
        Assert.Equal(64, ((string?)app["versions"]![0]!["sha256"])!.Length);
        Assert.True(File.Exists(Path.Combine(Output, "packages/org.example.notes/1.0.1-beta.1/org.example.notes-1.0.1-beta.1.kazoapp")));
        Assert.Equal(catalogJson, File.ReadAllText(Path.Combine(Output, "catalog.json")));
        Assert.DoesNotContain("beta", File.ReadAllText(Path.Combine(Output, "APPS.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void An_app_with_only_beta_versions_is_listed_from_its_newest_one()
    {
        Publish(ReleaseChannel.Beta, catalog.AddApp(version: "0.1.0-beta.1"));
        Publish(ReleaseChannel.Beta, catalog.AddApp(version: "0.1.0-beta.2"));

        JsonNode app = Read("catalog-beta.json")["apps"]![0]!;
        Assert.Equal("0.1.0-beta.2", (string?)app["latestVersion"]);
        Assert.Equal(["0.1.0-beta.2", "0.1.0-beta.1"], Versions(app));
        Assert.False(File.Exists(Path.Combine(Output, "catalog.json")));
    }

    [Fact]
    public void Beta_versions_keep_through_a_stable_publication_until_a_newer_stable_version_replaces_them()
    {
        Publish(ReleaseChannel.Stable, catalog.AddApp(version: "1.0.0"));
        Publish(ReleaseChannel.Beta, catalog.AddApp(version: "1.0.1-beta.1"));
        Publish(ReleaseChannel.Stable, catalog.AddApp(appId: "org.example.other", version: "2.0.0"));

        Assert.Equal(["1.0.1-beta.1", "1.0.0"], Versions(App("org.example.notes")));

        Publish(ReleaseChannel.Stable, catalog.AddApp(version: "1.0.1"));

        Assert.Equal(["1.0.1", "1.0.0"], Versions(App("org.example.notes")));
        Assert.False(Directory.Exists(Path.Combine(Output, "packages/org.example.notes/1.0.1-beta.1")));
        Assert.False(Directory.Exists(Path.Combine(Output, "assets/org.example.notes/1.0.1-beta.1")));
    }

    [Fact]
    public void Beta_versions_have_their_own_retention()
    {
        foreach (string version in new[] { "1.0.0", "1.1.0", "1.2.0" })
        {
            Publish(ReleaseChannel.Stable, catalog.AddApp(version: version));
        }

        foreach (string version in new[] { "1.2.1-beta.1", "1.2.1-beta.2", "1.2.1-beta.3", "1.2.1-beta.4" })
        {
            Publish(ReleaseChannel.Beta, catalog.AddApp(version: version));
        }

        Assert.Equal(["1.2.1-beta.4", "1.2.1-beta.3", "1.2.1-beta.2", "1.2.0", "1.1.0", "1.0.0"], Versions(App("org.example.notes")));
        Assert.False(Directory.Exists(Path.Combine(Output, "packages/org.example.notes/1.2.1-beta.1")));
        Assert.True(Directory.Exists(Path.Combine(Output, "packages/org.example.notes/1.0.0")));
    }

    [Fact]
    public void Each_branch_publishes_only_its_own_channel()
    {
        List<CatalogError> errors = Publish(ReleaseChannel.Stable, catalog.AddApp(version: "1.0.1-beta.1"), expectSuccess: false);
        Assert.Contains(errors, e => e.Message.Contains("beta branch", StringComparison.Ordinal));

        errors = Publish(ReleaseChannel.Beta, catalog.AddApp(version: "1.0.1"), expectSuccess: false);
        Assert.Contains(errors, e => e.Message.Contains("submit it to main", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Output));
    }

    [Fact]
    public void A_beta_version_older_than_the_newest_stable_version_is_refused()
    {
        Publish(ReleaseChannel.Stable, catalog.AddApp(version: "1.0.1"));

        List<CatalogError> errors = Publish(ReleaseChannel.Beta, catalog.AddApp(version: "1.0.1-beta.3"), expectSuccess: false);

        Assert.Contains(errors, e => e.Message.Contains("already published", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(Output, "packages/org.example.notes/1.0.1-beta.3")));
    }

    [Fact]
    public void A_beta_version_is_signed_with_the_same_key()
    {
        string key = PackageSigner.CreateKeyPair().PrivateKeyPem;
        Publish(ReleaseChannel.Beta, catalog.AddApp(version: "1.0.1-beta.1"), signingKey: key);

        Assert.False(string.IsNullOrEmpty((string?)App("org.example.notes")["versions"]![0]!["signature"]));
    }

    private List<CatalogError> Publish(ReleaseChannel channel, string folder, bool expectSuccess = true, string? signingKey = null)
    {
        if (Directory.Exists(Built))
        {
            Directory.Delete(Built, recursive: true);
        }

        string target = Path.Combine(Built, Path.GetFileName(folder));
        Directory.CreateDirectory(target);
        File.WriteAllBytes(Path.Combine(target, "app.kazoapp"), TestCatalog.PackageOf(folder));
        List<CatalogError> errors = [];
        CatalogPublisher.Publish(catalog.Root, Built, Output, 3, Now, errors, signingKey, channel);
        if (expectSuccess)
        {
            Assert.Empty(errors);
        }

        return errors;
    }

    private JsonNode Read(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(Output, file)))!;

    private JsonNode App(string appId) =>
        Read("catalog-beta.json")["apps"]!.AsArray().Single(app => (string?)app!["appId"] == appId)!;

    private static IEnumerable<string?> Versions(JsonNode app) =>
        app["versions"]!.AsArray().Select(v => (string?)v!["version"]);
}
