using System.Text.Json.Nodes;
using Kazo.Os.App.Abstractions;

namespace Kazo.Os.MarketPlace;

/// <summary>
/// The "rebuild only" check (ADR 0271 §5 of Kazo-ca/Os): a submission that only moves an app to a
/// new Kazo OS SDK brings no new code to read, so the automation may merge it once its validation
/// is green, on <c>main</c> and on <c>beta</c> (ADR 0271 Q5 = A). A submission is a rebuild only
/// when, against the newest version of the same app already in the branch (the "previous" one):
/// <list type="number">
/// <item>its <c>store.json</c> is the same except <c>commit</c>;</item>
/// <item>its <c>manifest.json</c> is the same except <c>version</c> and <c>sdkCompatibility</c>,
/// so it asks for the same capabilities;</item>
/// <item>its icon, descriptions and screenshots are the same bytes;</item>
/// <item>in the app repository, between the previous commit and the submitted one, only the Kazo OS
/// submodule (<c>external/KazoOs</c>) moved and lines changed only by the version (the
/// <c>Version</c> of the <c>[AppManifest]</c> class and of the committed <c>manifest.json</c>, as
/// <c>.github/scripts/bump-app-version.sh</c> writes them).</item>
/// </list>
/// Anything else waits for a human review, as before.
/// </summary>
public static class RebuildCheck
{
    /// <summary>Path of the Kazo OS submodule in an app repository (Kazo OS ADR 0176).</summary>
    public const string SubmodulePath = "external/KazoOs";

    // The manifest fields a rebuild for a new SDK changes by itself.
    private static readonly string[] RebuildManifestFields = ["version", "sdkCompatibility"];

    /// <summary>
    /// Finds the newest folder of the same app with a lower version, in the branch the
    /// submission goes to, or <see langword="null"/> for a first submission.
    /// </summary>
    /// <param name="appsRoot">The <c>apps</c> folder of the branch.</param>
    /// <param name="appId">The app id of the submission.</param>
    /// <param name="version">The version of the submission.</param>
    /// <returns>The folder name, <c>&lt;appId&gt;_&lt;version&gt;</c>.</returns>
    public static string? FindPrevious(string appsRoot, string appId, SemanticVersion version)
    {
        if (!Directory.Exists(appsRoot))
        {
            return null;
        }

        return Directory.EnumerateDirectories(appsRoot)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Select(name => AppFolderValidator.TrySplitName(name, out string id, out SemanticVersion v) ? (Name: name, Id: id, Version: v) : default)
            .Where(entry => entry.Name is not null && entry.Id == appId && entry.Version.CompareTo(version) < 0)
            .OrderByDescending(entry => entry.Version)
            .Select(entry => entry.Name)
            .FirstOrDefault();
    }

    /// <summary>Checks a submission against the previous version of its app.</summary>
    /// <param name="previous">The previous version, already in the branch.</param>
    /// <param name="submitted">The submitted version.</param>
    /// <param name="diff">
    /// <c>git diff --no-color --no-ext-diff --no-renames --unified=0</c> of the app repository from
    /// the previous commit to the submitted one.
    /// </param>
    /// <returns>Why it is not a rebuild only; empty when it is one.</returns>
    public static IReadOnlyList<string> Check(AppFolder previous, AppFolder submitted, string diff)
    {
        List<string> reasons = [];
        CompareStore(previous.Store, submitted.Store, reasons);
        CompareManifests(previous, submitted, reasons);
        CompareFiles(previous.Path, submitted.Path, reasons);
        CheckDiff(diff, previous.Version.ToString(), submitted.Version.ToString(), reasons);
        return reasons;
    }

    /// <summary>
    /// Checks the diff of the app repository: only the Kazo OS submodule moves, and every other
    /// changed line differs from the line it replaces by the version alone.
    /// </summary>
    /// <param name="diff">Unified diff with no context lines.</param>
    /// <param name="previousVersion">Version of the previous commit.</param>
    /// <param name="version">Version of the submitted commit.</param>
    /// <param name="reasons">Receives why the diff is more than a rebuild.</param>
    public static void CheckDiff(string diff, string previousVersion, string version, List<string> reasons)
    {
        foreach (DiffFile file in DiffFile.Parse(diff))
        {
            if (file.Unsupported is { } header)
            {
                reasons.Add($"`{file.Path}` changes in a way a rebuild never does ({header}).");
            }
            else if (file.Submodule)
            {
                if (file.Path != SubmodulePath)
                {
                    reasons.Add($"The submodule `{file.Path}` moves: only `{SubmodulePath}` may.");
                }
            }
            else if (!file.OnlyVersionChanges(previousVersion, version))
            {
                reasons.Add($"`{file.Path}` changes more than the version ({previousVersion} → {version}).");
            }
        }
    }

    private static void CompareStore(StoreEntry previous, StoreEntry submitted, List<string> reasons)
    {
        if (previous.Repository != submitted.Repository
            || previous.Project != submitted.Project
            || previous.Category != submitted.Category
            || previous.License != submitted.License
            || !previous.Tags.SequenceEqual(submitted.Tags, StringComparer.Ordinal))
        {
            reasons.Add("`store.json` changes more than `commit`.");
        }
    }

