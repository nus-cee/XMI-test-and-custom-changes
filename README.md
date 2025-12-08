# Revit XMI Exporter

An Autodesk Revit add-in that exports structural model data to [XMI Schema](https://github.com/xmi-schema) JSON format.

## Overview

The Revit XMI Exporter plugin extracts structural framing elements (beams, columns), analytical members, materials, cross-sections, and geometry from Autodesk Revit models and exports them to the XMI Schema JSON format. This enables interoperability between Revit and other structural engineering applications that support XMI Schema.

## Features

- **Dual Representation Export**: Exports both physical elements (beams, columns) and their analytical representations
- **Structural Elements**: Beams, columns, and analytical curve members
- **Geometry Export**: 3D points with coordinate deduplication (1e-10 tolerance)
- **Materials**: Extracts material properties including elastic modulus, shear modulus, Poisson's ratio, density, and thermal expansion coefficient
- **Cross-Sections**: Supports rectangular, circular, and custom section profiles with automatic parameter extraction
- **Storeys/Levels**: Exports Revit levels as XMI storeys with elevation data
- **Structural Connections**: Creates analytical point connections at member endpoints
- **IFC GUID Preservation**: Preserves existing IFC GUIDs from Revit when available
- **Export Statistics**: Displays summary of exported elements upon completion

## Prerequisites

- **Autodesk Revit 2026** (or compatible version)
- **.NET 8.0 Runtime**
- **Windows 10/11** (x64)

## Installation

### Option 1: From Release Build

1. Download the latest release from the [Releases](https://github.com/xmi-schema/revit-export-plugin/releases) page
2. Extract the contents to:
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2026\BetekkRevitXmiExporter\
   ```
3. Copy the `RevitXmiExporter.addin` file to:
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2026\
   ```
4. Restart Revit

### Option 2: Build from Source

See [Building from Source](#building-from-source) section below.

## Usage

### Exporting a Revit Model

1. Open a Revit project containing structural elements
2. Navigate to the **XMI Schema** ribbon tab
3. Click the **Export XMI** button in the "Export XMI" panel
4. Choose a destination folder and filename for the JSON export
5. Click **Save**

### Export Dialog

Upon successful export, a dialog displays export statistics:
- Number of storeys, beams, columns
- Analytical members count
- Materials and cross-sections
- 3D points and structural connections

The exported JSON file follows the [XMI Schema specification](https://github.com/xmi-schema/xmi-schema).

### Error Handling

If an error occurs during export:
- An error dialog displays the issue
- Detailed logs are written to `error_log.txt` in the export directory
- Include this file when reporting issues

## Project Structure

```
revit-export-plugin/
├── App.cs                          # Revit entry point, ribbon UI setup
├── RevitXmiExporter.addin          # Revit add-in manifest
├── RevitXmiExporter.csproj         # Project file
├── RevitXmiExporter.sln            # Solution file
├── builder/
│   ├── BetekkRevitModelToXmiExportCommand.cs  # Export command handler
│   ├── BetekkRevitToXmiModelManager.cs        # Export orchestration
│   └── BetekkXmiBuilder.cs                    # XMI model builder
├── Utils/
│   ├── Converters.cs               # Unit conversion utilities
│   ├── EnumHelper.cs               # Enum utilities
│   ├── ModelInfoBuilder.cs         # Logging utilities
│   └── NativeIdBuilder.cs          # ID generation utilities
├── Properties/
│   ├── AssemblyInfo.cs             # Assembly metadata
│   └── launchSettings.json         # Debug launch settings
└── Tests/                          # Test harness
```

## Building from Source

### Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK
- Autodesk Revit 2026 installed (for API references)

### Build Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/xmi-schema/revit-export-plugin.git
   cd revit-export-plugin
   ```

2. Set the Revit installation path environment variable:
   ```bash
   set RevitInstallDir=C:\Program Files\Autodesk\Revit 2026
   ```

3. Restore NuGet packages and build:
   ```bash
   dotnet restore
   dotnet build --configuration Release
   ```

4. The build automatically copies files to:
   - Add-in manifest: `C:\ProgramData\Autodesk\Revit\Addins\2026\`
   - Plugin DLLs: `C:\ProgramData\Autodesk\Revit\Addins\2026\BetekkRevitXmiExporter\`

### Debugging

1. Open `RevitXmiExporter.sln` in Visual Studio
2. Set build configuration to `Debug`
3. Start debugging (F5) - this will launch Revit with the add-in loaded
4. Open a Revit model and use the Export XMI command

## Dependencies

| Package | Version | Description |
|---------|---------|-------------|
| [XmiSchema](https://www.nuget.org/packages/XmiSchema) | 0.11.0 | XMI Schema library for model creation and JSON serialization |
| [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) | 13.0.4 | JSON serialization |
| RevitAPI | 2026 | Autodesk Revit API (referenced from Revit installation) |
| RevitAPIUI | 2026 | Autodesk Revit UI API (referenced from Revit installation) |

## Supported Elements

### Physical Elements
- **Beams** (OST_StructuralFraming category)
- **Columns** (OST_StructuralColumns category)

### Analytical Elements
- **Analytical Members** (AnalyticalMember class)
- **Structural Point Connections** (auto-generated at endpoints)

### Properties Exported
- Element geometry (start/end points, length)
- Material properties (type, grade, elastic/shear modulus, density)
- Cross-section profiles (shape, dimensions, area)
- Level/storey associations
- IFC GUIDs (when available)
- Local coordinate axes

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes (`git commit -m 'Add your feature'`)
4. Push to the branch (`git push origin feature/your-feature`)
5. Open a Pull Request

## License

This project is developed by [BETEKK Pte Ltd](https://www.betekk.com).

## Links

- [XMI Schema Repository](https://github.com/xmi-schema/xmi-schema)
- [XMI Schema NuGet Package](https://www.nuget.org/packages/XmiSchema)
- [Autodesk Revit API Documentation](https://www.revitapidocs.com/)

## Version History

| Version | Description |
|---------|-------------|
| 0.2.0 | Current release with XmiSchema v0.11.0 support |
