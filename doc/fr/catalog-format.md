> [English version](../en/catalog-format.md)

# Format du catalogue, vérifications et publication

Ce document décrit le dépôt catalogue du Kazo OS Store tel qu'il est construit ici : le format d'un
dossier d'app, ce que vérifie une pull request, ce que publie une fusion, et le format de
`catalog.json` que lisent le Kazo OS Store et `kazopkg`. La décision d'origine est
l'[ADR 0188](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/adr/0188-hack-store-and-app-catalog.md)
(§4 à §6) ; les règles de confiance et d'installation sont dans
l'[ADR 0189](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/adr/0189-app-sources-trust-and-per-user-install.md).

## Architecture

```text
main                              relue par PR
  apps/<appId>_<version>/         un dossier par version d'app
  categories.json                 liste fermée des catégories
  tools/                          outil du catalogue (C#) et ses tests
  external/KazoOs                 sous-module : bibliothèques Kazo OS lues par l'outil
  .github/actions/build-app/      compile une app depuis son commit (+ CodeQL)
  .github/workflows/validate.yml  vérifie une PR
  .github/workflows/pr-comment.yml écrit le commentaire de revue
  .github/workflows/publish.yml   publie après la fusion

packages                          écrite seulement par publish.yml, sans historique de code
  catalog.json
  APPS.md, APPS-fr.md               la liste des apps, liée depuis les README
  packages/<appId>/<version>/<appId>-<version>.kazoapp
  assets/<appId>/<version>/icon.png, screenshots/N.png, description.<lang>.md
```

L'outil `tools/Kazo.Os.MarketPlace.Tool` lit les manifestes avec `AppManifestJsonSerializer` et
`AppManifestValidator`, et les paquets avec `AppPackage.Read` : ce sont les classes de Kazo OS
elles-mêmes, donc le catalogue applique exactement les règles de l'OS à l'installation. L'outil
compile les deux bibliothèques depuis le sous-module `external/KazoOs` (`https://github.com/Kazo-ca/Os`),
sur le tag `v3.2.0` (2026-11-07 : les manifestes de Kazo OS 3.2.0 ont le champ `realNetworkPorts`
de l'ADR 0235, que la lecture stricte de 3.0.0 refusait ; HackerOS a été renommé Kazo OS le
2026-11-05, ADR 0228 et 0229 de Kazo OS). Les paquets NuGet `Kazo.Os.*` contiennent aussi le lecteur de paquets : le sous-module pourra
être remplacé plus tard par des `PackageReference`.

La CI compile une app soumise à son commit avec le sous-module `external/KazoOs` de l'app (une
app créée avec le modèle `kazoos-solution` y lit sa version de Kazo OS) : l'action
`build-app` récupère ce seul sous-module, et seulement s'il pointe vers
`https://github.com/Kazo-ca/Os`. Seules les apps compilées pour Kazo OS 3.0 ou plus récent (paquets
`Kazo.Os.*`, cible `KazoOsPack`, paquet `.kazoapp`) peuvent être compilées : un commit d'une app
HackerOS 2.x (`external/HackerOs`, `HackerOsPack`, `.hackapp`) ne le peut pas, et l'OS 3.0 refuse
de toute façon une telle app (`KOSAPP0021`).

Les versions publiées avant le renommage (`ca.kazo.chess_1.0.0`) restent telles quelles : un
dossier fusionné ne change jamais, et leur entrée de `catalog.json` garde le chemin de paquet
`.hackapp`, que Kazo OS lit encore (`AppPackage.LegacyExtension`). Une nouvelle version d'une telle
app est un nouveau dossier compilé pour Kazo OS 3.0.

## Le dossier d'une version

Le nom est `<appId>_<version>` : l'identifiant de l'app (au moins trois segments en minuscules
séparés par des points, règle de `AppManifestValidator`) et sa version SemVer exacte. Tout fichier
qui n'est pas dans ce tableau est refusé, pour qu'aucun binaire n'entre dans `main`.

