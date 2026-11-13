using System.Globalization;
using System.Text;

namespace Kazo.Os.MarketPlace;

/// <summary>
/// The public list of the Store apps, <c>APPS.md</c> (English) and <c>APPS-fr.md</c> (French), written
/// at the root of the <c>packages</c> branch next to <c>catalog.json</c> each time the catalog is
/// published. GitHub renders them, so the READMEs of this repository and of Kazo-ca/Os link to them:
/// anyone can see what the Store offers without starting Kazo OS.
/// </summary>
/// <remarks>
/// The page is built from <see cref="CatalogDocument"/> only, so it always matches what the Store shows.
/// Its text depends on the catalog alone (no clock): rewriting an unchanged catalog leaves the page
/// unchanged and the publish workflow has nothing to commit. Names and descriptions come from app
/// authors, so they are escaped before they reach the Markdown.
/// </remarks>
public static class AppsPage
{
    /// <summary>File name of the English page.</summary>
    public const string EnglishFileName = "APPS.md";

    /// <summary>File name of the French page.</summary>
    public const string FrenchFileName = "APPS-fr.md";

    /// <summary>Address of Kazo OS; <c>?autolaunch=&lt;appId&gt;</c> opens an app there (ADR 0258 of Kazo-ca/Os).</summary>
    public const string OsAddress = "https://os.kazo.ca/";

    private const int IconSide = 48;

    /// <summary>Writes both pages into a checkout of the <c>packages</c> branch.</summary>
    /// <param name="document">The catalog just written.</param>
    /// <param name="outputRoot">Checkout of the <c>packages</c> branch.</param>
    public static void Write(CatalogDocument document, string outputRoot)
    {
        File.WriteAllText(Path.Combine(outputRoot, EnglishFileName), Render(document, PageText.English));
        File.WriteAllText(Path.Combine(outputRoot, FrenchFileName), Render(document, PageText.French));
    }

    /// <summary>Renders the page in one language.</summary>
    /// <param name="document">The catalog.</param>
    /// <param name="text">The fixed text of the page, in its language.</param>
    /// <returns>The Markdown of the page, ending with a new line.</returns>
    public static string Render(CatalogDocument document, PageText text)
    {
        StringBuilder page = new();
        page.Append(CultureInfo.InvariantCulture, $"> [{text.OtherLanguageLink}]({text.OtherLanguageFile})\n\n");
        page.Append(CultureInfo.InvariantCulture, $"# {text.Title}\n\n");
        page.Append(text.Introduction).Append("\n\n");

        if (document.Apps.Count == 0)
        {
            page.Append(text.NoApp).Append('\n');
            return page.ToString();
        }

        page.Append(Count(document.Apps.Count, text));
        DateTimeOffset[] dates = [.. document.Apps.SelectMany(app => app.Versions).Select(version => version.PublishedAt)];
        if (dates.Length > 0)
        {
            page.Append(CultureInfo.InvariantCulture, $" {text.LastPublication} {dates.Max().UtcDateTime:yyyy-MM-dd}.");
        }

        page.Append("\n\n");
        page.Append(CultureInfo.InvariantCulture, $"| | {text.AppHeader} | {text.DescriptionHeader} | {text.VersionHeader} | {text.CategoryHeader} | {text.LinksHeader} |\n");
        page.Append("| --- | --- | --- | --- | --- | --- |\n");
        IEnumerable<CatalogApp> apps = document.Apps
            .OrderBy(app => app.Name, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(app => app.AppId, StringComparer.Ordinal);
        foreach (CatalogApp app in apps)
        {
            page.Append(Row(app, document.Categories, text)).Append('\n');
        }

        page.Append('\n').Append(text.Footer).Append('\n');
        return page.ToString();
    }

    private static string Count(int count, PageText text) =>
        count == 1 ? text.OneApp : string.Format(CultureInfo.InvariantCulture, text.ManyApps, count);

    private static string Row(CatalogApp app, IReadOnlyList<CatalogCategory> categories, PageText text)
    {
        CatalogVersion? latest = app.Versions.FirstOrDefault(version => version.Version == app.LatestVersion) ?? app.Versions.FirstOrDefault();
        string icon = latest is null ? string.Empty : $"<img src=\"{latest.Icon}\" width=\"{IconSide}\" height=\"{IconSide}\" alt=\"\">";
        string name = $"**{Escape(app.Name)}**<br>`{app.AppId}`";

        string description = Escape(Localized(app.ShortDescriptions, text.Language) ?? string.Empty);
        string[] extensions = [.. (latest?.FileHandlers ?? []).SelectMany(handler => handler.Extensions).Distinct(StringComparer.OrdinalIgnoreCase)];
        if (extensions.Length > 0)
        {
            description += $"<br>{text.Opens} {string.Join(", ", extensions.Select(Escape))}";
        }

        CatalogCategory? known = categories.FirstOrDefault(c => c.Id == app.Category);
        string category = Escape((known is null ? null : Localized(known.Labels, text.Language)) ?? app.Category);
        string links = $"[{text.TryIt}]({OsAddress}?autolaunch={Uri.EscapeDataString(app.AppId)}) · [{text.Source}]({app.Repository}) · {Escape(app.License)}";
        return $"| {icon} | {name} | {description} | {Escape(app.LatestVersion)} | {category} | {links} |";
    }

    /// <summary>Text in the page language, else English (required by the catalog), else any.</summary>
    private static string? Localized(IReadOnlyDictionary<string, string>? values, string language)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        return values.TryGetValue(language, out string? value) ? value
            : values.TryGetValue(CatalogPaths.RequiredLanguage, out string? english) ? english
            : values.OrderBy(pair => pair.Key, StringComparer.Ordinal).First().Value;
    }

