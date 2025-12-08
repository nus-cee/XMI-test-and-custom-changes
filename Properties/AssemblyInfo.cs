using System.Reflection;
using System.Runtime.Versioning;

[assembly: AssemblyCompany("Betekk")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: AssemblyDescription("Autodesk Revit add-in that exports structural data to XMI schema JSON.")]
[assembly: AssemblyProduct("Betekk RevitXmiExporter")]
[assembly: AssemblyTitle("RevitXmiExporter")]
[assembly: AssemblyVersion("0.2.0")]
[assembly: AssemblyFileVersion("0.2.0")]
[assembly: AssemblyInformationalVersion("0.2.0")]
[assembly: TargetFramework(".NETCoreApp,Version=v8.0", FrameworkDisplayName = ".NET 8.0")]
