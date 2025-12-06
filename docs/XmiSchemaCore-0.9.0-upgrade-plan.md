# XmiSchema.Core 0.9.0 Upgrade Plan

## Current Status
- **Current Version**: XmiSchema.Core 0.6.0
- **Target Version**: XmiSchema.Core 0.9.0
- **Date Created**: 2025-12-05

## Pre-Upgrade Investigation Phase

### 1. Package Discovery & Documentation Review
**Goal**: Identify breaking changes, new features, and migration requirements.

**Tasks**:
- [ ] Locate XmiSchema.Core 0.9.0 NuGet package and verify availability
  ```bash
  dotnet add package XmiSchema.Core --version 0.9.0 --dry-run
  ```
- [ ] Extract and review package documentation:
  - Check `~/.nuget/packages/xmischema.core/0.9.0/README.md` after package restore
  - Review `xmischema.core.nuspec` for dependency changes
  - Look for changelog, release notes, or migration guides
- [ ] Document breaking changes in `docs/XmiSchemaCore-0.9.0-review.md` (similar to 0.6.0 review)
- [ ] Identify deprecated APIs that were used in 0.6.0
- [ ] Check for new required dependencies or framework version changes

### 2. API Surface Comparison
**Goal**: Map changes between 0.6.0 and 0.9.0 APIs.

**Tasks**:
- [ ] Compare namespace structure (any new namespaces or reorganization?)
- [ ] Document changes to `IXmiManager` interface and implementation
- [ ] Review entity creation methods (any signature changes?):
  - `CreateStructuralMaterial`
  - `CreateStructuralCrossSection`
  - `CreateStructuralCurveMember`
  - `CreateStructuralSurfaceMember`
  - `CreateStructuralPointConnection`
  - `CreateStructuralStorey`
  - `CreateStructuralSegment`
- [ ] Check enum changes (`XmiMaterialTypeEnum`, etc.)
- [ ] Review helper utilities (`ExtensionEnumHelper`, geometry helpers)
- [ ] Document serialization behavior changes (`BuildJson()` method)

### 3. Dependency & Compatibility Check
**Goal**: Ensure all dependencies are compatible with 0.9.0.

**Tasks**:
- [ ] Verify .NET 8 compatibility (or check if 0.9.0 requires .NET 9)
- [ ] Check for conflicts with:
  - Newtonsoft.Json 13.0.3
  - WindowsAPICodePack-Shell 1.1.1
  - Microsoft.CodeAnalysis.NetAnalyzers 8.0.0
- [ ] Review Revit API compatibility (ensure no breaking changes for Revit 2026)
- [ ] Test on Windows x64 platform target

## Upgrade Implementation Phase

### Phase 1: Package Update & Build Verification
**Goal**: Update the package reference and resolve immediate compilation errors.

**Tasks**:
- [ ] Update `RevitXmiExporter.csproj` line 55:
  ```xml
  <PackageReference Include="XmiSchema.Core" Version="0.9.0" />
  ```
- [ ] Run package restore:
  ```bash
  dotnet restore RevitXmiExporter.csproj
  ```
- [ ] Attempt clean build to identify compilation errors:
  ```bash
  dotnet clean
  dotnet build RevitXmiExporter.sln
  ```
- [ ] Document all build errors and categorize by severity
- [ ] Create GitHub issue or tracking document for each breaking change category

### Phase 2: Builder Layer Updates
**Goal**: Update `Builder/` code to work with 0.9.0 API changes.

**Files to Review**:
- `builder/BetekkXmiBuilder.cs` (lines 1-217)
- `builder/BetekkExportCommand.cs` (lines 1-187)
- `builder/BetekkJsonExporter.cs`

**Tasks**:
- [ ] Update `BetekkXmiBuilder` constructor if `IXmiManager` initialization changed
- [ ] Review model initialization pattern (`_manager.Models = new List<XmiModel>`)
- [ ] Verify looper execution order still valid (materials → cross sections → points → storeys → members)
- [ ] Update `GetJson()` call if signature changed
- [ ] Handle any new required parameters in collection methods
- [ ] Add error handling for new validation rules

### Phase 3: Mapper Layer Updates
**Goal**: Update all `ClassMapper/` implementations to use 0.9.0 entity creation methods.

