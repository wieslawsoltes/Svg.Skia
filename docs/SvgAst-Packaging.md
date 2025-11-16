# Svg.Ast Packaging Guide

This document describes how to build and publish the `Svg.Ast` package once the API surface is considered stable.

## Prerequisites

- .NET SDK 8.0 or later
- Access to the project root (contains `src/Svg.Ast/Svg.Ast.csproj`)
- Valid NuGet API key (if pushing to nuget.org)

## 1. Verify code & tests

```bash
dotnet test Svg.Skia.sln -c Release --filter Svg.Ast.UnitTests
dotnet run -c Release --project tests/Svg.Ast.Benchmarks/Svg.Ast.Benchmarks.csproj
```

Ensure benchmarks/tests pass before packaging.

## 2. Update version

Edit `src/Svg.Ast/Svg.Ast.csproj` and set the desired version metadata (either via `<Version>` or standard `Directory.Build.props` if introduced). Optionally update release notes in `docs/SvgAst.md` or a CHANGELOG.

## 3. Pack the NuGet

From the repo root:

```bash
dotnet pack src/Svg.Ast/Svg.Ast.csproj -c Release -o ./artifacts/NuGet
```

This command uses the metadata baked into the csproj (`PackageId`, `PackageDescription`, `PackageReadmeFile`, `LicenseExpression`, XML docs). The resulting `.nupkg` file will be placed under `artifacts/NuGet`.

## 4. Verify package contents

Optional: inspect the package to ensure the README, XML docs, and license are embedded.

```bash
nuget inspect ./artifacts/NuGet/Svg.Ast.<version>.nupkg
```

Or open the `.nupkg` (it is a Zip file) and confirm the following files exist:

- `docs/SvgAst.md` (used as the package readme on NuGet)
- `LICENSE.TXT` (via license expression)
- `Svg.Ast.xml` (documentation file for IntelliSense)

## 5. Publish

```bash
dotnet nuget push ./artifacts/NuGet/Svg.Ast.<version>.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json
```

Replace the api key or use a private source as appropriate.

## 6. Consuming the package

Once published, consumers can reference the AST directly:

```bash
dotnet add package Svg.Ast
```

Usage is described in `docs/SvgAst.md` (parsing, traversal, rendering).
