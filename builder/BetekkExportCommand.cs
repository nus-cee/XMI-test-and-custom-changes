using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Betekk.RevitXmiExporter.Builder;
using Betekk.RevitXmiExporter.Utils;
using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace Betekk.RevitXmiExporter
{
    /// <summary>
    /// Revit external command invoked by the ExportJson button. Collects the output location,
    /// runs the export pipeline, and surfaces user feedback/error logging.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BetekkExportCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the export by prompting for a destination, delegating to <see cref="BetekkJsonExporter"/>,
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
            string lastExportPath = null;
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Title = "Choose an export location",
                    FileName = "StructuredAnalyticalModel.json",
                    DefaultExt = "json",
                    Filter = "JSON files (*.json)|*.json"
                };

                if (saveDialog.ShowDialog() != DialogResult.OK)
                {
                    RevitTaskDialog.Show("Export canceled", "No path selected. The export has been canceled.");
                    return Result.Cancelled;
                }

                lastExportPath = saveDialog.FileName;
                ModelInfoBuilder.SetLogDirectory(Path.GetDirectoryName(lastExportPath));

                string basePath = Path.Combine(
                    Path.GetDirectoryName(saveDialog.FileName) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(saveDialog.FileName) ?? "StructuredAnalyticalModel");

                BetekkJsonExporter exporter = new BetekkJsonExporter();
                string exportJson = exporter.Export(doc);
                string exportPath = basePath + "_xmi_export.json";
                File.WriteAllText(exportPath, exportJson, Encoding.UTF8);

                RevitTaskDialog dialog = new RevitTaskDialog("Export complete")
                {
                    MainInstruction = "The structural model was exported successfully.",
                    MainContent = exportPath
                };
                dialog.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[BetekkExportCommand] {ex}");
                RevitTaskDialog.Show("Export error", "An exception occurred during export. See error_log.txt for details.");
                return Result.Failed;
            }
        }
    }
}
