#!/usr/bin/env bash

set -euo pipefail

version="${1:-4.15.1}"
asset="Azure.Functions.Cli.linux-x64.${version}.zip"
url="https://github.com/Azure/azure-functions-core-tools/releases/download/${version}/${asset}"
destination="${RUNNER_TEMP:-/tmp}/functions-core-tools-${version}"

curl -fsSL --retry 3 -o "${destination}.zip" "$url"

expected="$(curl -fsSL --retry 3 "${url}.sha2" | tr -d '[:space:]' | tr '[:upper:]' '[:lower:]')"
actual="$(sha256sum "${destination}.zip" | cut -d' ' -f1)"
if [ "$expected" != "$actual" ]; then
  echo "::error::Checksum mismatch for $asset (expected $expected, got $actual)"
  exit 1
fi

unzip -q -o "${destination}.zip" -d "$destination"
rm "${destination}.zip"
chmod +x "$destination/func"
[ -f "$destination/gozip" ] && chmod +x "$destination/gozip"

echo "$destination" >> "${GITHUB_PATH:-/dev/null}"
"$destination/func" --version
