#!/usr/bin/env bash
# check-package-apis.sh: step 5 of ADR 0188 §5 of Kazo-ca/Os, as ADR 0277 §3 decides it. Checks
# that every assembly of a package built by our CI only calls members the trimmed Kazo OS keeps.
#
#   check-package-apis.sh --package <file.kazoapp> --report <file.md> [--channel stable|beta]
#
# The check is the Kazo OS one, not a copy: the script downloads Kazo.Os.App.Build of the target
# Kazo OS version from nuget.org and runs the "check-api" command of its manifest generator against
# the API surface the package carries (kazoos-build/api-surface/kazoos-api-surface.json.gz). This is
# the same code as KazoOsPack (KOSAPP0100) and, at install time, kazopkg and the Store.
#
# Target version: the newest stable Kazo OS for the stable channel (what os.kazo.ca runs), the newest
# version, pre-releases included, for the beta channel (ADR 0271). KAZOOS_API_VERSION names another
# one; KAZOOS_APP_BUILD_PACKAGE names a local Kazo.Os.App.Build .nupkg instead of nuget.org (tests).
#
# The report (Markdown, appended to --report) lists the missing members. Exit code: 0 when the
# package passes, or when the target version carries no API surface yet (Kazo OS 3.4.2 and older:
# the report then says the APIs were not checked, and a workflow warning is written); 1 when a member
# is missing; 2 when the check could not run (bad arguments, download or tool failure).
set -uo pipefail

PACKAGE=''
REPORT=''
CHANNEL='stable'
while [[ $# -gt 0 ]]; do
    case "$1" in
        --package) PACKAGE="${2:-}"; shift 2 ;;
        --report) REPORT="${2:-}"; shift 2 ;;
        --channel) CHANNEL="${2:-}"; shift 2 ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
done

if [[ -z "$PACKAGE" || -z "$REPORT" ]]; then
    echo 'usage: check-package-apis.sh --package <file.kazoapp> --report <file.md> [--channel stable|beta]' >&2
    exit 2
fi
if [[ "$CHANNEL" != 'stable' && "$CHANNEL" != 'beta' ]]; then
    echo "--channel must be stable or beta, not '$CHANNEL'" >&2
    exit 2
fi
if [[ ! -f "$PACKAGE" ]]; then
    echo "No package at '$PACKAGE'." >&2
    exit 2
fi

NUGET='https://api.nuget.org/v3-flatcontainer/kazo.os.app.build'
SURFACE_ENTRY='kazoos-build/api-surface/kazoos-api-surface.json.gz'
GENERATOR_ENTRY='tools/generator/*'
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Writes one section of the review: what was checked and against which Kazo OS.
report() {
    printf '%s\n' "$@" >> "$REPORT"
}

# 1. The Kazo OS version to check against.
version="${KAZOOS_API_VERSION:-}"
nupkg="${KAZOOS_APP_BUILD_PACKAGE:-}"
if [[ -n "$nupkg" ]]; then
    if [[ -z "$version" ]]; then
        version="$(basename "$nupkg" .nupkg)"
        version="${version#Kazo.Os.App.Build.}"
    fi
else
    if [[ -z "$version" ]]; then
        # nuget.org lists the versions of a package in ascending SemVer order.
        if ! versions="$(curl -fsSL --retry 3 "$NUGET/index.json")"; then
            echo "::error title=API check::Could not list the versions of Kazo.Os.App.Build on nuget.org."
            exit 2
        fi
        if [[ "$CHANNEL" == 'stable' ]]; then
            version="$(jq -r '[.versions[] | select(contains("-") | not)] | last // empty' <<< "$versions")"
        else
            version="$(jq -r '.versions | last // empty' <<< "$versions")"
        fi
        if [[ -z "$version" ]]; then
            echo "::error title=API check::nuget.org lists no $CHANNEL version of Kazo.Os.App.Build."
            exit 2
        fi
    fi
    nupkg="$WORK/Kazo.Os.App.Build.$version.nupkg"
    lower="$(tr '[:upper:]' '[:lower:]' <<< "$version")"
    if ! curl -fsSL --retry 3 -o "$nupkg" "$NUGET/$lower/kazo.os.app.build.$lower.nupkg"; then
        echo "::error title=API check::Could not download Kazo.Os.App.Build $version from nuget.org."
        exit 2
    fi
fi

# 2. The surface and the checker of that version.
if ! unzip -l "$nupkg" | grep -q "$SURFACE_ENTRY"; then
    echo "::warning title=API check::Kazo OS $version carries no API surface (ADR 0277): the package's APIs were not checked against the trimmed OS."
    report "**API check**: ⚠️ not run. Kazo OS $version, the newest $CHANNEL version, publishes no API surface (ADR 0277 of Kazo-ca/Os, from the next release). The install-time check of the OS still applies." ''
    exit 0
fi
if ! unzip -q -o "$nupkg" "$SURFACE_ENTRY" "$GENERATOR_ENTRY" -d "$WORK/build"; then
    echo "::error title=API check::Could not extract the API surface and the checker from Kazo.Os.App.Build $version."
    exit 2
fi

# 3. The check. One line per missing member: "<origin> : error KOSAPP0100: <member> ...".
output="$(dotnet "$WORK/build/tools/generator/Kazo.Os.Tools.ManifestGenerator.dll" check-api \
    --package "$PACKAGE" --api-surface "$WORK/build/$SURFACE_ENTRY" 2>&1)"
code=$?
printf '%s\n' "$output"
missing="$(grep -E ' error KOSAPP0100: ' <<< "$output" | sed -E 's/^.* error KOSAPP0100: //' || true)"

if [[ $code -eq 0 ]]; then
    report "**API check**: ✅ every member the package calls is in Kazo OS $version (trimmed)." ''
    exit 0
fi

if [[ -n "$missing" ]]; then
    count="$(grep -c . <<< "$missing")"
    report "**❌ API check**: $count member(s) the package calls are not in Kazo OS $version (trimmed). The OS refuses to install it. Replace these calls, then submit a new version:" ''
    while IFS= read -r line; do
        report "- $line"
        echo "::error title=API check::$line"
    done <<< "$missing"
    report ''
    exit 1
fi

# The checker stopped for another reason (an unreadable package, for example): show its output.
echo "::error title=API check::The check against Kazo OS $version did not run (exit code $code)."
report "**❌ API check**: the check against Kazo OS $version did not run (exit code $code):" '' '```' "$(tail -n 20 <<< "$output")" '```' ''
exit 2
