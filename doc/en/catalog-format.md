> [Version française](../fr/catalog-format.md)

# Catalog format, checks and publication

This document describes the Kazo OS Store catalog repository as built here: the format of an app
folder, what a pull request is checked for, what a merge publishes, and the format of
`catalog.json` that the Kazo OS Store and `kazopkg` read. The original decision is
[ADR 0188](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0188-hack-store-and-app-catalog.md)
(§4 to §6); trust and installation rules are in
[ADR 0189](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0189-app-sources-trust-and-per-user-install.md).

## Architecture

```text
main                              reviewed by PR
  apps/<appId>_<version>/         one folder per app version
  categories.json                 closed list of categories
  tools/                          catalog tool (C#) and its tests
  external/KazoOs                 submodule: Kazo OS libraries the tool reads with
  .github/actions/build-app/      builds an app from its commit (+ CodeQL)
  .github/workflows/validate.yml  checks a PR
  .github/workflows/pr-comment.yml writes the review comment
  .github/workflows/publish.yml   publishes after the merge

packages                          written only by publish.yml, no code history
  catalog.json
  APPS.md, APPS-fr.md               the list of the apps, linked from the READMEs
  packages/<appId>/<version>/<appId>-<version>.kazoapp
  assets/<appId>/<version>/icon.png, screenshots/N.png, description.<lang>.md
```

The tool `tools/Kazo.Os.MarketPlace.Tool` reads manifests with `AppManifestJsonSerializer` and
`AppManifestValidator`, and packages with `AppPackage.Read`: the Kazo OS classes themselves, so the
catalog applies exactly the rules the OS applies at install time. The tool builds the two
libraries from the `external/KazoOs` submodule (`https://github.com/Kazo-ca/Os`), on the `v3.2.0`
tag (2026-11-07: Kazo OS 3.2.0 manifests have the `realNetworkPorts` field of ADR 0235, which the
strict reading of 3.0.0 refused; HackerOS was renamed Kazo OS on 2026-11-05, Kazo OS ADR 0228 and
0229). The `Kazo.Os.*` NuGet packages carry the package reader too: the submodule can be replaced by `PackageReference`
items later.

The CI builds a submitted app at its commit with the app's own `external/KazoOs` submodule (an
app made with the `kazoos-solution` template reads its Kazo OS version there): the
`build-app` action fetches that one submodule, and only when it points to
`https://github.com/Kazo-ca/Os`. Only apps built against Kazo OS 3.0 or later (`Kazo.Os.*`
packages, `KazoOsPack` target, `.kazoapp` package) can be built: a commit of a HackerOS 2.x app
(`external/HackerOs`, `HackerOsPack`, `.hackapp`) cannot, and the 3.0 OS refuses such an app
anyway (`KOSAPP0021`).

Versions published before the rename (`ca.kazo.chess_1.0.0`) stay as they are: a merged folder
never changes, and their `catalog.json` entry keeps its `.hackapp` package path, which Kazo OS
still reads (`AppPackage.LegacyExtension`). A new version of such an app is a new folder built
against Kazo OS 3.0.

## A version folder

The name is `<appId>_<version>`: the app id (at least three lower-case dot-separated segments,
the `AppManifestValidator` rule) and its exact SemVer. Any file that is not in this table is
refused, so no binary enters `main`.

