# Execution Plan for Revit Export Plugin

## Objectives
- Formalize version control so releases, hotfixes, and installer assets stay traceable.
- Ship a guided installer that drops the add-in manifest and DLLs into the correct Revit `Addins/2025` path.
- Replace the legacy XmiSchema dependency with NuGet `XmiSchema.Core` 0.6.0 and align all property names with .NET guidelines.

## Phase 1 – Baseline & Version Control Enhancements
1. Audit the repository: inventory current branches, tags, CI workflows, and external assets (Revit DLL references in `Revit_to_XMI.csproj`).
2. Adopt a branching model (e.g., `main` + `develop` + `feature/*`) and add `CONTRIBUTING.md` notes that all plugin work lands through PRs with linked issues.
3. Introduce semantic versioning: add `Directory.Build.props` with a central `<Version>` value, create annotated tags (`vX.Y.Z`), and update the solution metadata so installer packages display the same version.
4. Configure Git attributes/hooks: enforce `.editorconfig`, block Autodesk binaries via `.gitignore`, and optionally add pre-push validation (`dotnet format`, `dotnet build`).

## Phase 2 – Dependency & Library Cleanup
1. Review `XmiSchema.Core` 0.6.0 release notes for API breaking changes; document adjustments needed in `builder/` and `classMapper/`.
2. Update `Revit_to_XMI.csproj` to reference the new package version and run `dotnet restore`.
3. Refactor code to meet naming conventions: ensure properties use PascalCase, remove redundant Hungarian prefixes, and add analyzers (e.g., `Microsoft.CodeAnalysis.NetAnalyzers`) if gaps remain.
4. Update or add unit tests under `test/` to cover library contract changes (e.g., serialization expectations, schema validation) and run `dotnet test`.

## Phase 3 – Installer & Distribution Pipeline
1. Define the add-in bundle layout: `InstallerPayload/Applications/Revit/Addins/2025/Revit_to_XMI.addin` + `ApplicationFiles/` with compiled DLLs and config files.
2. Choose an installer technology (WiX v4 MSI, `Inno Setup`, or MSIX). Scaffold scripts under `installer/` that copy the publish output, write the `.addin` manifest with correct assembly paths, and register uninstall info.
3. Extend `.csproj` with a `PublishProfile` targeting `bin/Release/Publish` and ensure the pipeline produces Release builds with strong naming and file version info.
4. Create a `build_installer.ps1` (or Cake script) that performs: `dotnet publish`, copies payload into the installer project, runs the installer build, and outputs signed binaries into `dist/`.
5. Update documentation (AGENTS.md + README) with end-user instructions (download installer, run, restart Revit) and rollback guidance.

## Phase 4 – Automation, QA, and Release
1. Add a GitHub Actions workflow (or Azure DevOps pipeline) that triggers on tags, executes formatting/build/test jobs on Windows runners, uploads build logs, and stores installer artifacts.
2. Introduce signing (Authenticode certificate) to avoid SmartScreen warnings; include TODOs if certificate procurement is pending.
3. Pilot the installer on clean Revit 2025 environments, validate automatic placement in `%APPDATA%/Autodesk/Revit/Addins/2025`, and smoke-test export workflows.
4. Update release checklist: tag commit, verify CI artifacts, publish GitHub Release with installer + checksum, and notify stakeholders of upgrade instructions.
