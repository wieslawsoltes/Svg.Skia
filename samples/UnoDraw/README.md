# UnoDraw

A native SVG design workspace built with Uno Platform, using the Svg.Skia document and rendering pipeline throughout.

```sh
git submodule update --init --recursive
dotnet run --project samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop
```

Pass an SVG path after `--` to open it at startup. The default desktop target runs on Windows, macOS and Linux/X11 and requires no mobile workloads.

The same workspace also has opt-in WebAssembly, Android and iOS targets. For example:

```sh
dotnet workload install wasm-tools
dotnet publish samples/UnoDraw/UnoDraw.csproj -c Release -f net10.0-browserwasm -p:UnoDrawTargetFrameworks=net10.0-browserwasm
```

The editor guide includes mobile build commands and required SDKs. Browser file uploads save through a new destination or download; Android and iOS use native file-provider capabilities. The responsive menu exposes layers, the inspector and every editor command at narrow widths.

On Linux, use a desktop session with D-Bus and an XDG desktop portal backend for
native Open and Save dialogs (for example, `xdg-desktop-portal-gtk` or the KDE
backend). The renderer uses the host's accelerated backend when available and
falls back to software when necessary.

See [the editor guide](../../docs/unodraw.md) for architecture, controls, validation and feature coverage.