    /// <summary>
    /// Makes author text safe inside a Markdown table cell: no HTML, no table break, no link or
    /// emphasis of its own, on one line.
    /// </summary>
    internal static string Escape(string value)
    {
        StringBuilder escaped = new(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '&': escaped.Append("&amp;"); break;
                case '<': escaped.Append("&lt;"); break;
                case '>': escaped.Append("&gt;"); break;
                case '\r' or '\n' or '\t': escaped.Append(' '); break;
                case '\\' or '|' or '`' or '*' or '_' or '[' or ']' or '(' or ')' or '#' or '!' or '~':
                    escaped.Append('\\').Append(c);
                    break;
                default: escaped.Append(c); break;
            }
        }

        return escaped.ToString().Trim();
    }
}

/// <summary>Fixed text of <see cref="AppsPage"/> in one language.</summary>
/// <param name="Language">Language code used to pick descriptions and category labels.</param>
/// <param name="OtherLanguageLink">Label of the link to the page in the other language.</param>
/// <param name="OtherLanguageFile">File of the page in the other language.</param>
/// <param name="Title">Page title.</param>
/// <param name="Introduction">Paragraph under the title.</param>
/// <param name="NoApp">Text shown when the catalog is empty.</param>
/// <param name="OneApp">Count sentence for one app.</param>
/// <param name="ManyApps">Count sentence for several apps; <c>{0}</c> is the count.</param>
/// <param name="LastPublication">Words before the date of the newest publication.</param>
/// <param name="AppHeader">Header of the app column.</param>
/// <param name="DescriptionHeader">Header of the description column.</param>
/// <param name="VersionHeader">Header of the version column.</param>
/// <param name="CategoryHeader">Header of the category column.</param>
/// <param name="LinksHeader">Header of the links column.</param>
/// <param name="Opens">Words before the file extensions an app opens.</param>
/// <param name="TryIt">Label of the link that opens the app in Kazo OS.</param>
/// <param name="Source">Label of the link to the app repository.</param>
/// <param name="Footer">Paragraph at the end of the page.</param>
public sealed record PageText(
    string Language,
    string OtherLanguageLink,
    string OtherLanguageFile,
    string Title,
    string Introduction,
    string NoApp,
    string OneApp,
    string ManyApps,
    string LastPublication,
    string AppHeader,
    string DescriptionHeader,
    string VersionHeader,
    string CategoryHeader,
    string LinksHeader,
    string Opens,
    string TryIt,
    string Source,
    string Footer)
{
    /// <summary>The English page.</summary>
    public static PageText English { get; } = new(
        "en",
        "Version française",
        AppsPage.FrenchFileName,
        "Kazo OS Store apps",
        "Every app you can install from the Store of [Kazo OS](https://github.com/Kazo-ca/Os). "
            + "**Try it** opens the app in Kazo OS in your browser, with nothing to install on your computer. "
            + "This page is rewritten by the **Publish catalog** workflow each time the catalog changes.",
        "No app is published yet.",
        "1 app.",
        "{0} apps.",
        "Last publication:",
        "App",
        "Description",
        "Version",
        "Category",
        "Links",
        "Opens:",
        "Try it",
        "Source",
        "To propose an app, read the [catalog README](https://github.com/Kazo-ca/Os-AppMarketPlace#readme).");

    /// <summary>The French page.</summary>
    public static PageText French { get; } = new(
        "fr",
        "English version",
        AppsPage.EnglishFileName,
        "Apps du Kazo OS Store",
        "Toutes les apps qu'on peut installer depuis le Store de [Kazo OS](https://github.com/Kazo-ca/Os). "
            + "**Essayer** ouvre l'app dans Kazo OS, dans le navigateur, sans rien installer sur l'ordinateur. "
            + "Cette page est réécrite par le workflow **Publish catalog** chaque fois que le catalogue change.",
        "Aucune app n'est encore publiée.",
        "1 app.",
        "{0} apps.",
        "Dernière publication :",
        "App",
        "Description",
        "Version",
        "Catégorie",
        "Liens",
        "Ouvre :",
        "Essayer",
        "Code source",
        "Pour proposer une app, lisez le [README du catalogue](https://github.com/Kazo-ca/Os-AppMarketPlace/blob/main/readme-fr.md).");
}
