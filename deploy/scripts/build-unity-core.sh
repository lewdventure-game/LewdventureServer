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

cp "$root/deploy/unity/README.md" "$output/"
cp "$root/deploy/unity/link.xml" "$output/"
cp "$root/deploy/unity/UnityCoreLog.cs" "$output/"

commit="$(git -C "$root" rev-parse --short HEAD 2>/dev/null || echo unknown)"
protocol="$(grep -oE 'ProtocolVersion = [0-9]+' "$root/src/Lewdventure.Server.Battle/Battles/Services/BattleSimulatorService.cs" | grep -oE '[0-9]+' | head -1)"

{
  echo "commit=$commit"
  echo "built=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "protocolVersion=$protocol"
  echo "targetFramework=$framework"
  echo "newtonsoft=13.0.2"
} > "$output/VERSION.txt"

echo "[unity-core] $output"
ls -1 "$output"
