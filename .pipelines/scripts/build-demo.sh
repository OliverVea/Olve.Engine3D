#!/bin/sh
# Publishes the browser synthesizer sample. Emscripten needs python3 and downloads the emdawnwebgpu port on the
# first build; temporary files go to the container's filesystem (/olve is a small emptyDir).
set -e
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq --no-install-recommends python3 >/dev/null

export TMPDIR=/work/tmp
mkdir -p "$(dirname "$WORK")" "$OUT" "$TMPDIR"
cp -a "$TREE" "$WORK"
cd "$WORK"

dotnet workload install wasm-tools
dotnet publish samples/Olve.Engine3D.Samples.Synth.Web/Olve.Engine3D.Samples.Synth.Web.csproj \
  --configuration Release --output "$OUT"
ls "$OUT/wwwroot"
