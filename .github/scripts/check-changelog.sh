#!/usr/bin/env bash

set -euo pipefail

base="${1:?usage: check-changelog.sh <base-sha> <head-sha>}"
head="${2:?usage: check-changelog.sh <base-sha> <head-sha>}"

core_dir="src/FellowshipAnalyzer.Core/"
core_changelog="src/FellowshipAnalyzer.Core/Analysis/CoreChangelog.razor"
heroes_prefix="src/Heroes/FellowshipAnalyzer.Heroes."

changed="$(git diff --name-only "$base...$head")"

if [ -z "$changed" ]; then
  echo "No changed files."
  exit 0
fi

if grep -qxF "$core_changelog" <<<"$changed"; then
  echo "Core changelog updated; changelog requirement satisfied."
  exit 0
fi

failures=0

if grep -q "^$core_dir" <<<"$changed"; then
  echo "::error file=$core_changelog::Core changed but $core_changelog was not updated"
  failures=$((failures + 1))
fi

heroes="$(grep "^$heroes_prefix[^/]*/" <<<"$changed" | sed -E "s|^$heroes_prefix([^/]+)/.*|\1|" | sort -u || true)"

while IFS= read -r hero; do
  [ -n "$hero" ] || continue

  hero_changelog="$heroes_prefix$hero/Changelog.razor"

  if grep -qxF "$hero_changelog" <<<"$changed"; then
    echo "$hero changelog updated."
    continue
  fi

  echo "::error file=$hero_changelog::$hero changed but neither $hero_changelog nor $core_changelog was updated"
  failures=$((failures + 1))
done <<<"$heroes"

if [ "$failures" -gt 0 ]; then
  exit 1
fi

echo "Changelog requirement satisfied."
