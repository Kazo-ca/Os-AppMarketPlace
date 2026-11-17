namespace Kazo.Os.MarketPlace.Tests;

/// <summary>
/// The Store refuses a feature whose drive provider declaration is invalid (ADR 0282 §1 and §4,
/// ADR 0284 §2 of Kazo-ca/Os) before anything is built or signed. The rules are those of the
/// Kazo OS manifest validator the catalog tool is built with: these tests pin that the catalog
/// applies them, one submission per refused rule.
/// </summary>
public sealed class DriveProviderSubmissionTests : IDisposable
{
    private const string AppId = "org.example.drives";
    private const string Version = "1.0.0";
    private const string DrivesProvide = "drives.provide";

    private readonly TestCatalog catalog = new();

    public void Dispose() => catalog.Dispose();

    [Fact]
    public void A_feature_with_a_valid_form_provider_is_accepted()
    {
        Assert.Empty(Validate(Provider()));
    }

    [Fact]
    public void A_feature_with_a_valid_oauth_provider_is_accepted()
    {
        const string oauth = """
            [{"type":"exampledrive","name":"Example","provider":"Example.Drives.ExampleProvider, Example.Drives","hosts":["api.example.org","*.content.example.org"],
              "connection":{"kind":"oauth","oauth":{"authorizationEndpoint":"https://example.org/oauth/authorize","tokenEndpoint":"https://api.example.org/oauth/token",
              "fullAccess":{"clientId":"public-app-id"}}}}]
            """;

        Assert.Empty(Validate(oauth));
    }

    [Theory]
    [InlineData("dropbox")]
    [InlineData("onedrive")]
    [InlineData("s3")]
    [InlineData("hostfs")]
    [InlineData("tmpfs")]
    public void A_type_the_os_keeps_for_itself_is_refused(string type)
    {
        AssertRefused(Validate(Provider(type: type)), "manifest.drive-provider.reserved");
    }

