#!/usr/bin/env bash
mkdir temp
cd ./temp
git clone https://github.com/wieslawsoltes/SVG.git
mkdir W3CTestSuite
dotnet run -p ../../samples/svgc/svgc.csproj -c Release -- -j  ../W3CTestSuite.json
dotnet new console -n W3CTestSuite -o W3CTestSuite
dotnet add ./W3CTestSuite/W3CTestSuite.csproj package -v 2.80.2 SkiaSharp
dotnet add ./W3CTestSuite/W3CTestSuite.csproj package -v 2.80.2 SkiaSharp.NativeAssets.Linux
dotnet build ./W3CTestSuite/W3CTestSuite.csproj
