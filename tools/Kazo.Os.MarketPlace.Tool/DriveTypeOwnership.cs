using System.Text.Json;
using Kazo.Os.App.Abstractions;

namespace Kazo.Os.MarketPlace;

/// <summary>
/// One type of network drive belongs to one app of the catalog (ADR 0282 of Kazo-ca/Os). The OS
/// already refuses, at install time, a second feature that provides a type another installed
/// feature provides; the catalog refuses it at submission instead, so the user never meets that
/// failure. The app published first keeps the type, and its later versions keep it too (Benoit's
/// decision, 2026-11-17).
/// </summary>
public static class DriveTypeOwnership
{
    /// <summary>Checks the drive types of the catalog.</summary>
    /// <param name="appsRoot">The <c>apps/</c> folder of the catalog.</param>
    /// <param name="added">
    /// The folders a pull request adds. Their types may not belong to another app of the folders
    /// already in the branch, nor to another app added by the same pull request. When empty, the
    /// whole catalog is checked: no type may be provided by two apps.
    /// </param>
    /// <param name="errors">Receives one error per folder that provides a type of another app.</param>
    public static void Check(string appsRoot, IReadOnlyCollection<string> added, List<CatalogError> errors)
    {
        HashSet<string> addedNames = new(added, StringComparer.Ordinal);
        List<(string Folder, string AppId, string Type)> declarations = Read(appsRoot);
        foreach (IGrouping<string, (string Folder, string AppId, string Type)> type in declarations.GroupBy(static item => item.Type, StringComparer.Ordinal))
        {
            string[] established = [.. type.Where(item => !addedNames.Contains(item.Folder)).Select(static item => item.AppId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
            if (addedNames.Count == 0)
            {
                if (established.Length > 1)
                {
                    errors.Add(new CatalogError(
                        CatalogPaths.AppsFolder,
                        $"The drive type '{type.Key}' is provided by {string.Join(", ", established.Select(static id => $"'{id}'"))}: a type belongs to one app of the catalog (ADR 0282)."));
                }

                continue;
            }

            string[] newcomers = [.. type.Where(item => addedNames.Contains(item.Folder)).Select(static item => item.AppId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
            foreach ((string folder, string appId, _) in type.Where(item => addedNames.Contains(item.Folder)))
            {
                string? owner = established.FirstOrDefault(id => id != appId);
                if (owner is not null)
                {
                    errors.Add(new CatalogError(
                        $"{CatalogPaths.AppsFolder}/{folder}/{CatalogPaths.ManifestFile}",
                        $"The drive type '{type.Key}' is already provided by '{owner}': the app published first keeps a type (ADR 0282). Choose another type."));
                }
                else if (established.Length == 0 && newcomers.Length > 1)
                {
                    errors.Add(new CatalogError(
                        $"{CatalogPaths.AppsFolder}/{folder}/{CatalogPaths.ManifestFile}",
                        $"The drive type '{type.Key}' is declared by several apps of this pull request ({string.Join(", ", newcomers.Select(static id => $"'{id}'"))}): submit them separately."));
                }
            }
        }
    }

    /// <summary>
    /// The drive types each folder declares. A manifest that cannot be read is skipped: the folder
    /// check reports it.
    /// </summary>
    private static List<(string Folder, string AppId, string Type)> Read(string appsRoot)
    {
        List<(string, string, string)> declarations = [];
        if (!Directory.Exists(appsRoot))
        {
            return declarations;
        }

        foreach (string folder in Directory.EnumerateDirectories(appsRoot).Order(StringComparer.Ordinal))
        {
            string path = Path.Combine(folder, CatalogPaths.ManifestFile);
            if (!File.Exists(path))
            {
                continue;
            }

            AppManifest manifest;
            try
            {
                manifest = AppManifestJsonSerializer.DeserializeStrict(File.ReadAllText(path));
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
                continue;
            }

            foreach (AppDriveProviderManifest? provider in manifest.DriveProviders ?? [])
            {
                if (provider is { Type: { Length: > 0 } type })
                {
                    declarations.Add((Path.GetFileName(folder), manifest.Id, type));
                }
            }
        }

        return declarations;
    }
}
