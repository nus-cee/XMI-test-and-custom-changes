# XmiSchema.Core 0.6.0 Review Notes

## Sources Reviewed
- NuGet metadata stored locally at `/mnt/c/Users/darel/.nuget/packages/xmischema.core/0.6.0/README.md` and `xmischema.core.nuspec`.
- Sample usage embedded in the README (manager + storey/material/segment creation snippet).

## Observed API/Behavior Shifts
- The package now targets .NET 8 only; all downstream builders must execute inside a net8.0 context and call into the Windows Desktop targeting pack for UI entry points.
- `IXmiManager` models are explicit objects—consumers must populate `manager.Models` and address entities through a model index (the README sample initializes `new XmiModel()` before building graph content).
- Entity identifiers follow PascalCase (`Id`, `NativeId`, `IfcGuid`). Any legacy code that referenced `ID`/`NativeID` or custom struct wrappers must be renamed accordingly.
- Constructors enforce richer geometry payloads (e.g., `CreateStructuralCurveMember` now demands `List<XmiStructuralPointConnection>` and `List<XmiSegment>` objects plus local axis strings; surface members mirror that contract).
- Material helpers rely on the enum helpers (`ExtensionEnumHelper.FromEnumValue<T>()`) to hydrate `XmiStructuralMaterialTypeEnum` rather than magic numbers.

## Builder Implications
- `builder/XmiBuilder.cs`: we already initialize `_manager.Models` with a `new XmiModel()`, which satisfies the new multi-model requirement. However, `BuildModel` never calls `StructuralMaterialLooper` or `StructuralCrossSectionLooper`, so the schema graph misses materials/sections that the 0.6.0 sample expects for downstream members. Action: insert those loops before curve/surface mappers.
- `builder/ExportCommand.cs` and `JsonExporter.cs` do not handle missing payloads gracefully. If new schema validation requires non-empty cross sections, we need to surface actionable TaskDialog messages (e.g., “missing materials detected”) before writing JSON.

## ClassMapper Implications
- `classMapper/StructuralMaterialMapper.cs`: confirm that every return path sets `materialType`, `grade`, and modulus strings. Release requirements emphasize typed values; add guards/logging for `StructuralAssetId` that fails to resolve.
- `classMapper/StructuralCurveMemberMapper.cs`: the new `CreateStructuralCurveMember` signature expects actual `segments` entries. We currently pass an empty list. Implement `StructuralSegmentMapper` wiring (there is a mapper skeleton but it is unused) and include torsion/bending releases derived from the Revit member when available.
- `classMapper/StructuralSurfaceMemberMapper.cs`: same issue as curve members—the method fabricates empty `nodes`/`segments`. Populate those collections through the existing `StructuralPointConnectionMapper` and `StructuralSegmentMapper` so JSON consumers receive polygons that satisfy 0.6.0 validation.
- `classMapper/StructuralPointConnectionMapper.cs` & `StructuralStoreyMapper.cs`: release docs highlight unique IDs per native entity. Ensure the current strategy (creating a new storey per level) de-duplicates entries through `manager.GetEntitiesOfType<T>()` before creating duplicates, otherwise `BuildJson` may emit invalid duplicates.

## Follow-Up Actions
1. Update `XmiBuilder.BuildModel` to loop materials and cross sections, then re-run MSBuild to verify no missing references.
2. Wire `StructuralSegmentMapper` into curve/surface mappers so `List<XmiSegment>` is never empty.
3. Add safeguards/deduplication for storey/material creation based on `NativeId` comparisons in the manager cache.
4. Append validation logs (or TaskDialog warnings) when the new schema contract cannot be satisfied, enabling future unit tests to assert on these paths.
