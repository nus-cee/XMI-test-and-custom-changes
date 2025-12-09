using System.Diagnostics.CodeAnalysis;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Betekk.RevitXmiExporter.Utils;
using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Revit external command invoked by the ExportXmi button. Collects the output location,
    /// runs the export pipeline, and surfaces user feedback/error logging.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BetekkRevitModelToXmiExportCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the export by prompting for a destination, delegating to <see cref="BetekkRevitToXmiModelManager"/>,
        /// and handling success/failure notifications.
        /// </summary>
        /// <param name="commandData">Revit provided context (documents, application, selection).</param>
        /// <param name="message">Populated when returning <see cref="Result.Failed"/>.</param>
        /// <param name="elements">Unused; provided for completeness per Revit API.</param>
        /// <returns><see cref="Result.Succeeded"/> when the JSON file is written.</returns>
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument? uidoc = commandData.Application.ActiveUIDocument;
                Document? doc = uidoc?.Document;
                if (doc == null)
                {
                    message = "Revit did not supply an active document to export.";
                    ShowErrorDialog("No active document", null);
                    return Result.Failed;
                }

                if (!TryPromptForExportPath(doc, out string? exportPath))
                {
                    ShowCancellationDialog();
                    return Result.Cancelled;
                }

                string? logDirectory = Path.GetDirectoryName(exportPath);
                if (!string.IsNullOrWhiteSpace(logDirectory))
                {
                    ModelInfoBuilder.SetLogDirectory(logDirectory);
                }

                BetekkRevitToXmiModelManager exporter = new BetekkRevitToXmiModelManager();
                ExportResult result = exporter.Export(doc);

                SaveExport(exportPath, result.Json);
                ShowSuccessDialog(exportPath, result.Statistics);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[BetekkExportCommand] {ex}");
                message = ex.Message;
                ShowErrorDialog("An exception occurred during export.", ex);
                return Result.Failed;
            }
        }

        private static bool TryPromptForExportPath(Document doc, [NotNullWhen(true)] out string? exportPath)
        {
            exportPath = null;
            string initialDirectory = GetInitialDirectory(doc);
            string defaultFileName = BuildDefaultFileName(doc);

            FileSaveDialog saveDialog = new FileSaveDialog("JSON files (*.json)|*.json")
            {
                Title = "Export structural model to JSON",
                InitialFileName = Path.Combine(initialDirectory, defaultFileName)
            };

            ItemSelectionDialogResult result = saveDialog.Show();
            if (result != ItemSelectionDialogResult.Confirmed)
            {
                return false;
            }

            ModelPath? modelPath = saveDialog.GetSelectedModelPath();
            if (modelPath == null)
            {
                return false;
            }

            string? userVisiblePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
            if (string.IsNullOrWhiteSpace(userVisiblePath))
            {
                return false;
            }

            exportPath = EnsureJsonExtension(userVisiblePath);
            return true;
        }

        private static string BuildDefaultFileName(Document doc)
        {
            string? docTitle = doc?.Title;
            string sanitizedName = string.IsNullOrWhiteSpace(docTitle) ? "xmi_export" : docTitle.Trim();
            return $"{sanitizedName}.json";
        }

        private static string GetInitialDirectory(Document doc)
        {
            string? docPath = doc?.PathName;
            if (!string.IsNullOrWhiteSpace(docPath))
            {
                string? directory = Path.GetDirectoryName(docPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
            }

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrWhiteSpace(documents) ? Directory.GetCurrentDirectory() : documents;
        }

        private static string EnsureJsonExtension(string path)
        {
            return Path.ChangeExtension(path, ".json");
        }

        private static void SaveExport(string exportPath, string payload)
        {
            string? directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(exportPath, payload, Encoding.UTF8);
        }

        private static void ShowSuccessDialog(string exportPath, ExportStatistics stats)
        {
            StringBuilder summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine("Export Summary:");
            summaryBuilder.AppendLine($"  • Storeys: {stats.StoreyCount}");
            summaryBuilder.AppendLine($"  • Beams: {stats.BeamCount}");
            summaryBuilder.AppendLine($"  • Columns: {stats.ColumnCount}");
            summaryBuilder.AppendLine($"  • Analytical Members: {stats.AnalyticalMemberCount}");
            summaryBuilder.AppendLine($"  • Materials: {stats.MaterialCount}");
            summaryBuilder.AppendLine($"  • Cross-Sections: {stats.CrossSectionCount}");
            summaryBuilder.AppendLine($"  • 3D Points: {stats.PointCount}");
            summaryBuilder.AppendLine($"  • Structural Connections: {stats.ConnectionCount}");
            summaryBuilder.AppendLine();
            summaryBuilder.AppendLine($"File saved to:");
            summaryBuilder.Append(exportPath);

            RevitTaskDialog dialog = new RevitTaskDialog("Export complete")
            {
                MainInstruction = "The structural model was exported successfully.",
                MainContent = summaryBuilder.ToString(),
                FooterText = "Press Ctrl+C to copy the file path from this dialog.",
                CommonButtons = TaskDialogCommonButtons.Close
            };

            dialog.Show();
        }

        private static void ShowCancellationDialog()
        {
            RevitTaskDialog dialog = new RevitTaskDialog("Export canceled")
            {
                MainInstruction = "No file was exported.",
                MainContent = "Choose a destination to generate a JSON file.",
                CommonButtons = TaskDialogCommonButtons.Close
            };

            dialog.Show();
        }

        private static void ShowErrorDialog(string header, Exception? exception)
        {
            string logPath = ModelInfoBuilder.GetErrorLogPath();

            RevitTaskDialog dialog = new RevitTaskDialog("Export error")
            {
                MainInstruction = string.IsNullOrWhiteSpace(header) ? "The export failed." : header,
                MainContent = $"Details were written to:{Environment.NewLine}{logPath}{Environment.NewLine}{Environment.NewLine}Press Ctrl+C to copy this message.",
                ExpandedContent = exception?.ToString(),
                FooterText = "Include error_log.txt when reporting the issue.",
                CommonButtons = TaskDialogCommonButtons.Close
            };

            dialog.Show();
        }
    }
}