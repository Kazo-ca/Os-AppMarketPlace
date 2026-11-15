using Kazo.Os.App.Abstractions;

namespace Kazo.Os.MarketPlace;

/// <summary>
/// <c>catalog-beta.json</c> at the root of the <c>packages</c> branch (ADR 0271 §5 of Kazo-ca/Os),
/// read by a pre-release of Kazo OS. Same format as <c>catalog.json</c> (<see cref="CatalogDocument"/>,
/// schema version 1): all of <c>catalog.json</c>, plus the beta versions of the apps.
/// <list type="bullet">
/// <item>An app keeps the listing fields and <c>latestVersion</c> of <c>catalog.json</c>: a beta
/// version never changes how an app is listed. An app that has only beta versions is listed from
/// its newest one.</item>
/// <item>Versions are sorted newest first in SemVer order (<c>1.0.1</c> &gt; <c>1.0.1-beta.2</c>
/// &gt; <c>1.0.0</c>).</item>
/// <item>Beta versions have their own retention (the newest <c>keep</c> of each app) and do not
/// count in the stable ones. A beta version leaves, package and assets included, as soon as a
/// newer stable version of its app is published.</item>
/// </list>
/// Both publications rewrite it: <c>main</c> after <c>catalog.json</c>, <c>beta</c> after adding
/// its new versions. The beta versions are kept in this file only, between two publications.
/// </summary>
public static class BetaCatalog
{
    /// <summary>File name at the root of the <c>packages</c> branch.</summary>
    public const string FileName = "catalog-beta.json";

    /// <summary>
    /// Reads the beta versions already published, by app id. Each entry keeps the listing fields
    /// of <c>catalog-beta.json</c>, used only for an app that has no stable version.
    /// </summary>
    /// <param name="outputRoot">Checkout of the <c>packages</c> branch.</param>
    public static Dictionary<string, CatalogApp> ReadBetaApps(string outputRoot)
    {
        string path = Path.Combine(outputRoot, FileName);
        CatalogDocument document = File.Exists(path) ? CatalogDocument.Read(File.ReadAllText(path)) : CatalogDocument.Empty;
        Dictionary<string, CatalogApp> apps = new(StringComparer.Ordinal);
        foreach (CatalogApp app in document.Apps)
        {
            List<CatalogVersion> betas = [.. app.Versions.Where(version => ReleaseChannels.Of(version.Version) == ReleaseChannel.Beta)];
            if (betas.Count > 0)
            {
                apps[app.AppId] = app with { LatestVersion = betas[0].Version, Versions = betas };
            }
        }

        return apps;
    }

    /// <summary>
    /// Writes <c>catalog-beta.json</c> from the stable catalog and the beta versions, and removes
    /// the package and assets of every beta version that leaves.
    /// </summary>
    /// <param name="outputRoot">Checkout of the <c>packages</c> branch, updated in place.</param>
    /// <param name="stable">The stable catalog, as written in <c>catalog.json</c>.</param>
    /// <param name="categories">Categories with their labels.</param>
    /// <param name="betaApps">Beta versions by app id (<see cref="ReadBetaApps"/>, plus the new ones).</param>
    /// <param name="keptBetaVersions">Beta versions of each app kept downloadable.</param>
    /// <returns>The document written.</returns>
    public static CatalogDocument Write(
        string outputRoot,
        CatalogDocument stable,
        IReadOnlyList<CatalogCategory> categories,
        IReadOnlyDictionary<string, CatalogApp> betaApps,
        int keptBetaVersions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(keptBetaVersions, 1);
        Dictionary<string, CatalogApp> stableApps = stable.Apps.ToDictionary(app => app.AppId, StringComparer.Ordinal);
        List<CatalogApp> apps = [];
        foreach (string appId in stableApps.Keys.Union(betaApps.Keys).Order(StringComparer.Ordinal))
        {
            CatalogApp? stableApp = stableApps.GetValueOrDefault(appId);
            List<CatalogVersion> betas = Newest(betaApps.GetValueOrDefault(appId)?.Versions ?? []);
            SemanticVersion? newestStable = stableApp?.Versions.Count > 0 ? SemanticVersion.Parse(stableApp.LatestVersion) : null;
            List<CatalogVersion> kept = [];
            foreach (CatalogVersion beta in betas)
            {
                bool replaced = newestStable is { } released && released.CompareTo(SemanticVersion.Parse(beta.Version)) > 0;
                if (replaced || kept.Count >= keptBetaVersions)
                {
                    CatalogFiles.DeleteVersion(outputRoot, appId, beta.Version);
                    continue;
                }

                kept.Add(beta);
            }

            if (stableApp is not null)
            {
                apps.Add(stableApp with { Versions = Newest([.. stableApp.Versions, .. kept]) });
            }
            else if (kept.Count > 0)
            {
                apps.Add(betaApps[appId] with { LatestVersion = kept[0].Version, Versions = kept });
            }
        }

        CatalogDocument written = new(CatalogDocument.CurrentSchemaVersion, categories, apps);
        Directory.CreateDirectory(outputRoot);
        File.WriteAllText(Path.Combine(outputRoot, FileName), written.Write());
        return written;
    }

    private static List<CatalogVersion> Newest(IEnumerable<CatalogVersion> versions)
    {
        List<CatalogVersion> sorted = [.. versions];
        sorted.Sort((left, right) => SemanticVersion.Parse(right.Version).CompareTo(SemanticVersion.Parse(left.Version)));
        return sorted;
    }
}
