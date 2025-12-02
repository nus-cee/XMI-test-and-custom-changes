# Execution Plan for Revit Export Plugin

## Objectives
- Formalize version control so releases, hotfixes, and installer assets stay traceable.
- Ship a guided installer that drops the add-in manifest and DLLs into the correct Revit `Addins/2025` path.
- Replace the legacy XmiSchema dependency with NuGet `XmiSchema.Core` 0.6.0 and align all property names with .NET guidelines.

## Current Status Snapshot
- **Phase 1** – Completed. Repo renamed to `Betekk.RevitXmiExporter`, semantic versioning lives in `Directory.Build.props`, Git hygiene files plus pre-push hook are in place, and contributor docs describe the branching and PR flow.
- **Phase 2** – In progress. Package upgraded to `XmiSchema.Core` 0.6.0 and code adjusted for new identifiers, but we still need a final audit against release notes and refreshed unit tests.
- **Phase 3** – Not started. Awaiting dependency stabilization before scaffolding the installer workspace.
- **Phase 4** – Not started. CI/CD, signing, and rollout sequencing depend on the installer deliverables.

## Phase 1 - Baseline & Version Control Enhancements ✅
1. Audit the repository: inventory current branches, tags, CI workflows, and external assets (Revit DLL references in `RevitXmiExporter.csproj`). **Done** via initial repo review.
2. Adopt a branching model (e.g., `main` + `develop` + `feature/*`) and add `CONTRIBUTING.md` notes that all plugin work lands through PRs with linked issues. **Done** and published in the documentation set.
3. Introduce semantic versioning: add `Directory.Build.props` with a central `<Version>` value, create annotated tags (`vX.Y.Z`), and update the solution metadata so installer packages display the same version. **Done** with `VersionPrefix` defaults.
4. Configure Git attributes/hooks: enforce `.editorconfig`, block Autodesk binaries via `.gitignore`, and optionally add pre-push validation (`dotnet format`, `dotnet build`). **Done** through `.gitattributes`, `.gitignore`, and `scripts/git-hooks/pre-push.ps1`.

## Phase 2 - Dependency & Library Cleanup (🔄)
1. Review `XmiSchema.Core` 0.6.0 release notes for API breaking changes; document adjustments needed in `builder/` and `classMapper/`. **Done** – notes live in `docs/XmiSchemaCore-0.6.0-review.md`, the Revit 2026 docs confirmed the AnalyticalMember/AnalyticalPanel APIs we rely on (`GetCurve`, `GetLoops`), and the follow-up actions (material/cross-section loops, segment population, storey de-duplication) are now implemented in code.
2. Update `RevitXmiExporter.csproj` to reference the new package version and run `dotnet restore`. **Done**; restore succeeds on Windows MSBuild after COM reference limitation workaround.
3. Refactor code to meet naming conventions: ensure properties use PascalCase, remove redundant Hungarian prefixes, and add analyzers (e.g., `Microsoft.CodeAnalysis.NetAnalyzers`) if gaps remain. **Done** – schema helpers now emit PascalCase identifiers, `_`-prefixed keys were removed from tests, and the analyzer package (with `AnalysisLevel=latest`) runs as part of the build.
4. Update or add unit tests under `test/` to cover library contract changes (e.g., serialization expectations, schema validation) and run `dotnet test`. **Pending** – blocked by linking Revit stubs/headless harness.

## Phase 3 - Installer & Distribution Pipeline (⏳)
1. Define the add-in bundle layout: `InstallerPayload/Applications/Revit/Addins/2025/RevitXmiExporter.addin` + `ApplicationFiles/` with compiled DLLs and config files.
2. Choose an installer technology (WiX v4 MSI, `Inno Setup`, or MSIX). Scaffold scripts under `installer/` that copy the publish output, write the `.addin` manifest with correct assembly paths, and register uninstall info.
3. Extend `.csproj` with a `PublishProfile` targeting `bin/Release/Publish` and ensure the pipeline produces Release builds with strong naming and file version info.
4. Create a `build_installer.ps1` (or Cake script) that performs: `dotnet publish`, copies payload into the installer project, runs the installer build, and outputs signed binaries into `dist/`.
5. Update documentation (AGENTS.md + README) with end-user instructions (download installer, run, restart Revit) and rollback guidance.

## Phase 4 - Automation, QA, and Release (⏳)
1. Add a GitHub Actions workflow (or Azure DevOps pipeline) that triggers on tags, executes formatting/build/test jobs on Windows runners, uploads build logs, and stores installer artifacts.
2. Introduce signing (Authenticode certificate) to avoid SmartScreen warnings; include TODOs if certificate procurement is pending.
3. Pilot the installer on clean Revit 2025 environments, validate automatic placement in `%APPDATA%/Autodesk/Revit/Addins/2025`, and smoke-test export workflows.
4. Update release checklist: tag commit, verify CI artifacts, publish GitHub Release with installer + checksum, and notify stakeholders of upgrade instructions.