| Fichier | Obligatoire | Règle |
|---|---|---|
| `icon.png` | oui | PNG 256×256, 200 Ko au plus |
| `short_description.<lang>.txt` | `en` oui | UTF-8, une ligne non vide, 120 caractères au plus |
| `description.<lang>.md` | `en` oui | UTF-8, non vide, 20 Ko au plus |
| `manifest.json` | oui | celui **du paquet** construit par `KazoOsPack` (pas celui du projet : le paquet y ajoute les ressources statiques, ADR 0221 de Kazo OS) ; lecture stricte et validation de l'OS ; `id` et `version` égaux au nom du dossier |
| `store.json` | oui | voir ci-dessous |
| `screenshots/1.png` … `5.png` | non | 1 à 5 PNG numérotés sans trou, 1 Mo et 3840 px de côté au plus |

`<lang>` est un code de langue comme `fr` ou `pt-BR`.

### `store.json`

Lu strictement : une propriété inconnue ou en double est une erreur.

| Propriété | Obligatoire | Règle |
|---|---|---|
| `repository` | oui | `https://github.com/<owner>/<repo>`, dépôt public |
| `commit` | oui | SHA complet de 40 caractères hexadécimaux en minuscules |
| `category` | oui | un `id` de `categories.json` |
| `tags` | non | 10 au plus, minuscules, chiffres et `-`, 30 caractères au plus, sans doublon |
| `license` | oui | expression SPDX (`MIT`, `GPL-3.0-only`, `MIT OR Apache-2.0`) |
| `project` | non | chemin relatif du `.csproj` à compiler (lettres, chiffres, `.`, `_`, `-`, `/`, sans `..`) |
| `kind` | non | ce que le dossier publie : `app` (valeur quand le champ manque) ou `feature` (une fonctionnalité de l'OS, [ADR 0243](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/adr/0243-feature-app-type.md) de Kazo OS : son `manifest.json` a `kind: "feature"` et son paquet contient `entrypoints/`). L'outil n'accepte `feature` qu'une fois son sous-module `external/KazoOs` passé à une version de Kazo OS qui lit ce genre ; les packs de thèmes (ADR 0190) viendront avec leur chantier |

### `categories.json`

Un tableau de `{ "id": "...", "labels": { "en": "...", "fr": "..." } }`. L'`id` est en minuscules
(2 à 32 caractères, chiffres et `-`), unique, et les libellés anglais et français sont obligatoires.
Une catégorie s'ajoute par une PR sur ce fichier.

## Vérification d'une pull request (`validate.yml`)

Les numéros sont ceux de l'ADR 0188 §5.

| Étape | Où | Ce qui est fait |
|---|---|---|
| — | job *Catalog tool tests* | tests de l'outil |
| — | job *Check folders* | un fichier d'un dossier déjà fusionné ne peut ni changer ni disparaître ; la PR ne peut qu'ajouter des dossiers |
| 1, 2 | job *Check folders* | `validate` sur `categories.json` et tous les dossiers |
| 3 | job *Build* | `git fetch` du commit dans le dépôt de `store.json` ; échec s'il n'existe pas |
| 4 | job *Build* puis *review* | `dotnet build -c Release -t:KazoOsPack`, puis `check-package` : le paquet doit être lu par `AppPackage.Read` et son manifeste être celui du dossier |
| 5 | — | vérification des API contre le runtime élagué : faite par `kazopkg` à l'installation (ADR 0182) ; pas en CI pour la v1 (décision de Benoit, Q1 = A ; [#3](https://github.com/Kazo-ca/Os-AppMarketPlace/issues/3)) |
| 6 | job *Build* | CodeQL sur le code de l'app : C# depuis sa build tracée (`manual`) ; JavaScript/TypeScript, si le dépôt en a hors de `external/`, depuis ses sources avec la CLI CodeQL du runner (`none`, le seul mode que JavaScript accepte, que l'action CodeQL lancerait sur le dossier du catalogue lui-même) ; `external/**` (le sous-module Kazo OS) est ignoré ; les résultats sont résumés dans le commentaire |
| 7 | *review* puis `pr-comment.yml` | commentaire : source, licence, SDK, taille et SHA-256 du paquet, capacités (les dangereuses d'abord, marquées ⚠️), fichiers JavaScript, assemblies, résultats CodeQL |

Puis un mainteneur relit le code et approuve. Seule exception : une « reconstruction seule » soumise
par l'automate des apps est fusionnée par `auto-merge.yml` (voir [Canal bêta](#canal-bêta-adr-0271-5)).

### Sécurité des workflows

- Les jobs qui compilent l'app exécutent son code (MSBuild peut lancer n'importe quoi) : ils n'ont
  qu'un jeton en lecture et aucun secret. L'outil est compilé et lancé dans un autre job, qui ne
  fait que lire les artefacts.
