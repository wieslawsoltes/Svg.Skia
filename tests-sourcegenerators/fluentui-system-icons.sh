#!/usr/bin/env bash
mkdir temp
cd ./temp
git clone https://github.com/microsoft/fluentui-system-icons.git
mkdir fluentui
dotnet run -p ../../samples/svgc/svgc.csproj -c Release -- -j ../fluentui-system-icons.json
dotnet new console -n fluentui -o fluentui
dotnet add ./fluentui/fluentui.csproj package -v 2.80.2 SkiaSharp
dotnet add ./fluentui/fluentui.csproj package -v 2.80.2 SkiaSharp.NativeAssets.Linux
dotnet build ./fluentui/fluentui.csproj
