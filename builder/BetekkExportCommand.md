# BetekkExportCommand Workflow

This document captures how the Revit `ExportXmi` ribbon button (defined in `App.cs`) drives the export workflow and how `BetekkExportCommand` orchestrates the sequential extraction of model data.

## High-Level Flow

1. **Button Click (Revit UI)**  
   `App.OnStartup` wires the `ExportXmi` button to `Betekk.RevitXmiExporter.BetekkExportCommand`.
2. **User Chooses Destination**  
   `BetekkExportCommand.Execute` prompts with a `SaveFileDialog`, ensuring the user chooses where to write the exported JSON and log files.
3. **Builder Pipeline**  
   `BetekkJsonExporter.Export` creates a `BetekkXmiBuilder`, invokes the sequential extraction stages, and calls `_manager.BuildJson`.
4. **Write Output + Notify**  
   `BetekkExportCommand` writes `<chosen-name>_xmi_export.json`, shows a success `TaskDialog`, or captures failures in `ModelInfoBuilder` logs.

## Sequential Extraction Stages

`BetekkXmiBuilder.BuildModel` executes these stages in order. Keep this list updated when adding new collectors.  
Before the stage loop runs, the builder scans placed elements to determine which materials and element
types are actually used; only those referenced entities are exported, which keeps template defaults or unused family types from generating noisy warnings.

| Order | Stage | Responsibility |
| ----- | ----- | -------------- |
| 1 | `MaterialLooper` | Gather every `Material` to seed XMI structural material definitions. |
| 2 | `CrossSectionLooper` | Visit Revit element types (`FloorType`, `WallType`, `FamilySymbol`, etc.) and map cross sections. |
| 3 | `StructuralPointConnectionLooper` | Build structural point connections from analytical nodes (`ReferencePoint`). |
| 4 | `StoreyLooper` | Extract `Level` elements representing structural storeys. |
| 5 | `StructuralCurveMemberLooper` | Map `AnalyticalMember` instances into structural curve members. |
| 6 | `StructuralSurfaceMemberLooper` | Convert `AnalyticalPanel` elements into structural surface members. |

When refactoring, keep the order stable so downstream mappers (e.g., surface members referencing point connections) find the entities created earlier in the sequence.

## Error Handling & Logging

- `BetekkExportCommand` uses `ModelInfoBuilder.SetLogDirectory` to place `info_log.txt` and `error_log.txt` beside the exported JSON.
- Each mapper writes category-specific warnings (e.g., missing materials in `CrossSectionMapper`).  
  These logs do not block export but highlight gaps in the model data.
- Exceptions propagate back to `BetekkExportCommand`, which shows the user a failure dialog and writes the stack trace to `error_log.txt`.

## Deployment Notes

- `RevitXmiExporter.addin` must point at the compiled `RevitXmiExporter.dll`.
- All dependencies (e.g., `xmi-schema-Csharp.Core.dll`) must reside next to the add-in DLL—use `dotnet publish` or the post-publish copy target in `RevitXmiExporter.csproj` to keep them synchronized.

## Future Enhancements

- Introduce progress reporting between stages to surface “Step 3/6” feedback in Revit.
- Allow optional filtering (e.g., export only selected categories) before invoking the builder.
- Persist the last export path in Revit’s journal or user profile for quicker repeats.
