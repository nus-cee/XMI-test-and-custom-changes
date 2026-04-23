using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Betekk.RevitXmiExporter.Utils;
using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Revit external command invoked by the Import XMI button. Prompts the user for an
    /// XMI JSON file and creates structural columns in the active document.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BetekkXmiImportCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the import by prompting for a source JSON file, delegating to
        /// <see cref="BetekkXmiImporter"/>, and handling success/failure notifications.
        /// </summary>
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
                    message = "Revit did not supply an active document for import.";
                    ShowErrorDialog("No active document", null);
                    return Result.Failed;
                }

                if (!TryPromptForImportPath(out string? importPath))
                {
                    ShowCancellationDialog();
                    return Result.Cancelled;
                }

                string json = File.ReadAllText(importPath, Encoding.UTF8);

                BetekkXmiImporter importer = new BetekkXmiImporter();
                int count = importer.Import(doc, json);

                ShowSuccessDialog(importPath, count);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[BetekkXmiImportCommand] {ex}");
                message = ex.Message;
                ShowErrorDialog("An exception occurred during import.", ex);
                return Result.Failed;
            }
        }

        private static bool TryPromptForImportPath(out string? importPath)
        {
            importPath = null;

            FileOpenDialog openDialog = new FileOpenDialog("JSON files (*.json)|*.json")
            {
                Title = "Select XMI JSON file to import"
            };

            ItemSelectionDialogResult result = openDialog.Show();
            if (result != ItemSelectionDialogResult.Confirmed)
            {
                return false;
            }

            ModelPath? modelPath = openDialog.GetSelectedModelPath();
            if (modelPath == null)
            {
                return false;
            }

            string? path = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            importPath = path;
            return true;
        }

        private static void ShowSuccessDialog(string importPath, int columnCount)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Import Summary:");
            sb.AppendLine($"  • Elements created: {columnCount}");
            sb.AppendLine();
            sb.AppendLine($"Source file:");
            sb.Append(importPath);

            RevitTaskDialog dialog = new RevitTaskDialog("Import complete")
            {
                MainInstruction = "Structural elements were imported successfully.",
                MainContent = sb.ToString(),
                CommonButtons = TaskDialogCommonButtons.Close
            };

            dialog.Show();
        }

        private static void ShowCancellationDialog()
        {
            RevitTaskDialog dialog = new RevitTaskDialog("Import canceled")
            {
                MainInstruction = "No file was imported.",
                MainContent = "Select a JSON file containing structural element definitions.",
                CommonButtons = TaskDialogCommonButtons.Close
            };

            dialog.Show();
        }

        private static void ShowErrorDialog(string header, Exception? exception)
        {
            string logPath = ModelInfoBuilder.GetErrorLogPath();

            RevitTaskDialog dialog = new RevitTaskDialog("Import error")
            {
                MainInstruction = string.IsNullOrWhiteSpace(header) ? "The import failed." : header,
                MainContent = $"Details were written to:{Environment.NewLine}{logPath}",
                ExpandedContent = exception?.ToString(),
                CommonButtons = TaskDialogCommonButtons.Close
            };

            dialog.Show();
        }
    }
}
