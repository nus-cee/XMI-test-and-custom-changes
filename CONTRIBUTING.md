# Contributing to Betekk.RevitXmiExporter

Thanks for helping improve the Revit exporter. Follow the workflow below to keep builds reproducible and installer artifacts traceable.

## Branching Model
- `main`: production-ready code that aligns with the latest installer tag.
- `develop`: integration branch for the next release; feature branches merge here first.
- `feature/<ticket>-<slug>`: scoped work items (e.g., `feature/123-add-installer`).
- `hotfix/<ticket>-<slug>`: urgent fixes that cherry-pick onto `main` and then back into `develop`.

Keep branches short-lived. If a branch drifts more than a few days, rebase on `develop` before opening a PR: `git fetch origin && git rebase origin/develop`.

## Issue & PR Workflow
1. Open a GitHub issue describing the change (problem statement, acceptance criteria, Revit version tested).
2. Reference the issue when creating your branch and when committing: `git commit -m "123:Add installer manifest validation"`.
3. Push to your fork and open a PR targeting `develop`. In the PR template:
   - Link the issue via `Closes #123`.
   - Attach screenshots/logs for ribbon UI or installer changes.
   - List manual/automated tests executed (include Revit build number).
4. Request at least one reviewer from the Betekk Revit team. No self-merge unless CI is green and at least one approval is present.

## Commit Hygiene
- Keep commits atomic (single concern) and under ~200 lines when possible.
- Prefer imperative subject lines (`Add installer manifest schema validation`).
- Run `dotnet format` (when available) and `msbuild RevitXmiExporter.sln /p:Configuration=Release` before pushing.

## Repo Hygiene & Hooks
- `.editorconfig` and `.gitattributes` keep indentation, newline, and diff behavior consistent. Do not override these locally; add scoped overrides in feature branches if needed.
- Autodesk/Revit binaries (`RevitAPI*.dll`, `.addin`, `.rvt`, `.rfa`) are excluded in `.gitignore`. Leave them outside the repo to avoid legal/size issues.
- A reusable pre-push hook lives under `.githooks/pre-push` and proxies to `scripts/git-hooks/pre-push.ps1`. Enable it once per clone:
  ```bash
  git config core.hooksPath .githooks
  ```
  The PowerShell script runs `dotnet format --verify-no-changes` and `msbuild RevitXmiExporter.sln /p:Configuration=Release`. Run pushes from a Visual Studio Developer PowerShell (or set `MSBUILD_EXE_PATH`) so the hook can find `MSBuild.exe`.
- If you truly need to skip the hook (e.g., hotfix while offline), provide rationale in your PR and run the same commands manually before requesting review.

## Review Checklist
- ✅ Namespaces start with `Betekk.` and follow the existing folder layout.
- ✅ Revit API references honor `Directory.Build.props` overrides (no hard-coded paths).
- ✅ Documentation updated when commands or installation steps change (`README.md`, `TESTING.md`, installers).
- ✅ Installer payloads do not include user-specific files (keep `%APPDATA%` deploy instructions in docs only).

Following this guide keeps feature flow predictable and ensures every binary in `dist/` can be reproduced from tagged commits.
