#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
output="${1:-$root/out/unity-core}"
framework="netstandard2.1"
binaries="$root/src/Lewdventure.Server.Battle/bin/Release/$framework"

dotnet build "$root/src/Lewdventure.Server.Battle/Lewdventure.Server.Battle.csproj" -c Release -f "$framework"

rm -rf "$output"
mkdir -p "$output"

cp "$binaries/Lewdventure.Server.Contracts.dll" "$output/"
cp "$binaries/Lewdventure.Server.GameConfig.dll" "$output/"
cp "$binaries/Lewdventure.Server.Battle.dll" "$output/"

echo "[unity-core] $output"
ls -1 "$output"