**Files to Review** (in dependency order):
1. `ClassMapper/Base/StructuralBaseEntityMapper.cs`
2. `ClassMapper/Point3DMapper.cs`
3. `ClassMapper/StructuralMaterialMapper.cs`
4. `ClassMapper/StructuralCrossSectionMapper.cs`
5. `ClassMapper/StructuralPointConnectionMapper.cs`
6. `ClassMapper/StructuralStoreyMapper.cs`
7. `ClassMapper/StructuralSegmentMapper.cs`
8. `ClassMapper/StructuralCurveMemberMapper.cs`
9. `ClassMapper/StructuralSurfaceMemberMapper.cs`

**Tasks**:
- [ ] Update `ExtractBasicProperties()` if identifier requirements changed
- [ ] Fix property naming (PascalCase enforcement: `Id`, `NativeId`, `IfcGuid`)
- [ ] Update `manager.CreateStructuralMaterial()` signature:
  - Check if new parameters added (sustainability metrics, certifications, etc.)
  - Verify enum handling still uses `ExtensionEnumHelper`
- [ ] Update `manager.CreateStructuralCrossSection()` if changed
- [ ] Fix geometry creation (`CreateStructuralCurveMember`, `CreateStructuralSurfaceMember`):
  - Check if segment/node requirements changed
  - Verify local axis string format
  - Update bounding/geometry payload structure
- [ ] Add deduplication checks if 0.9.0 has stricter uniqueness validation
- [ ] Update null handling and error logging for new validation rules

### Phase 4: Utility & Helper Updates
**Goal**: Ensure utility code works with 0.9.0 changes.

**Files to Review**:
- `Utils/EnumHelper.cs`
- `Utils/Converters.cs`
- `Utils/ModelInfoBuilder.cs`
- `Utils/NativeIdBuilder.cs`

**Tasks**:
- [ ] Verify `ExtensionEnumHelper.FromEnumValue<T>()` still works with new enums
- [ ] Update unit conversion if 0.9.0 changes geometry units
- [ ] Check if error logging format needs updates for schema validation errors
- [ ] Review GUID/ID generation if uniqueness requirements changed

### Phase 5: Test Updates & Validation
**Goal**: Update test scaffolding and validate exports with 0.9.0 schema.

**Files to Review**:
- `Test/EntityTest/TestStorey.cs`
- `Test/TestJsonGenerator.cs`
- Any commented-out harness commands in `App.cs`

**Tasks**:
- [ ] Update test fixture builders to match 0.9.0 entity structure
- [ ] Regenerate baseline JSON snapshots with 0.9.0 serialization
- [ ] Test export with real Revit sample models:
  - Simple model (1-2 elements)
  - Medium complexity (10-20 elements with materials/sections)
  - Complex model (full building with multiple storeys)
- [ ] Validate JSON output against 0.9.0 schema requirements
- [ ] Document any new validation errors or warnings
- [ ] Update `Test/README.md` with 0.9.0-specific testing notes

## Post-Upgrade Validation Phase

### 1. Functional Testing
**Checklist**:
- [ ] Plugin loads in Revit 2026 without errors
- [ ] Ribbon tab appears correctly
- [ ] ExportJson button clickable and responsive
- [ ] File save dialog appears
- [ ] JSON export completes without exceptions
- [ ] `error_log.txt` contains no unexpected warnings
- [ ] Export file size reasonable (not empty, not bloated)
- [ ] JSON is valid and parseable

### 2. Schema Validation Testing
**Checklist**:
- [ ] JSON validates against XMI schema 0.9.0 (if validator tool exists)
- [ ] All required entities present (materials, storeys, members)
- [ ] Entity relationships correctly represented
- [ ] No duplicate IDs in output
- [ ] All PascalCase properties correctly formatted
- [ ] Geometry data complete (segments, nodes, local axes)

### 3. Regression Testing
**Compare 0.6.0 vs 0.9.0 outputs**:
- [ ] Export same Revit model with both versions
- [ ] Compare JSON structure (note expected differences)
- [ ] Verify no data loss in migration
- [ ] Document any intentional schema changes
- [ ] Update documentation if export format changed

### 4. Performance Testing
**Benchmarks**:
- [ ] Time export of small model (< 10 elements): ___ seconds
- [ ] Time export of medium model (10-50 elements): ___ seconds
- [ ] Time export of large model (100+ elements): ___ seconds
- [ ] Compare against 0.6.0 baseline (document any significant changes)
- [ ] Check memory usage during export
- [ ] Verify no memory leaks

## Documentation Updates

