using Kazo.Os.App.Abstractions;
using Kazo.Os.Platform.Core.Apps.Packages;

namespace Kazo.Os.MarketPlace;

/// <summary>
/// Updates the content of the <c>packages</c> branch after a merge (ADR 0188 §6): copies each
/// newly built package and its assets, rewrites <c>catalog.json</c>, and keeps only the newest
/// versions of each app, then rewrites the public list of the apps (<see cref="AppsPage"/>).
/// Each publication also rewrites <c>catalog-beta.json</c> (<see cref="BetaCatalog"/>, ADR 0271 §5
/// of Kazo-ca/Os): from <c>main</c> with the new stable catalog, from <c>beta</c> with the new beta
/// versions, leaving <c>catalog.json</c> and the list of the apps as they are.
/// Nothing is written when one folder fails its checks.
/// </summary>
public static class CatalogPublisher
{
    /// <summary>Publishes built folders into a checkout of the <c>packages</c> branch.</summary>
    /// <param name="catalogRoot">Checkout of <c>main</c> (folders and <c>categories.json</c>).</param>
    /// <param name="builtRoot">
    /// One sub-folder per app folder name, holding the <c>.kazoapp</c> our CI built. Missing or empty
    /// when only <c>categories.json</c> changed: the catalog is then rewritten with the new labels.
    /// </param>
    /// <param name="outputRoot">Checkout of the <c>packages</c> branch, updated in place.</param>
    /// <param name="keptVersions">Versions of each app kept downloadable.</param>
    /// <param name="publishedAt">Publication time written for the new versions.</param>
    /// <param name="errors">Receives every problem found.</param>
    /// <param name="signingKeyPem">
    /// Private key (PKCS#8 PEM) that signs each new package (<see cref="PackageSigner"/>), or
    /// <see langword="null"/> to publish unsigned packages. Versions already published keep their signature.
    /// </param>
    /// <param name="channel">
    /// Channel of the branch that publishes: <see cref="ReleaseChannel.Stable"/> from <c>main</c>,
    /// <see cref="ReleaseChannel.Beta"/> from <c>beta</c>. Every built folder must belong to it.
    /// </param>
    /// <returns>The names of the folders published.</returns>
    public static IReadOnlyList<string> Publish(
        string catalogRoot,
        string builtRoot,
        string outputRoot,
        int keptVersions,
        DateTimeOffset publishedAt,
        List<CatalogError> errors,
        string? signingKeyPem = null,
        ReleaseChannel channel = ReleaseChannel.Stable)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(keptVersions, 1);
        CategoryList categories = CategoryList.Load(Path.Combine(catalogRoot, CatalogPaths.CategoriesFile), CatalogPaths.CategoriesFile, errors);
        List<(AppFolder Folder, byte[] Bytes, AppPackage Package)> ready = [];
        IEnumerable<string> builtFolders = Directory.Exists(builtRoot) ? Directory.EnumerateDirectories(builtRoot) : [];
        foreach (string built in builtFolders.Order(StringComparer.Ordinal))
        {
            (AppFolder Folder, byte[] Bytes, AppPackage Package)? item = Prepare(catalogRoot, built, categories, errors);
            if (item is not null)
            {
                ReleaseChannels.Check(item.Value.Folder, channel, errors);
                ready.Add(item.Value);
            }
        }

        if (errors.Count > 0)
        {
            return [];
        }

