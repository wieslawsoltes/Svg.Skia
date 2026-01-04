# Project Guidelines for Codex Agents

This repository contains the Svg.Skia library and associated tests. The following rules apply when creating pull requests with the Codex agent.

## References
- SVG 1.1 specification: https://www.w3.org/TR/SVG11/
- Avalonia UI code: https://github.com/AvaloniaUI/Avalonia
- Avalonia UI documentation: https://github.com/AvaloniaUI/avalonia-docs
- Free .NET decompiler (ILSpy): https://github.com/icsharpcode/ILSpy

## Workflow
1. After cloning the repo run `git submodule update --init --recursive` to fetch external dependencies.
2. Follow the coding conventions enforced by `.editorconfig`. Run `dotnet format --no-restore` before committing to ensure style compliance.
3. Build the solution using `dotnet build Svg.Skia.slnx -c Release`.
4. Run all tests with `dotnet test Svg.Skia.slnx -c Release`.

## Commit Guidelines
- Use concise commit messages summarizing your change in under 72 characters.
- Additional description lines may follow the summary if necessary.

## Pull Request Guidelines
- The pull request description should briefly explain what was changed and reference any relevant specification sections or documentation when appropriate.