### Code Documentation
- [ ] Update inline code comments referencing 0.6.0 behavior
- [ ] Add XML doc comments for any new methods
- [ ] Document new parameters or property requirements
- [ ] Update examples in mapper classes

### Repository Documentation
- [ ] Update `README.md` with 0.9.0 version reference
- [ ] Update `PLAN.md` Phase 2 status (mark 0.9.0 upgrade complete)
- [ ] Create `docs/XmiSchemaCore-0.9.0-review.md` with findings
- [ ] Update `AGENTS.md` if coding patterns changed
- [ ] Add migration notes for future developers

### User Documentation
- [ ] Update installation instructions if needed
- [ ] Document any new export options or features
- [ ] Update troubleshooting guide with 0.9.0-specific issues
- [ ] Refresh screenshots if UI changed

## Rollout Strategy

### 1. Version Increment
- [ ] Update `Directory.Build.props` `VersionPrefix` to `0.3.0` (minor version bump for dependency upgrade)
- [ ] Update assembly metadata to reflect 0.9.0 dependency
- [ ] Tag commit: `git tag -a v0.3.0 -m "Upgrade to XmiSchema.Core 0.9.0"`

### 2. Build & Package
- [ ] Clean build in Release mode:
  ```bash
  dotnet clean
  dotnet build RevitXmiExporter.sln -c Release
  ```
- [ ] Publish to deployment folder:
  ```bash
  dotnet publish RevitXmiExporter.csproj -c Release -o dist/v0.3.0/Plugin
  ```
- [ ] Verify all dependencies copied to output folder
- [ ] Test installation from dist folder

### 3. Pilot Deployment
- [ ] Deploy to test environment (clean Revit 2026 installation)
- [ ] Run smoke tests with 3-5 sample models
- [ ] Collect feedback from pilot users
- [ ] Document any issues discovered
- [ ] Fix critical bugs before wider rollout

### 4. Production Deployment
- [ ] Update Revit add-in manifest if paths changed
- [ ] Deploy to `C:\ProgramData\Autodesk\Revit\Addins\2026\BetekkRevitXmiExporter\`
- [ ] Notify users of upgrade and any breaking changes
- [ ] Provide rollback instructions (keep 0.6.0 version available)
- [ ] Monitor for issues in first week

## Rollback Plan

If critical issues are discovered after upgrade:

1. **Immediate Rollback**:
   ```bash
   git checkout tags/v0.2.0
   dotnet restore
   dotnet publish RevitXmiExporter.csproj -c Release -o dist/rollback/Plugin
   ```

2. **Revert Package**:
   - Edit `RevitXmiExporter.csproj` line 55: change version back to `0.6.0`
   - Run `dotnet restore` and rebuild

3. **Document Issues**:
   - Create GitHub issues for each blocking problem
   - Note workarounds or temporary fixes
   - Identify root cause before retry

## Risk Assessment

### High Risk Items
- **Breaking changes in entity creation methods**: May require significant mapper refactoring
- **Schema validation changes**: Could invalidate existing export workflows
- **Geometry format changes**: May break downstream consumers of JSON

### Medium Risk Items
- **Enum changes**: Relatively easy to fix but could affect all mappers
- **Performance regressions**: May require optimization work
- **New dependencies**: Could introduce compatibility issues

### Low Risk Items
- **Property naming changes**: Mechanical find/replace operations
- **Documentation updates**: No code impact
- **Minor version bumps**: Generally backward compatible

## Success Criteria

Upgrade is considered successful when:
- ✅ All code compiles without errors or warnings
- ✅ All functional tests pass
- ✅ JSON validates against 0.9.0 schema
- ✅ No performance regression > 10%
- ✅ Documentation updated and accurate
- ✅ Pilot users report no blocking issues
- ✅ Rollback plan tested and confirmed working

## Timeline Estimate

- **Pre-Upgrade Investigation**: 2-4 hours
- **Phase 1 (Package Update)**: 1-2 hours
- **Phase 2 (Builder Updates)**: 2-4 hours
- **Phase 3 (Mapper Updates)**: 4-8 hours
- **Phase 4 (Utility Updates)**: 1-2 hours
- **Phase 5 (Test Updates)**: 2-4 hours
- **Post-Upgrade Validation**: 2-3 hours
- **Documentation**: 2-3 hours
- **Total Estimated**: 16-30 hours

## Notes & Findings

_Use this section to document discoveries during the upgrade process_

### Breaking Changes Identified
-

### New Features in 0.9.0
-

### Workarounds Implemented
-

### Outstanding Questions
-
