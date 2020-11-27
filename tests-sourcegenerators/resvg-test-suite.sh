#!/usr/bin/env bash
mkdir temp
cd ./temp
git clone https://github.com/wieslawsoltes/resvg-test-suite.git
mkdir resvgtestsuite
dotnet run -p ../../samples/svgc/svgc.csproj -c Release -- -j ../resvg-test-suite.json
dotnet new console -n resvgtestsuite -o resvgtestsuite
dotnet add ./resvgtestsuite/resvgtestsuite.csproj package -v 2.80.2 SkiaSharp
dotnet add ./resvgtestsuite/resvgtestsuite.csproj package -v 2.80.2 SkiaSharp.NativeAssets.Linux
dotnet build ./resvgtestsuite/resvgtestsuite.csproj
