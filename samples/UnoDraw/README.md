# UnoDraw

A native SVG design workspace built with Uno Platform, using the Svg.Skia document and rendering pipeline throughout.

```sh
git submodule update --init --recursive
dotnet run --project samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop
```

Pass an SVG path after `--` to open it at startup. The desktop head runs on Windows, macOS and Linux/X11. No mobile workloads are required.

On Linux, use a desktop session with D-Bus and an XDG desktop portal backend for
native Open and Save dialogs (for example, `xdg-desktop-portal-gtk` or the KDE
backend). The renderer uses the host's accelerated backend when available and
falls back to software when necessary.

See [the editor guide](../../docs/unodraw.md) for architecture, controls, validation and feature coverage.
