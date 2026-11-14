using Kazo.Os.App.Abstractions;

namespace Kazo.Os.MarketPlace;

/// <summary>
/// Channel of the catalog a branch publishes (ADR 0271 §5 of Kazo-ca/Os): <c>main</c> publishes the
/// stable versions in <c>catalog.json</c>, <c>beta</c> the beta versions (SemVer with a suffix,
/// <c>1.0.1-beta.1</c>) in <c>catalog-beta.json</c>.
/// </summary>
public enum ReleaseChannel
{
    /// <summary>Versions without a suffix, from <c>main</c>.</summary>
    Stable,

    /// <summary>Versions with a suffix, from <c>beta</c>.</summary>
    Beta,
}

/// <summary>Helpers of <see cref="ReleaseChannel"/>.</summary>
public static class ReleaseChannels
{
    /// <summary>Name of the catalog branch that receives the beta versions.</summary>
    public const string BetaBranch = "beta";

    /// <summary>Reads <c>stable</c> or <c>beta</c> from the command line.</summary>
    /// <exception cref="ArgumentException">Any other value.</exception>
    public static ReleaseChannel Parse(string? value) => value switch
    {
        null or "stable" => ReleaseChannel.Stable,
        "beta" => ReleaseChannel.Beta,
        _ => throw new ArgumentException($"Unknown channel '{value}': use stable or beta."),
    };

    /// <summary>Gets the channel a version belongs to: beta when it has a suffix.</summary>
    public static ReleaseChannel Of(SemanticVersion version) =>
        version.PreRelease is null ? ReleaseChannel.Stable : ReleaseChannel.Beta;

    /// <summary>Gets the channel of a version written in the catalog.</summary>
    public static ReleaseChannel Of(string version) => Of(SemanticVersion.Parse(version));

    /// <summary>
    /// Checks that a version folder is on the branch of its channel: a version with a suffix is
    /// submitted to <c>beta</c>, any other one to <c>main</c>.
    /// </summary>
    public static void Check(AppFolder folder, ReleaseChannel channel, List<CatalogError> errors)
    {
        ReleaseChannel own = Of(folder.Version);
        if (own == channel)
        {
            return;
        }

        string display = $"{CatalogPaths.AppsFolder}/{folder.Name}";
        errors.Add(own == ReleaseChannel.Beta
            ? new CatalogError(display, $"{folder.Version} is a beta version (it has a suffix): submit it to the {BetaBranch} branch of the catalog, not to main.")
            : new CatalogError(display, $"{folder.Version} is a stable version: submit it to main. The {BetaBranch} branch publishes only versions with a suffix (1.0.1-beta.1); it receives the stable ones from main."));
    }
}
