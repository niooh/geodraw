# GeoDraw
Minimal geometry editor built with Avalonia (.NET 10).

## Features
- Draw points, lines, rectangles, circles, ellipses
- Transform: rotate, reflect
- Connect selected points into lines or beziers
- Undo / Redo (Ctrl+Z / Ctrl+Y)
- Import / Export JSON, SVG, PNG
- Object ID highlighting, optional ID labels
- CLI box: `select p1 l2`, `which p1`, `clear`, `help`

## Run

```sh
dotnet run
```

## Publish

Take Windows software as an example:

```pwsh
dotnet publish -c Release -r win-x64 --no-self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o .\publish\win-x64-fd-single
```

## License
MIT, Copyright (c) 2026 niooh
