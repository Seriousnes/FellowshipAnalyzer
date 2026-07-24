#!/usr/bin/env bash
# Emits a GitHub Actions job matrix covering every test project in the solution.
#
# A project counts as a test project when it is listed in the solution and its
# csproj references Microsoft.NET.Test.Sdk, so the matrix follows the solution
# rather than a hand-maintained list.
#
# Usage: discover-tests.sh [solution-path]
# Writes matrix=<json> and count=<n> to $GITHUB_OUTPUT (stdout when unset).

set -euo pipefail

solution="${1:-FellowshipAnalyzer.slnx}"

if [ ! -f "$solution" ]; then
  echo "::error::Solution '$solution' not found (working directory: $PWD)"
  exit 1
fi

candidates="$(dotnet sln "$solution" list | tr '\\' '/' | grep -E '\.csproj$' | sort || true)"

if [ -z "$candidates" ]; then
  echo "::error::'dotnet sln $solution list' returned no projects"
  exit 1
fi

entries=""
names=""
count=0

while IFS= read -r project; do
  [ -n "$project" ] || continue

  if [ ! -f "$project" ]; then
    echo "::error::Solution lists '$project' but no such file exists"
    exit 1
  fi

  grep -qF 'Microsoft.NET.Test.Sdk' "$project" || continue

  base="$(basename "$project" .csproj)"
  name="${base#FellowshipAnalyzer.}"
  name="${name%.Tests}"

  entries="${entries:+$entries,}{\"name\":\"$name\",\"project\":\"$project\"}"
  names="${names:+$names$'\n'}$name"
  count=$((count + 1))
done <<EOF
$candidates
EOF

if [ "$count" -eq 0 ]; then
  echo "::error::No test projects discovered in '$solution'"
  exit 1
fi

duplicates="$(printf '%s\n' "$names" | sort | uniq -d)"
if [ -n "$duplicates" ]; then
  echo "::error::Matrix names must be unique per project; duplicated: $(printf '%s' "$duplicates" | tr '\n' ' ')"
  exit 1
fi

matrix="{\"include\":[$entries]}"

echo "Discovered $count test projects:"
printf '%s\n' "$names" | sed 's/^/  - /'

{
  echo "matrix=$matrix"
  echo "count=$count"
} >> "${GITHUB_OUTPUT:-/dev/stdout}"
