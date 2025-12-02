# Revit Export Plugin

## Overview
This repository contains a .NET 8 add-in for Autodesk Revit 2025 that exports structural entities (storeys, segments, materials, point connections) to JSON aligned with the XMI schema. The add-in registers a ribbon tab (`XMI-Schema`) with an `ExportJson` button that marshals model data through dedicated builders and mappers before serializing via `XmiSchema.Core`.

## Features
- Ribbon integration for one-click JSON exports from within Revit.
- Modular builders (`builder/`) and mappers (`classMapper/`) that keep schema translation isolated from UI concerns.
- Utility helpers (`utils/`) for naming, geometry conversion, and Revit lookups.
- Test scaffolding (`test/`) that generates deterministic JSON payloads for regression checks.

## Prerequisites
- Autodesk Revit 2025 with `RevitAPI.dll` and `RevitAPIUI.dll` accessible one directory above the repo (`../Revit/Revit 2025/`).
- .NET SDK 8.0+ on Windows x64.
- Visual Studio 2022 or the `dotnet` CLI.

## Project Layout
- `App.cs` - Revit `IExternalApplication` entry point that builds ribbon UI.
- `builder/ExportCommand.cs` - Implements `IExternalCommand` invoked from Revit, orchestrating `XmiBuilder` and `JsonExporter`.
- `classMapper/` - Type-specific converters that project Revit entities to the schema types.
- `utils/` - Cross-cutting helpers (naming services, document traversal, validation).
- `test/` - Lightweight entity builders and JSON generators to validate serialization logic offline.
- `AGENTS.md` - contributor workflow guide; `PLAN.md` - roadmap for installer/versioning work.

## Build & Publish
```bash
# Restore NuGet packages and COM interop proxies
dotnet restore RevitXmiExporter.csproj

# Compile Debug build for quick validation
dotnet build RevitXmiExporter.sln

# Produce Release payload ready for deployment
dotnet publish RevitXmiExporter.csproj -c Release -o dist/Plugin
```
The publish output contains `RevitXmiExporter.dll`, dependencies, and configuration files required by Revit.

## Manual Installation
1. Ensure Revit is closed.
2. Copy `dist/Plugin` contents to `%AppData%/Autodesk/Revit/Addins/2025/RevitXmiExporter/`.
3. Create or update `%AppData%/Autodesk/Revit/Addins/2025/RevitXmiExporter.addin` referencing the DLL location:
   ```xml
   <RevitAddIns>
     <AddIn Type="Command">
       <Name>RevitXmiExporter</Name>
       <Assembly>C:\Users\%USERNAME%\AppData\Roaming\Autodesk\Revit\Addins\2025\RevitXmiExporter\RevitXmiExporter.dll</Assembly>
       <AddInId>8D83E68A-B739-4ACD-A9DB-1BC78F315B08</AddInId>
       <FullClassName>Betekk.RevitXmiExporter.ExportCommand</FullClassName>
       <VendorId>RXP</VendorId>
       <VendorDescription>Revit to XMI Exporter</VendorDescription>
     </AddIn>
   </RevitAddIns>
   ```
4. Launch Revit; open any model and click `XMI-Schema` -> `ExportJson`.

## Testing
Testing is currently manual plus lightweight serialization checks:
- Use the builders under `test/EntityTest/` to create fixture data and compare JSON snapshots.
- When unit tests are added to the solution, run `dotnet test RevitXmiExporter.sln` locally and in CI.
- Document manual export scenarios (sample models, parameters toggled) under `test/README.md` as you expand coverage.

## Contributing & Roadmap
Follow `AGENTS.md` for coding style, commit conventions, and PR expectations. Upcoming work-installer automation, semantic versioning, and the upgrade to `XmiSchema.Core` 0.6.0-is tracked in `PLAN.md`. Feel free to open issues or PRs referencing the relevant plan phase.