- Une PR venant d'un fork ne peut pas écrire de commentaire : `pr-comment.yml` tourne depuis `main`
  sur l'événement `workflow_run`, lit l'artefact comme des données, et ne commente que si le numéro
  de PR désigne une PR ouverte dont la tête est le commit vérifié.
- Les valeurs venant de la PR (noms de dossiers, champs de `store.json`) passent au shell par des
  variables d'environnement, jamais par `${{ }}`, et ne sont utilisées qu'après la vérification du
  format.
- Une PR peut modifier les workflows eux-mêmes : le relecteur vérifie que la PR ne touche que
  `apps/` (ou alors c'est une PR de maintenance relue comme telle).

## Publication (`publish.yml`)

Après une fusion dans `main` qui ajoute des dossiers (ou change `categories.json`) :

1. *plan* liste les dossiers ajoutés par le push et les revérifie ;
2. *build* recompile chaque app au même commit (sans CodeQL), avec un jeton en lecture ;
3. *publish* (seul job avec `contents: write`) lance `publish` de l'outil sur une copie de la
   branche `packages` (créée orpheline la première fois), puis pousse un commit.

L'outil revérifie chaque dossier et chaque paquet ; si l'un échoue, rien n'est écrit. Il copie le
paquet et les images, réécrit `catalog.json`, puis ne garde que les **3 dernières versions** de
chaque app (les plus anciennes sont retirées de `packages/` et `assets/` ; leur dossier reste dans
l'historique de `main`). Une version plus ancienne publiée en retard ne change pas la fiche de
l'app.

Chaque publication réécrit aussi `APPS.md` (anglais) et `APPS-fr.md` (français) à la racine de la
branche `packages`, depuis le `catalog.json` qui vient d'être écrit : une ligne par app avec son
icône, son nom, sa description courte, les types de fichiers qu'elle ouvre, sa dernière version, sa
catégorie, un lien **Essayer** (`https://os.kazo.ca/?autolaunch=<appId>`, ADR 0258 de Kazo OS),
son dépôt source et sa licence. Le texte des auteurs est échappé (ni HTML, ni lien à lui). La page
ne contient pas d'heure (la date affichée est le `publishedAt` le plus récent) : réécrire un
catalogue inchangé ne change rien. Les README de ce dépôt et de Kazo-ca/Os renvoient vers
<https://github.com/Kazo-ca/Os-AppMarketPlace/blob/packages/APPS-fr.md>.

Pour republier une version (par exemple après un changement de l'outil) : lancer **Publish
catalog** à la main avec les noms de dossiers séparés par des espaces. Lancé sans dossier, il ne
reconstruit rien et réécrit seulement `catalog.json` et la liste des apps : c'est ainsi qu'on remplit les
`fileHandlers` des versions publiées avant ce champ (voir plus bas), sans toucher à leurs paquets
ni à leurs signatures.

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

- Les chemins sont relatifs à la racine de la branche `packages`.
- Les apps sont triées par `appId`, leurs versions de la plus récente à la plus ancienne.
- Les champs de la fiche (`name`, `category`, `tags`, `shortDescriptions`, `license`,
  `repository`) viennent de la version la plus récente. `capabilities` et `sdkCompatibility` sont
  par version, pour que le Store montre ce qu'une mise à jour ajoute.
- `kind` (facultatif) dit ce que l'entrée installe, recopié de `store.json` : `app` (valeur quand
  le champ manque : les entrées publiées avant ce champ n'ont pas à être réécrites), `feature`
  (une fonctionnalité de l'OS, installée pour toute la machine seulement : le Kazo OS Store la liste
  avec les apps et ne propose que « Installer pour tous », [ADR 0243](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/adr/0243-feature-app-type.md)
  §4 de Kazo OS) ou `theme-pack` (ADR 0190, à venir). Un Kazo OS qui ne connaît pas une valeur lit
  l'entrée comme une `app` avec un avertissement (les versions d'avant l'ADR 0243 l'ignoraient) ;
  un nouveau `kind` ne change donc pas `schemaVersion`.
- `schemaVersion` change si le format change de façon incompatible ; un lecteur refuse une version
  plus grande que la sienne.
- `signature` (facultatif) : signature ECDSA P-256 / SHA-256 des octets du `.kazoapp`, au format
  IEEE P1363 (`r||s`, 64 octets) en base64 ([ADR 0214](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/adr/0214-hack-store-hot-add-signing-transactions.md)
  de Kazo OS, §2). `publish.yml` signe chaque nouvelle version avec le secret
  `KAZO_STORE_SIGNING_KEY` quand il existe ; sans lui, les versions sont publiées sans signature.
  Une version déjà publiée garde sa signature.
- `fileHandlers` (facultatif) : les types de fichiers que la version sait ouvrir, recopiés des
  `fileHandlers` de son manifeste : `extensions` (avec le point), `mediaType` (absent quand le
  manifeste n'en donne pas) et `actions` (`open`, `edit`…). La `priority` du manifeste n'est pas
  recopiée : elle ne départage que les apps installées. Le champ est absent quand le manifeste ne
  déclare aucun type. Kazo OS s'en sert pour proposer une app du catalogue quand aucune app
  installée n'ouvre un fichier ([ADR 0257](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/adr/0257-offer-a-store-app-for-an-unsupported-file.md)
  de Kazo OS). Le champ est dans `catalog.json`, pas dans le paquet : il ne change ni le `sha256`
  ni la `signature`. Les versions publiées avant ce champ le reçoivent à la publication suivante :
  l'outil le lit dans le manifeste de leur paquet sur la branche `packages`. Un lecteur qui ne le
  connaît pas l'ignore ; `schemaVersion` reste 1.

### Clé de signature (à faire par Benoit)

1. Sur ton ordinateur, dans une copie de ce dépôt :
   `dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- keygen --private-key ~/kazoos-store-signing.pem`.
   L'outil écrit la clé privée dans un nouveau fichier (`0600`), ne remplace jamais un fichier
   existant, et affiche la clé publique.
2. Copier le contenu du fichier `.pem` dans le secret `KAZO_STORE_SIGNING_KEY` du dépôt
   (Settings ▸ Secrets and variables ▸ Actions), puis garder le fichier hors ligne. La clé
   privée ne passe jamais par un agent ni par une PR.
3. Mettre la clé publique dans `OfficialCatalogSigningKey` (`AppPackageDefaults.cs`) ou dans la
   propriété MSBuild `KazoOsAppSigningKeys` de Kazo OS (`catalog:official=<clé publique>`) :
   dès cette version de l'OS, tout paquet du catalogue officiel doit porter une signature valide.
4. Republier les versions existantes (workflow *Publish catalog* ▸ Run workflow, avec leurs
   dossiers) pour qu'elles reçoivent une signature avant que l'OS l'exige.

## Canal bêta ([ADR 0271](https://github.com/Kazo-ca/Os/blob/release-channels/base/doc/fr/wasm/dev/adr/0271-beta-channel-and-automatic-app-updates.md) §5)

Kazo OS a deux canaux. Un OS stable lit `catalog.json`, une préversion de l'OS lit
`catalog-beta.json`, les deux à la racine de la branche `packages`.

| | Stable | Bêta |
|---|---|---|
| Branche qui reçoit les PR | `main` | `beta` |
| Versions acceptées | sans suffixe (`1.0.1`) | avec suffixe (`1.0.1-beta.1`) |
| Fichier publié | `catalog.json`, puis `catalog-beta.json` | `catalog-beta.json` seul |
| Sous-module `external/KazoOs` | version stable de l'OS | préversion de l'OS |

- **La branche `beta`** contient tout `main` (le job *Bring main into beta* de `publish.yml` y
  fusionne `main` après chaque publication stable, et garde le sous-module de `beta` quand les deux
  l'ont déplacé) plus les dossiers des versions bêta. Elle se crée depuis `main` quand la série
  `release-channels` y est fusionnée ; avant, l'automate des apps dit que le canal bêta n'est pas
  ouvert.
- **Vérification** : `validate --channel beta` sur une PR vers `beta`, `--channel stable` sinon.
  Un dossier ajouté doit être du canal de sa branche ; sans dossier nommé, `main` refuse tout dossier
  bêta, `beta` accepte les deux (elle reçoit les stables de `main`).
- **Publication** : un push sur `beta` ne recompile que les dossiers bêta ajoutés (les stables ont
  été publiés par `main`), signés avec la même clé `KAZO_STORE_SIGNING_KEY`, et réécrit
  `catalog-beta.json` sans toucher `catalog.json` ni la liste des apps. Les deux branches partagent
  le groupe de concurrence `publish-catalog`, car elles poussent sur la même branche `packages`.

### `catalog-beta.json`

Format fixé par le chantier 3 de la série (Kazo OS, `app-installation.md`, « Format de
`catalog-beta.json` ») :

- même format que `catalog.json` (`schemaVersion` 1) : tout `catalog.json` (catégories, apps,
  versions stables), plus les versions bêta dans `versions` de leur app ;
- paquets et ressources aux mêmes chemins (`packages/<appId>/<version>/`, `assets/<appId>/<version>/`) ;
- versions de la plus récente à la plus ancienne en SemVer (`1.0.1` > `1.0.1-beta.2` > `1.0.0`) ;
- `latestVersion` et champs de fiche : ceux de `catalog.json`, une bêta ne change pas la fiche ;
  une app qui n'a que des versions bêta est décrite par la plus récente ;
- rétention séparée : les 3 bêtas les plus récentes de chaque app, en plus des 3 stables ;
- une bêta est retirée (paquet et ressources compris) dès qu'une version stable plus récente de son
  app est publiée, et une bêta plus ancienne que la dernière stable est refusée.

Les versions bêta ne vivent qu'entre deux publications dans `catalog-beta.json` : chaque
publication le relit, puis le réécrit (`BetaCatalog`).

### Fusion des « reconstructions seules » (`auto-merge.yml`)

Quand l'automate des apps (workflow `sdk-update` d'Os-AppTemplate) déplace une app sur un nouveau
SDK, il soumet une version qui n'apporte aucun code à relire. Sur décision de Benoit (Q5 = A),
l'automate fusionne alors la PR une fois sa validation verte, en bêta comme en stable, dans ce
catalogue seulement. `auto-merge.yml` tourne depuis `main` après *Validate app submissions* et
fusionne seulement si :

1. la validation a réussi sur la tête actuelle d'une PR ouverte vers `main` ou `beta` ;
2. la PR vient d'une branche `submit/<appId>_<version>` de ce dépôt (l'App GitHub de
   l'organisation y pousse), ouverte par un bot ;
3. la PR ne fait qu'ajouter les fichiers de `apps/<appId>_<version>/` : la validation qui a tourné
   est donc celle de la branche de base, pas une que la PR aurait changée ;
4. `check-rebuild` de l'outil, compilé depuis la branche de base, compare le dossier à la version
   précédente de l'app dans cette branche (la plus récente plus ancienne) :
   - `store.json` identique sauf `commit` (même dépôt, même projet, catégorie, étiquettes, licence) ;
   - `manifest.json` identique sauf `version` et `sdkCompatibility`, donc mêmes capacités ;
   - icône, descriptions et captures identiques à l'octet ;
   - dans le dépôt de l'app, entre le commit précédent et le commit soumis
     (`git diff --unified=0`, récupéré sans rien exécuter), seul le sous-module `external/KazoOs`
     bouge, et chaque autre ligne changée ne diffère de l'ancienne que par la version (le
     `Version` de la classe `[AppManifest]` et du `manifest.json` versionné, comme les écrit
     `bump-app-version.sh`). Pas de fichier ajouté, supprimé, binaire ou changé de mode.

Sinon la PR attend un mainteneur, avec un commentaire qui dit pourquoi (première version de l'app,
code changé, capacités ajoutées…). La fusion se fait avec l'App GitHub de l'organisation
(`KAZO_AUTOMATION_APP_ID`, `KAZO_AUTOMATION_APP_KEY`) quand elle est disponible, ce qui lance
`publish.yml` ; sinon avec le jeton du workflow, qui lance alors lui-même la publication
(`workflow_dispatch` avec le dossier), car GitHub ne démarre aucun workflow pour un push fait avec ce jeton.

## L'outil du catalogue

```text
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- validate --root . [--channel stable|beta] [dossier...]
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- check-package --root . --folder <dossier> --report <f.md> [--package <f.kazoapp>] [--sarif <dossier>]
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- publish --root . --built <dossier> --output <copie de packages> [--channel stable|beta] [--keep 3] [--published-at <ISO 8601>]
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- previous-version --root . --folder <dossier>
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- check-rebuild --root . --folder <dossier> --diff <fichier.diff>
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- keygen --private-key <fichier.pem>
dotnet run --project tools/Kazo.Os.MarketPlace.Tool -- sign --package <f.kazoapp> [--private-key <fichier.pem>]
dotnet test tools/Kazo.Os.MarketPlace.slnx
```

Code de sortie : 0 si tout va bien, 1 si quelque chose est refusé, 2 pour une erreur d'usage. Dans
GitHub Actions, les erreurs deviennent des annotations sur les fichiers de la PR. Cloner avec
`--recurse-submodules` (ou `git submodule update --init`) avant de compiler l'outil.

## Réglage du dépôt (à faire par Benoit)

- Protection de `main` : PR obligatoire, **revue des Code Owners** obligatoire (`.github/CODEOWNERS`
  désigne `@werddomain`), vérifications *Catalog tool tests*, *Check folders* et *Check packages and
  write the review* obligatoires.
- Actions ▸ General : demander l'approbation avant de lancer les workflows des PR de contributeurs
  extérieurs.
- Protection de `packages` : écriture réservée à GitHub Actions.
- Canal bêta, une fois la série `release-channels` dans `main` : créer `beta` depuis `main`, la
  protéger comme `main` en laissant le job *Bring main into beta* (jeton du workflow) et l'App
  GitHub de l'organisation y pousser, et autoriser l'App à fusionner les PR de `main` et `beta`
  (ou l'ajouter aux exceptions de la protection) ; donner au dépôt l'accès aux secrets d'organisation
  `KAZO_AUTOMATION_APP_ID` et `KAZO_AUTOMATION_APP_KEY`.

## Décisions prises dans ce chantier

- `store.json` reçoit deux champs facultatifs : `project` (le `.csproj` à compiler) et `kind`
  (`app` seulement pour l'instant). Décision de Benoit (Q2 = A).
- Limites que l'ADR ne fixait pas : description longue 20 Ko, captures 1 Mo et 3840 px de côté,
  10 étiquettes.
- `catalog.json` contient aussi les catégories et leurs libellés, pour que le Store n'ait qu'un
  fichier à lire.
- Une version fusionnée est immuable : une PR ne peut qu'ajouter des dossiers.
- Canal bêta (2026-11-14, ADR 0271) : rétention de 3 bêtas par app en plus des 3 stables ; une
  bêta plus ancienne que la dernière stable est refusée ; la « reconstruction seule » exige aussi
  des ressources identiques et un `store.json` identique sauf `commit`, et se compare à la
  version précédente dans la branche visée.
- La vérification des API en CI (ADR 0188 §5 étape 5) est laissée à l'installation pour la v1
  (décision de Benoit, Q1 = A) ; elle viendra avec [#3](https://github.com/Kazo-ca/Os-AppMarketPlace/issues/3).
- `kind: "feature"` dans `catalog.json` (2026-11-11, décision de Benoit sur
  [Kazo-ca/Os#449](https://github.com/Kazo-ca/Os/pull/449), question 1 = B) : le catalogue dit le
  genre d'un paquet pour que le Store cache « Installer pour moi » ; l'absence du champ vaut `app`,
  les entrées existantes ne changent pas.

## Tâches

- [x] Canal bêta (ADR 0271 §5, chantier 4 de `release-channels`) : `catalog-beta.json`, branche
  `beta` dans `validate.yml` et `publish.yml`, `check-rebuild`, `auto-merge.yml`, tests.
- [ ] Créer la branche `beta` quand la série est dans `main` (chantier 8).

- [x] `categories.json`, modèle de PR, `CODEOWNERS`.
- [x] Outil du catalogue : format, manifeste, paquet, commentaire de revue, publication, tests.
- [x] `validate.yml`, `pr-comment.yml`, `publish.yml` et l'action `build-app`.
- [x] README (anglais, français) et ce document.
- [ ] Vérification des API contre le runtime élagué en CI ([#3](https://github.com/Kazo-ca/Os-AppMarketPlace/issues/3)).
- [ ] Remplacer le sous-module par les paquets NuGet quand une version les contient.
- [ ] Packs de thèmes (`kind: "theme-pack"`, ADR 0190).
- [ ] `kind: "feature"` dans l'outil (`store.json`, `check-package`, `publish`) quand le
  sous-module `external/KazoOs` lira les fonctionnalités (ADR 0243 de Kazo OS,
  [Kazo-ca/Os#449](https://github.com/Kazo-ca/Os/pull/449)).
