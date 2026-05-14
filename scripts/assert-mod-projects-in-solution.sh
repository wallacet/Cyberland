#!/usr/bin/env bash
# Validates that every *.csproj under mods/ is listed in Cyberland.sln.
# Used by .github/workflows/mod-projects-in-solution.yml and .githooks/pre-commit.
# Bash keeps CI on ubuntu-latest free of PowerShell; local Git for Windows provides bash too.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SLN="$REPO_ROOT/Cyberland.sln"
MODS="$REPO_ROOT/mods"

if [[ ! -f "$SLN" ]]; then
  echo "ERROR: Cyberland.sln not found at $SLN" >&2
  exit 1
fi
if [[ ! -d "$MODS" ]]; then
  echo "ERROR: mods directory not found at $MODS" >&2
  exit 1
fi

trim() {
  local s="$1"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

declare -a sln_projects=()
while IFS= read -r line || [[ -n "$line" ]]; do
  line="$(trim "$line")"
  [[ -z "$line" ]] && continue
  line="${line//\\//}"
  sln_projects+=("$line")
done < <(dotnet sln "$SLN" list | tail -n +3)

in_sln() {
  local needle="$1"
  local p
  for p in "${sln_projects[@]}"; do
    if [[ "$p" == "$needle" ]]; then
      return 0
    fi
  done
  return 1
}

declare -a missing=()
while IFS= read -r -d '' rel; do
  rel="mods/${rel#./}"
  rel="${rel//\\//}"
  if ! in_sln "$rel"; then
    missing+=("$rel")
  fi
done < <(cd "$MODS" && find . -type f -name '*.csproj' -print0)

if ((${#missing[@]} > 0)); then
  echo 'The following mod projects are missing from Cyberland.sln:'
  mapfile -t sorted < <(printf '%s\n' "${missing[@]}" | sort -u)
  for m in "${sorted[@]}"; do
    echo "  - $m"
  done
  exit 1
fi

echo 'All mod projects are present in Cyberland.sln.'
