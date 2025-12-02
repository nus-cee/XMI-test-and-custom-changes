# Repository Guidelines

## Project Structure & Module Organization
The solution file `RevitXmiExporter.sln` loads the single project `RevitXmiExporter.csproj`, which targets `net8.0` and references Revit 2025 assemblies plus `XmiSchema.Core`. Entry points in `App.cs` call into the `builder/` pipeline: `BetekkXmiBuilder` orchestrates Revit collectors, `BetekkJsonExporter` serializes, and `BetekkExportCommand` wires everything into the Revit external command. Domain mapping logic is separated in `classMapper/`, while reusable helpers (naming, geometry, Revit lookups) live in `utils/`. The `test/` folder hosts scaffolding (e.g., `EntityTest/TestStorey.cs`) for constructing dummy entities and JSON snapshots-reuse these when extending automation.

## Build, Test, and Development Commands
- `dotnet restore RevitXmiExporter.csproj` - pull all NuGet and COM interop dependencies before building.
- `dotnet build RevitXmiExporter.sln -c Release` - compile the plugin for x64; Release matches the Revit deployment expectation.
- `dotnet publish RevitXmiExporter.csproj -c Release -o bin/Plugin` - produce a self-contained drop that can be copied into `%APPDATA%/Autodesk/Revit/Addins/2025`.
- `dotnet test RevitXmiExporter.sln` - runs any future test projects wired into the solution; see below for current expectations.

## Coding Style & Naming Conventions
Formatting is enforced via the repository `.editorconfig`: 4-space indentation, CRLF endings, grouped `using` statements, and no `var` unless type names are redundant. Follow .NET naming defaults (PascalCase for types/members, interfaces prefixed with `I`). Prefer object/collection initializers, explicit types, and wrap braces on new lines. Run `dotnet format` before opening a PR to align with analyzer preferences (qualification rules, null checks, readonly fields).

## Testing Guidelines
Automated tests are still being bootstrapped; until a dedicated test project is added, validate JSON output locally using the data builders in `test/` and Revit sample models. Name new tests after the component under test (`StructuralStoreyTests.BuildsExpectedStoryJson`). Once you add an xUnit or NUnit project, keep it under `test/` and update the solution so `dotnet test` executes it in CI. Document any manual Revit steps (models used, parameters toggled) inside `/test/README.md` when you add new cases.

## Commit & Pull Request Guidelines
Git history mixes imperative statements and Conventional Commits (e.g., `fix: point3D and pointConnection`). Prefer the latter: `<type>: <scope> <summary>` with a short, action-focused summary. Each PR should describe: (1) motivation or linked issue, (2) user-visible Revit changes, (3) test evidence (`dotnet build`, manual Revit screenshots, JSON diffs). Request a reviewer familiar with Revit API changes when touching `builder/` or `classMapper/`.

## Security & Configuration Tips
Revit COM references point to `..\Revit\Revit 2025\`; keep that folder outside the repo and never commit Autodesk binaries. Store environment-specific paths (e.g., schema export destinations) in Revit add-in manifest files, not in code. When logging, drop identifiers that could expose model IP-prefer hashes or GUID truncation. Always verify the JSON payload with `XmiManager` before sharing outside the org.
