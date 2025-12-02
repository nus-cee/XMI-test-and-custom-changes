# Local Build & Publish

1. **Configure the Revit install path**  
   - Defaults to `C:\Program Files\Autodesk\Revit 2026` (Windows) or `/mnt/c/Program Files/Autodesk/Revit 2026` (WSL) via `Directory.Build.props`.  
   - Override globally (`setx REVIT_INSTALL_DIR "D:\Autodesk\Revit 2026"`) or per command (`-p:RevitInstallDir="D:\Autodesk\Revit 2026"`).

2. **Restore dependencies**  
   - Windows: `dotnet restore RevitXmiExporter.sln` (optional; see MSBuild note below)  
   - WSL/Linux: `dotnet restore RevitXmiExporter.sln /p:EnableWindowsTargeting=true`

3. **Build the solution (requires MSBuild on Windows)**  
   - Open “Developer Command Prompt for VS 2022” (loads the .NET Framework MSBuild at `%ProgramFiles(x86)%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe`, adjust edition as needed).  
   - Run `"%ProgramFiles(x86)%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" RevitXmiExporter.sln /p:Configuration=Release /p:RevitInstallDir="..."`.  
   - WSL/Linux: `dotnet build RevitXmiExporter.sln -c Release /p:EnableWindowsTargeting=true`

4. **Publish deployable bits**  
   - Windows: `"%ProgramFiles(x86)%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" RevitXmiExporter.csproj /t:Publish /p:Configuration=Release /p:PublishDir=dist\Plugin\ /p:RevitInstallDir="..."`  
   - WSL/Linux:  
     ```
     dotnet publish RevitXmiExporter.csproj -c Release -o dist/Plugin /p:EnableWindowsTargeting=true
     ```

5. **Deploy for manual testing**  
   Copy `dist/Plugin` into `%APPDATA%\Autodesk\Revit\Addins\2026\RevitXmiExporter` and ensure the `.addin` manifest references `Betekk.RevitXmiExporter.ExportCommand`.

# Headless Test via RevitCoreConsole

1. Build/publish as above.  
2. Stage the add-in under `%APPDATA%\Autodesk\Revit\Addins\2026\Betekk.RevitXmiExporter\`.  
3. Set an environment variable consumed by the command (e.g., `set BETEKK_CLI_EXPORT_PATH=C:\temp\sample.json`).  
4. Run CoreConsole against a test model:
   ```powershell
   & "C:\Program Files\Autodesk\Revit 2026\RevitCoreConsole.exe" `
     /i "C:\Models\Sample.rvt" `
     /al "%APPDATA%\Autodesk\Revit\Addins\2026\Betekk.RevitXmiExporter\Betekk.RevitXmiExporter.dll"
   ```
5. Inspect the generated JSON (and log files) to verify expected output.