    [Theory]
    [InlineData("Example")]
    [InlineData("1drive")]
    [InlineData("example_drive")]
    public void A_type_that_is_not_a_filesystem_type_is_refused(string type)
    {
        AssertRefused(Validate(Provider(type: type)), "manifest.drive-provider.type");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://dav.example.org")]
    [InlineData("dav.example.org:8080")]
    [InlineData("dav.example.org/path")]
    public void A_host_that_is_not_an_https_name_or_a_bare_wildcard_is_refused(string host)
    {
        AssertRefused(Validate(Provider(hosts: $"[\"{host}\"]")), "manifest.drive-provider.host");
    }

    [Fact]
    public void An_oauth_address_that_is_not_https_is_refused()
    {
        const string oauth = """
            [{"type":"exampledrive","name":"Example","provider":"Example.Drives.ExampleProvider, Example.Drives","hosts":["api.example.org"],
              "connection":{"kind":"oauth","oauth":{"authorizationEndpoint":"http://example.org/oauth/authorize","tokenEndpoint":"https://api.example.org/oauth/token",
              "fullAccess":{"clientId":"public-app-id"}}}}]
            """;

        AssertRefused(Validate(oauth), "manifest.drive-provider.address");
    }

    [Theory]
    [InlineData("""{"name":"address","label":"Address","kind":"url","secret":true,"required":true,"serverAddress":true}""")]
    [InlineData("""{"name":"address","label":"Address","kind":"text","secret":false,"required":true,"serverAddress":true}""")]
    [InlineData("""{"name":"address","label":"Address","kind":"url","secret":false,"required":false,"serverAddress":true}""")]
    public void A_server_address_field_that_is_not_a_required_public_url_is_refused(string field)
    {
        AssertRefused(Validate(Provider(fields: field)), "manifest.drive-provider.server-address");
    }

    [Fact]
    public void Two_server_address_fields_are_refused()
    {
        const string fields = """
            {"name":"address","label":"Address","kind":"url","secret":false,"required":true,"serverAddress":true},
            {"name":"mirror","label":"Mirror","kind":"url","secret":false,"required":true,"serverAddress":true}
            """;

        AssertRefused(Validate(Provider(fields: fields)), "manifest.drive-provider.server-address");
    }

    [Fact]
    public void A_provider_without_the_drives_provide_capability_is_refused()
    {
        AssertRefused(Validate(Provider(), capabilities: []), "manifest.drive-provider.capability");
    }

    [Fact]
    public void Only_a_feature_may_provide_a_drive_type()
    {
        AssertRefused(Validate(Provider(), capabilities: [], kind: "service"), "manifest.feature.only");
    }

    [Fact]
    public void Only_a_feature_may_ask_for_drives_provide()
    {
        AssertRefused(Validate("[]", kind: "service"), "manifest.feature.only");
    }

    [Fact]
    public void A_type_declared_twice_is_refused()
    {
        string provider = Provider().Trim()[1..^1];

        AssertRefused(Validate($"[{provider},{provider}]"), "manifest.drive-provider.duplicate");
    }

    [Fact]
    public void A_type_another_app_of_the_catalog_provides_is_refused()
    {
        AddFeature("org.example.first", "1.0.0");
        string second = AddFeature("org.example.second", "1.0.0");

        List<CatalogError> errors = [];
        DriveTypeOwnership.Check(catalog.Apps, [second], errors);

        CatalogError error = Assert.Single(errors);
        Assert.Equal($"apps/{second}/manifest.json", error.Path);
        Assert.StartsWith("The drive type 'exampledav' is already provided by 'org.example.first'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_app_that_owns_a_type_keeps_it_in_its_later_versions()
    {
        AddFeature("org.example.first", "1.0.0");
        string next = AddFeature("org.example.first", "1.1.0");

        List<CatalogError> errors = [];
        DriveTypeOwnership.Check(catalog.Apps, [next], errors);

        Assert.Empty(errors);
    }

    [Fact]
    public void Two_apps_of_one_pull_request_may_not_share_a_type()
    {
        string first = AddFeature("org.example.first", "1.0.0");
        string second = AddFeature("org.example.second", "1.0.0");

        List<CatalogError> errors = [];
        DriveTypeOwnership.Check(catalog.Apps, [first, second], errors);

        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.Contains("declared by several apps of this pull request", error.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void The_whole_catalog_check_refuses_a_type_provided_by_two_apps()
    {
        AddFeature("org.example.first", "1.0.0");
        AddFeature("org.example.first", "1.1.0");
        AddFeature("org.example.other", "1.0.0", type: "otherdav");

        List<CatalogError> errors = [];
        DriveTypeOwnership.Check(catalog.Apps, [], errors);
        Assert.Empty(errors);

        AddFeature("org.example.second", "1.0.0");
        DriveTypeOwnership.Check(catalog.Apps, [], errors);
        Assert.Contains("'org.example.first', 'org.example.second'", Assert.Single(errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_validate_command_refuses_a_submission_whose_type_is_taken()
    {
        AddFeature("org.example.first", "1.0.0");
        string second = AddFeature("org.example.second", "1.0.0", type: "otherdav");
        Assert.Equal(0, Program.Main(["validate", "--root", catalog.Root, second]));

        string third = AddFeature("org.example.third", "1.0.0");
        Assert.Equal(1, Program.Main(["validate", "--root", catalog.Root, third]));
    }

    private string AddFeature(string appId, string version, string type = "exampledav")
    {
        string manifest = TestCatalog.FeatureManifest(appId, version, [DrivesProvide], Provider(type: type));
        return Path.GetFileName(catalog.AddApp(appId, version, manifest, "feature"));
    }

    /// <summary>A WebDAV-like form provider; each argument replaces one part of a valid declaration.</summary>
    private static string Provider(string type = "exampledav", string hosts = "[\"*.example.org\"]", string? fields = null)
    {
        fields ??= """
            {"name":"address","label":"Address","kind":"url","secret":false,"required":true,"serverAddress":true},
            {"name":"password","label":"Password","kind":"text","secret":true,"required":true}
            """;
        return $$$"""
            [{"type":"{{{type}}}","name":"Example DAV","provider":"Example.Drives.ExampleDavProvider, Example.Drives","hosts":{{{hosts}}},
              "connection":{"kind":"form","fields":[{{{fields}}}]}}]
            """;
    }

    private List<CatalogError> Validate(string driveProviders, string[]? capabilities = null, string kind = "feature")
    {
        string manifest = TestCatalog.FeatureManifest(AppId, Version, capabilities ?? [DrivesProvide], driveProviders, kind);
        return catalog.Validate(catalog.AddApp(AppId, Version, manifest, kind == "feature" ? "feature" : null));
    }

    private static void AssertRefused(List<CatalogError> errors, string code)
    {
        Assert.Contains(errors, error =>
            error.Path.EndsWith("/manifest.json", StringComparison.Ordinal)
            && error.Message.StartsWith($"{code} at ", StringComparison.Ordinal));
    }
}