        return channel == ReleaseChannel.Beta
            ? PublishBeta(outputRoot, categories, ready, keptVersions, publishedAt, errors, signingKeyPem)
            : PublishStable(outputRoot, categories, ready, keptVersions, publishedAt, signingKeyPem);
    }

    private static IReadOnlyList<string> PublishStable(
        string outputRoot,
        CategoryList categories,
        List<(AppFolder Folder, byte[] Bytes, AppPackage Package)> ready,
        int keptVersions,
        DateTimeOffset publishedAt,
        string? signingKeyPem)
    {
        string catalogPath = Path.Combine(outputRoot, CatalogDocument.FileName);
        CatalogDocument document = File.Exists(catalogPath) ? CatalogDocument.Read(File.ReadAllText(catalogPath)) : CatalogDocument.Empty;
        Dictionary<string, CatalogApp> apps = document.Apps.ToDictionary(app => app.AppId, StringComparer.Ordinal);
        foreach ((AppFolder folder, byte[] bytes, AppPackage _) in ready)
        {
            CatalogVersion version = CopyFiles(folder, bytes, outputRoot, publishedAt) with
            {
                Signature = signingKeyPem is null ? null : PackageSigner.Sign(bytes, signingKeyPem),
            };
            apps[folder.AppId] = Merge(apps.GetValueOrDefault(folder.AppId), folder, version);
        }

        List<CatalogApp> pruned = [.. apps.Values
            .OrderBy(app => app.AppId, StringComparer.Ordinal)
            .Select(app => Prune(app, outputRoot, keptVersions))
            .Select(app => AddMissingFileHandlers(app, outputRoot))];
        Directory.CreateDirectory(outputRoot);
        CatalogDocument written = new(CatalogDocument.CurrentSchemaVersion, categories.Categories, pruned);
        File.WriteAllText(catalogPath, written.Write());

        // The public list of the apps (APPS.md, APPS-fr.md), linked from the READMEs, follows every publication.
        AppsPage.Write(written, outputRoot);

        // The beta channel carries all of the stable one: it follows every stable publication, and
        // drops the beta versions a new stable version replaces.
        BetaCatalog.Write(outputRoot, written, written.Categories, BetaCatalog.ReadBetaApps(outputRoot), keptVersions);
        return [.. ready.Select(item => item.Folder.Name)];
    }

    /// <summary>
    /// Publishes beta versions: their packages and assets, then <c>catalog-beta.json</c>.
    /// <c>catalog.json</c> and the list of the apps are read, never written.
    /// </summary>
    private static IReadOnlyList<string> PublishBeta(
        string outputRoot,
        CategoryList categories,
        List<(AppFolder Folder, byte[] Bytes, AppPackage Package)> ready,
        int keptVersions,
        DateTimeOffset publishedAt,
        List<CatalogError> errors,
        string? signingKeyPem)
    {
        string catalogPath = Path.Combine(outputRoot, CatalogDocument.FileName);
        CatalogDocument stable = File.Exists(catalogPath) ? CatalogDocument.Read(File.ReadAllText(catalogPath)) : CatalogDocument.Empty;
        Dictionary<string, CatalogApp> stableApps = stable.Apps.ToDictionary(app => app.AppId, StringComparer.Ordinal);
        foreach ((AppFolder folder, byte[] _, AppPackage _) in ready)
        {
            // A beta version older than a published stable version would leave at once.
            if (stableApps.TryGetValue(folder.AppId, out CatalogApp? app) && SemanticVersion.Parse(app.LatestVersion).CompareTo(folder.Version) > 0)
            {
                errors.Add(new CatalogError(
                    $"{CatalogPaths.AppsFolder}/{folder.Name}",
                    $"{folder.AppId} {app.LatestVersion} is already published: a beta version must be newer than the newest stable version."));
            }
        }

        if (errors.Count > 0)
        {
            return [];
        }

        Dictionary<string, CatalogApp> betaApps = BetaCatalog.ReadBetaApps(outputRoot);
        foreach ((AppFolder folder, byte[] bytes, AppPackage _) in ready)
        {
            CatalogVersion version = CopyFiles(folder, bytes, outputRoot, publishedAt) with
            {
                Signature = signingKeyPem is null ? null : PackageSigner.Sign(bytes, signingKeyPem),
            };

            // The listing fields of the beta entry only show for an app with no stable version.
            betaApps[folder.AppId] = Merge(betaApps.GetValueOrDefault(folder.AppId), folder, version);
        }

        // The categories of beta, which receives main: they are those of catalog.json, plus any new one.
        BetaCatalog.Write(outputRoot, stable, categories.Categories, betaApps, keptVersions);
        return [.. ready.Select(item => item.Folder.Name)];
    }

    private static (AppFolder, byte[], AppPackage)? Prepare(string catalogRoot, string built, CategoryList categories, List<CatalogError> errors)
    {
        string name = Path.GetFileName(built);
        string folderPath = Path.Combine(catalogRoot, CatalogPaths.AppsFolder, name);
        if (!Directory.Exists(folderPath))
        {
            errors.Add(new CatalogError($"{CatalogPaths.AppsFolder}/{name}", "A package was built for a folder that does not exist on main."));
            return null;
        }

        AppFolder? folder = AppFolderValidator.Validate(folderPath, categories, errors);
        string[] packages = Directory.GetFiles(built, "*" + AppPackage.Extension, SearchOption.AllDirectories);
        if (packages.Length != 1)
        {
            errors.Add(new CatalogError($"{CatalogPaths.AppsFolder}/{name}", $"Expected one built {AppPackage.Extension} package, found {packages.Length}."));
            return null;
        }

        if (folder is null)
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(packages[0]);
        AppPackage? package = PackageCheck.Check(folder, bytes, errors);
        return package is null ? null : (folder, bytes, package);
    }

    private static CatalogVersion CopyFiles(AppFolder folder, byte[] packageBytes, string outputRoot, DateTimeOffset publishedAt)
    {
        string version = folder.Version.ToString();
        string packagePath = $"{CatalogFiles.PackageFolder(folder.AppId, version)}/{folder.AppId}-{version}{AppPackage.Extension}";
        string assetsPath = CatalogFiles.AssetsFolder(folder.AppId, version);
        CatalogFiles.WriteFile(outputRoot, packagePath, packageBytes);

        string icon = $"{assetsPath}/{CatalogPaths.IconFile}";
        CatalogFiles.WriteFile(outputRoot, icon, File.ReadAllBytes(Path.Combine(folder.Path, CatalogPaths.IconFile)));
        List<string> screenshots = [];
        foreach (string screenshot in folder.Screenshots)
        {
            string target = $"{assetsPath}/{CatalogPaths.ScreenshotsFolder}/{screenshot}";
            CatalogFiles.WriteFile(outputRoot, target, File.ReadAllBytes(Path.Combine(folder.Path, CatalogPaths.ScreenshotsFolder, screenshot)));
            screenshots.Add(target);
        }

        Dictionary<string, string> descriptions = new(StringComparer.Ordinal);
        foreach ((string language, string fileName) in folder.DescriptionFiles.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            string target = $"{assetsPath}/{fileName}";
            CatalogFiles.WriteFile(outputRoot, target, File.ReadAllBytes(Path.Combine(folder.Path, fileName)));
            descriptions[language] = target;
        }

        AppSdkCompatibilityManifest sdk = folder.Manifest.SdkCompatibility;
        return new CatalogVersion(
            version,
            folder.Store.Commit,
            packagePath,
            AppPackage.Sha256(packageBytes),
            packageBytes.LongLength,
            publishedAt,
            [.. folder.Manifest.Capabilities.Order(StringComparer.Ordinal)],
            new CatalogSdkRange(sdk.MinimumVersion, sdk.MaximumVersion),
            icon,
            screenshots,
            descriptions,
            FileHandlers: CatalogFileHandler.From(folder.Manifest.FileHandlers));
    }

    private static CatalogApp Merge(CatalogApp? existing, AppFolder folder, CatalogVersion version)
    {
        List<CatalogVersion> versions = [.. (existing?.Versions ?? []).Where(v => v.Version != version.Version), version];
        versions.Sort((left, right) => SemanticVersion.Parse(right.Version).CompareTo(SemanticVersion.Parse(left.Version)));
        bool newest = versions[0] == version;
        if (existing is not null && !newest)
        {
            // An older version published late does not change how the app is listed.
            return existing with { Versions = versions };
        }

        return new CatalogApp(
            folder.AppId,
            "app",
            folder.Manifest.Name,
            folder.Store.Category,
            folder.Store.Tags,
            new SortedDictionary<string, string>(folder.ShortDescriptions.ToDictionary(), StringComparer.Ordinal),
            folder.Store.License,
            folder.Store.Repository,
            version.Version,
            versions);
    }

    private static CatalogApp Prune(CatalogApp app, string outputRoot, int keptVersions)
    {
        foreach (CatalogVersion removed in app.Versions.Skip(keptVersions))
        {
            // Older versions leave the packages branch; their folder stays in the history of main.
            CatalogFiles.DeleteVersion(outputRoot, app.AppId, removed.Version);
        }

        return app with { Versions = [.. app.Versions.Take(keptVersions)] };
    }

    /// <summary>
    /// Fills <see cref="CatalogVersion.FileHandlers"/> of the versions published before the field
    /// existed (ADR 0257 of Kazo-ca/Os), from the manifest inside their package on the
    /// <c>packages</c> branch. The package, its hash and its signature stay as they are: only
    /// <c>catalog.json</c> changes, so the next publication fills every version still kept.
    /// </summary>
    private static CatalogApp AddMissingFileHandlers(CatalogApp app, string outputRoot)
    {
        if (app.Versions.All(version => version.FileHandlers is not null))
        {
            return app;
        }

        return app with { Versions = [.. app.Versions.Select(version => version.FileHandlers is null ? version with { FileHandlers = ReadFileHandlers(outputRoot, version) } : version)] };
    }

    private static IReadOnlyList<CatalogFileHandler>? ReadFileHandlers(string outputRoot, CatalogVersion version)
    {
        string path = Path.Combine(outputRoot, version.Package);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return CatalogFileHandler.From(AppPackage.Read(File.ReadAllBytes(path)).Manifest.FileHandlers);
        }
        catch (AppPackageException)
        {
            // The package was checked when it was published; a reader that refuses it today changes
            // nothing in the catalog: the version simply announces no file type.
            return null;
        }
    }
}