| File | Required | Rule |
|---|---|---|
| `icon.png` | yes | PNG 256×256, 200 KB at most |
| `short_description.<lang>.txt` | `en` yes | UTF-8, one non-empty line, 120 characters at most |
| `description.<lang>.md` | `en` yes | UTF-8, not empty, 20 KB at most |
| `manifest.json` | yes | the one **of the package** `KazoOsPack` built (not the project's: the package adds the static resources to it, Kazo OS ADR 0221); strict read and OS validation; `id` and `version` equal to the folder name |
| `store.json` | yes | see below |
| `screenshots/1.png` … `5.png` | no | 1 to 5 PNG numbered without gaps, 1 MB and 3840 px per side at most |

`<lang>` is a language code such as `fr` or `pt-BR`.

### `store.json`

Read strictly: an unknown or duplicate property is an error.

| Property | Required | Rule |
|---|---|---|
| `repository` | yes | `https://github.com/<owner>/<repo>`, public repository |
| `commit` | yes | full 40-character lower-case hexadecimal SHA |
| `category` | yes | an `id` from `categories.json` |
| `tags` | no | at most 10, lower case, digits and `-`, 30 characters at most, no duplicate |
| `license` | yes | SPDX expression (`MIT`, `GPL-3.0-only`, `MIT OR Apache-2.0`) |
| `project` | no | relative path of the `.csproj` to build (letters, digits, `.`, `_`, `-`, `/`, no `..`) |
| `kind` | no | what the folder publishes: `app` (the value when the field is absent) or `feature` (a feature of the OS, [ADR 0243](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0243-feature-app-type.md) of Kazo OS: its `manifest.json` has `kind: "feature"` and its package holds `entrypoints/`). The tool accepts `feature` only once its `external/KazoOs` submodule moves to a Kazo OS version that reads that kind; theme packs (ADR 0190) come with their work item |

### `categories.json`

An array of `{ "id": "...", "labels": { "en": "...", "fr": "..." } }`. The `id` is lower case
(2 to 32 characters, digits and `-`), unique, and English and French labels are required. A
category is added by a PR on this file.

## Checking a pull request (`validate.yml`)

The numbers are those of ADR 0188 §5.

| Step | Where | What is done |
|---|---|---|
| — | job *Catalog tool tests* | tests of the tool |
| — | job *Check folders* | a file of an already merged folder may neither change nor disappear; a PR may only add folders |
| 1, 2 | job *Check folders* | `validate` on `categories.json` and every folder |
| 3 | job *Build* | `git fetch` of the commit from the `store.json` repository; fails if it does not exist |
| 4 | job *Build* then *review* | `dotnet build -c Release -t:KazoOsPack`, then `check-package`: the package must be read by `AppPackage.Read` and its manifest must be the folder's |
| 5 | — | API check against the trimmed runtime: done by `kazopkg` at install time (ADR 0182); not in CI for v1 (Benoit's decision, Q1 = A; [#3](https://github.com/Kazo-ca/Os-AppMarketPlace/issues/3)) |
| 6 | job *Build* | CodeQL on the app's code: C# from its traced build (`manual`); JavaScript/TypeScript, when the repository has some outside `external/`, from its sources with the runner's CodeQL CLI (`none`, the only mode JavaScript supports, which the CodeQL action would run on the catalog's own directory); `external/**` (the Kazo OS submodule) is ignored; results are summed up in the comment |
| 7 | *review* then `pr-comment.yml` | comment: source, license, SDK, package size and SHA-256, capabilities (dangerous first, marked ⚠️), JavaScript files, assemblies, CodeQL results |

Then a maintainer reads the code and approves; nothing is merged automatically.

### Workflow security

- The jobs that build the app run its code (MSBuild can run anything): they only have a read-only
  token and no secret. The tool is built and run in another job, which only reads the artifacts.
- A PR from a fork cannot write a comment: `pr-comment.yml` runs from `main` on the
  `workflow_run` event, reads the artifact as data, and only comments when the PR number names an
  open PR whose head is the checked commit.
- Values coming from the PR (folder names, `store.json` fields) reach the shell through
  environment variables, never through `${{ }}`, and are only used after the format check.
- A PR can change the workflows themselves: the reviewer checks that the PR only touches `apps/`
  (otherwise it is a maintenance PR, reviewed as such).

## Publication (`publish.yml`)

After a merge into `main` that adds folders (or changes `categories.json`):

1. *plan* lists the folders the push added and checks them again;
2. *build* rebuilds each app at the same commit (no CodeQL), with a read-only token;
3. *publish* (the only job with `contents: write`) runs the tool's `publish` on a copy of the
   `packages` branch (created as an orphan the first time), then pushes a commit.

The tool checks every folder and package again; if one fails, nothing is written. It copies the
package and the images, rewrites `catalog.json`, then keeps only the **3 newest versions** of each
app (older ones leave `packages/` and `assets/`; their folder stays in the history of `main`). An
older version published late does not change the app's listing.

Each publication also rewrites `APPS.md` (English) and `APPS-fr.md` (French) at the root of the
`packages` branch, from the `catalog.json` just written: one row per app with its icon, name, short
description, file types it opens, newest version, category, a **Try it** link
(`https://os.kazo.ca/?autolaunch=<appId>`, ADR 0258 of Kazo OS), its source repository and its
license. Author text is escaped (no HTML, no link of its own). The page holds no clock time (the
date shown is the newest `publishedAt`), so rewriting an unchanged catalog changes nothing. The
READMEs of this repository and of Kazo-ca/Os link to
<https://github.com/Kazo-ca/Os-AppMarketPlace/blob/packages/APPS.md>.

To publish a version again (for example after a change of the tool): run **Publish catalog** by
hand with the folder names separated by spaces. Run with no folder, it builds nothing and only
rewrites `catalog.json` and the list of the apps: this is how the `fileHandlers` of versions published before that field
are filled (see below), without touching their packages or signatures.

### `catalog.json`

```json
{
  "schemaVersion": 1,
  "categories": [
    { "id": "productivity", "labels": { "en": "Productivity", "fr": "Productivité" } }
  ],
  "apps": [
    {
      "appId": "org.example.notes",
      "kind": "app",
      "name": "Notes",
      "category": "productivity",
      "tags": ["notes", "markdown"],
      "shortDescriptions": { "en": "Take notes in Markdown.", "fr": "Prendre des notes en Markdown." },
      "license": "MIT",
      "repository": "https://github.com/example/Os-App-Notes",
      "latestVersion": "1.2.0",
      "versions": [
        {
          "version": "1.2.0",
          "commit": "3f2a9c1e0b7d4c5a8e6f1a2b3c4d5e6f7a8b9c0d",
          "package": "packages/org.example.notes/1.2.0/org.example.notes-1.2.0.kazoapp",
          "sha256": "…",
          "size": 48213,
          "publishedAt": "2026-10-30T12:00:00+00:00",
          "capabilities": ["clipboard.read"],
          "sdkCompatibility": { "minimumVersion": "3.0.0" },
          "icon": "assets/org.example.notes/1.2.0/icon.png",
          "screenshots": ["assets/org.example.notes/1.2.0/screenshots/1.png"],
          "descriptions": { "en": "assets/org.example.notes/1.2.0/description.en.md" },
          "signature": "MEUCIQ…",
          "fileHandlers": [
            { "mediaType": "text/markdown", "extensions": [".md"], "actions": ["open", "edit"] }
          ]
        }
      ]
    }
  ]
}
```

- Paths are relative to the root of the `packages` branch.
- Apps are sorted by `appId`, their versions from newest to oldest.
- The listing fields (`name`, `category`, `tags`, `shortDescriptions`, `license`, `repository`)
  come from the newest version. `capabilities` and `sdkCompatibility` are per version, so the Store
  can show what an update adds.
- `kind` (optional) says what the entry installs, copied from `store.json`: `app` (the value when
  the field is absent: entries published before this field need no rewrite), `feature` (a feature
  of the OS, installed for the whole machine only: the Kazo OS Store lists it with the apps and
  offers "Install for everyone" only, [ADR 0243](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0243-feature-app-type.md)
  §4 of Kazo OS) or `theme-pack` (ADR 0190, to come). A Kazo OS that does not know a value reads
  the entry as an `app` with a warning (versions before ADR 0243 skipped it); a new `kind` therefore
  does not change `schemaVersion`.
- `schemaVersion` changes when the format changes incompatibly; a reader refuses a higher version
  than its own.
- `signature` (optional): ECDSA P-256 / SHA-256 signature of the `.kazoapp` bytes, in IEEE P1363
  form (`r||s`, 64 bytes), base64 ([ADR 0214](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0214-hack-store-hot-add-signing-transactions.md)
  of Kazo OS, §2). `publish.yml` signs each new version with the `KAZO_STORE_SIGNING_KEY` secret
  when it exists; without it, versions are published unsigned. A version already published keeps
  its signature.
- `fileHandlers` (optional): the file types the version can open, copied from the `fileHandlers`
  of its manifest: `extensions` (with the dot), `mediaType` (absent when the manifest gives none)
  and `actions` (`open`, `edit`…). The manifest's `priority` is not copied: it only breaks ties
  between installed apps. The field is absent when the manifest declares no type. Kazo OS uses it
  to offer a catalog app when no installed app opens a file ([ADR 0257](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0257-offer-a-store-app-for-an-unsupported-file.md)
  of Kazo OS). The field is in `catalog.json`, not in the package: it changes neither `sha256` nor
  `signature`. Versions published before the field receive it at the next publication: the tool
  reads it from the manifest of their package on the `packages` branch. A reader that does not
  know it ignores it; `schemaVersion` stays 1.

### Signing key (for Benoit)

1. On your computer, in a copy of this repository:
   `dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- keygen --private-key ~/kazoos-store-signing.pem`.
   The tool writes the private key to a new file (`0600`), never replaces an existing file, and
   prints the public key.
2. Copy the content of the `.pem` file into the repository's `KAZO_STORE_SIGNING_KEY` secret
   (Settings ▸ Secrets and variables ▸ Actions), then keep the file offline. The private key never
   goes through an agent or a PR.
3. Put the public key in `OfficialCatalogSigningKey` (`AppPackageDefaults.cs`) or in Kazo OS's
   `KazoOsAppSigningKeys` MSBuild property (`catalog:official=<public key>`): from that OS
   version on, every package of the official catalog must carry a valid signature.
4. Publish the existing versions again (*Publish catalog* workflow ▸ Run workflow, with their
   folders) so they get a signature before the OS requires it.

## The catalog tool

```text
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- validate --root . [folder...]
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- check-package --root . --folder <folder> --report <f.md> [--package <f.kazoapp>] [--sarif <dir>]
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- publish --root . --built <dir> --output <packages copy> [--keep 3] [--published-at <ISO 8601>]
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- keygen --private-key <file.pem>
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- sign --package <f.kazoapp> [--private-key <file.pem>]
dotnet test tools/Kazo.Os.MarketPlace.slnx
```

Exit code: 0 when all is well, 1 when something is refused, 2 for a usage error. In GitHub
Actions, errors become annotations on the PR's files. Clone with `--recurse-submodules` (or
`git submodule update --init`) before building the tool.

## Repository settings (for Benoit)

- Protection of `main`: PR required, **Code Owners review** required (`.github/CODEOWNERS` names
  `@werddomain`), checks *Catalog tool tests*, *Check folders* and *Check packages and write the
  review* required.
- Actions ▸ General: require approval before running workflows of PRs from outside contributors.
- Protection of `packages`: writing reserved to GitHub Actions.

## Decisions taken in this work item

- `store.json` gets two optional fields: `project` (the `.csproj` to build) and `kind` (only `app`
  for now). Benoit's decision (Q2 = A).
- Limits the ADR did not set: long description 20 KB, screenshots 1 MB and 3840 px per side,
  10 tags.
- `catalog.json` also holds the categories and their labels, so the Store reads a single file.
- A merged version is immutable: a PR may only add folders.
- The API check in CI (ADR 0188 §5 step 5) is left to install time for v1 (Benoit's decision,
  Q1 = A); it comes with [#3](https://github.com/Kazo-ca/Os-AppMarketPlace/issues/3).
- `kind: "feature"` in `catalog.json` (2026-11-11, Benoit's decision on
  [Kazo-ca/Os#449](https://github.com/Kazo-ca/Os/pull/449), question 1 = B): the catalog says the
  kind of a package so the Store hides "Install for me"; an absent field means `app`, existing
  entries do not change.

## Tasks

- [x] `categories.json`, PR template, `CODEOWNERS`.
- [x] Catalog tool: format, manifest, package, review comment, publication, tests.
- [x] `validate.yml`, `pr-comment.yml`, `publish.yml` and the `build-app` action.
- [x] README (English, French) and this document.
- [ ] API check against the trimmed runtime in CI ([#3](https://github.com/Kazo-ca/Os-AppMarketPlace/issues/3)).
- [ ] Replace the submodule by the NuGet packages once a release carries them.
- [ ] Theme packs (`kind: "theme-pack"`, ADR 0190).
- [ ] `kind: "feature"` in the tool (`store.json`, `check-package`, `publish`) once the
  `external/KazoOs` submodule reads features (ADR 0243 of Kazo OS,
  [Kazo-ca/Os#449](https://github.com/Kazo-ca/Os/pull/449)).