    private static void CompareManifests(AppFolder previous, AppFolder submitted, List<string> reasons)
    {
        if (!previous.Manifest.Capabilities.Order(StringComparer.Ordinal).SequenceEqual(submitted.Manifest.Capabilities.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            reasons.Add("The capabilities change.");
        }

        JsonNode? before = WithoutRebuildFields(Path.Combine(previous.Path, CatalogPaths.ManifestFile));
        JsonNode? after = WithoutRebuildFields(Path.Combine(submitted.Path, CatalogPaths.ManifestFile));
        if (!JsonNode.DeepEquals(before, after))
        {
            reasons.Add("`manifest.json` changes more than `version` and `sdkCompatibility`.");
        }
    }

    private static JsonNode? WithoutRebuildFields(string path)
    {
        JsonNode? node = JsonNode.Parse(File.ReadAllText(path));
        if (node is JsonObject manifest)
        {
            foreach (string field in RebuildManifestFields)
            {
                manifest.Remove(field);
            }
        }

        return node;
    }

    private static void CompareFiles(string previousFolder, string submittedFolder, List<string> reasons)
    {
        Dictionary<string, string> before = ListedFiles(previousFolder);
        Dictionary<string, string> after = ListedFiles(submittedFolder);
        foreach (string name in before.Keys.Union(after.Keys).Order(StringComparer.Ordinal))
        {
            bool same = before.TryGetValue(name, out string? left)
                && after.TryGetValue(name, out string? right)
                && File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right));
            if (!same)
            {
                reasons.Add($"`{name}` is added, removed or changed.");
            }
        }
    }

    // Every file of a folder but the two that a rebuild changes, by relative path.
    private static Dictionary<string, string> ListedFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => (Name: Path.GetRelativePath(folder, path).Replace('\\', '/'), Path: path))
            .Where(file => file.Name is not CatalogPaths.ManifestFile and not CatalogPaths.StoreFile)
            .ToDictionary(file => file.Name, file => file.Path, StringComparer.Ordinal);

    /// <summary>One file of a unified diff.</summary>
    private sealed class DiffFile
    {
        private readonly List<(List<string> Removed, List<string> Added)> hunks = [];

        private DiffFile(string path) => Path = path;

        public string Path { get; }

        public bool Submodule { get; private set; }

        /// <summary>Gets the header line of a change a rebuild never makes (new file, mode, binary…).</summary>
        public string? Unsupported { get; private set; }

        public static List<DiffFile> Parse(string diff)
        {
            List<DiffFile> files = [];
            DiffFile? current = null;
            bool inHunk = false;
            foreach (string line in diff.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    current = new DiffFile(PathOf(line));
                    files.Add(current);
                    inHunk = false;
                    continue;
                }

                if (current is null || line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("@@", StringComparison.Ordinal))
                {
                    current.hunks.Add(([], []));
                    inHunk = true;
                }
                else if (inHunk && line[0] == '-')
                {
                    current.hunks[^1].Removed.Add(line[1..]);
                }
                else if (inHunk && line[0] == '+')
                {
                    current.hunks[^1].Added.Add(line[1..]);
                }
                else if (inHunk && line[0] == '\\')
                {
                    // "\ No newline at end of file".
                }
                else if (!inHunk)
                {
                    current.ReadHeader(line);
                }
            }

            return files;
        }

        public bool OnlyVersionChanges(string previousVersion, string version)
        {
            foreach ((List<string> removed, List<string> added) in hunks)
            {
                if (removed.Count != added.Count)
                {
                    return false;
                }

                for (int index = 0; index < removed.Count; index++)
                {
                    if (!removed[index].Contains(previousVersion, StringComparison.Ordinal)
                        || removed[index].Replace(previousVersion, version, StringComparison.Ordinal) != added[index])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static string PathOf(string header)
        {
            // "diff --git a/<path> b/<path>": renames are off (--no-renames), so both paths are equal.
            string rest = header["diff --git ".Length..];
            int middle = rest.IndexOf(" b/", StringComparison.Ordinal);
            string path = middle > 0 ? rest[..middle] : rest;
            return path.StartsWith("a/", StringComparison.Ordinal) ? path[2..] : path;
        }

        private void ReadHeader(string line)
        {
            if (line.StartsWith("index ", StringComparison.Ordinal))
            {
                // "index <old>..<new> 160000": a submodule (gitlink).
                Submodule |= line.EndsWith(" 160000", StringComparison.Ordinal);
            }
            else if (line.StartsWith("new file mode", StringComparison.Ordinal)
                || line.StartsWith("deleted file mode", StringComparison.Ordinal)
                || line.StartsWith("old mode", StringComparison.Ordinal)
                || line.StartsWith("new mode", StringComparison.Ordinal)
                || line.StartsWith("Binary files", StringComparison.Ordinal)
                || line.StartsWith("GIT binary patch", StringComparison.Ordinal)
                || line.StartsWith("rename ", StringComparison.Ordinal)
                || line.StartsWith("copy ", StringComparison.Ordinal)
                || line.StartsWith("similarity index", StringComparison.Ordinal))
            {
                Unsupported ??= line;
            }
        }
    }
}
