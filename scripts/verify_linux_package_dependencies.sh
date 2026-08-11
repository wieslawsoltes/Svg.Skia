#!/usr/bin/env bash

set -euo pipefail

packages_dir="${1:-artifacts/packages}"

if [[ ! -d "$packages_dir" ]]; then
  echo "Package directory does not exist: $packages_dir" >&2
  exit 1
fi

core_package="$(find "$packages_dir" -maxdepth 1 -type f -name 'Svg.Skia.[0-9]*.nupkg' -print -quit)"
controls_package="$(find "$packages_dir" -maxdepth 1 -type f -name 'Svg.Controls.Skia.Avalonia.[0-9]*.nupkg' -print -quit)"

if [[ -z "$core_package" || -z "$controls_package" ]]; then
  echo "Svg.Skia and Svg.Controls.Skia.Avalonia packages are required." >&2
  exit 1
fi

controls_file="$(basename "$controls_package")"
controls_version="${controls_file#Svg.Controls.Skia.Avalonia.}"
controls_version="${controls_version%.nupkg}"

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

unzip -p "$core_package" Svg.Skia.nuspec > "$work_dir/Svg.Skia.nuspec"
unzip -p "$controls_package" Svg.Controls.Skia.Avalonia.nuspec > "$work_dir/Svg.Controls.Skia.Avalonia.nuspec"

if grep -q '<dependency id="SkiaSharp.NativeAssets.Linux"' "$work_dir/Svg.Skia.nuspec"; then
  echo "Svg.Skia must let applications choose their Linux native asset package." >&2
  exit 1
fi

managed_version="$(sed -n 's/.*<dependency id="SkiaSharp" version="\([^"]*\)".*/\1/p' "$work_dir/Svg.Skia.nuspec" | sort -u)"
native_version="$(sed -n 's/.*<dependency id="SkiaSharp.NativeAssets.Linux" version="\([^"]*\)".*/\1/p' "$work_dir/Svg.Controls.Skia.Avalonia.nuspec" | sort -u)"

if [[ -z "$managed_version" || "$managed_version" == *$'\n'* ]]; then
  echo "Svg.Skia must declare exactly one managed SkiaSharp version." >&2
  exit 1
fi

if [[ -z "$native_version" || "$native_version" == *$'\n'* ]]; then
  echo "Svg.Controls.Skia.Avalonia must declare exactly one Linux native SkiaSharp version." >&2
  exit 1
fi

if [[ "$managed_version" != "$native_version" ]]; then
  echo "Managed SkiaSharp $managed_version does not match Linux native SkiaSharp $native_version." >&2
  exit 1
fi

dotnet new console \
  --name LinuxConsumer \
  --output "$work_dir/consumer" \
  --framework net10.0 \
  --no-restore

dotnet package add "Svg.Controls.Skia.Avalonia@$controls_version" \
  --project "$work_dir/consumer/LinuxConsumer.csproj" \
  --no-restore

dotnet restore "$work_dir/consumer/LinuxConsumer.csproj" \
  --runtime linux-x64 \
  --source "$packages_dir" \
  --source https://api.nuget.org/v3/index.json

dotnet publish "$work_dir/consumer/LinuxConsumer.csproj" \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  --no-restore \
  --output "$work_dir/publish"

assets_file="$work_dir/consumer/obj/project.assets.json"

grep -q "\"SkiaSharp/$managed_version\"" "$assets_file"
grep -q "\"SkiaSharp.NativeAssets.Linux/$native_version\"" "$assets_file"

if [[ ! -f "$work_dir/publish/libSkiaSharp.so" ]]; then
  echo "The linux-x64 consumer did not publish libSkiaSharp.so." >&2
  exit 1
fi

echo "Verified linux-x64 package graph with SkiaSharp $managed_version."
