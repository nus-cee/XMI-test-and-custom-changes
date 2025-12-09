using Autodesk.Revit.DB;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Helper facade that instantiates <see cref="BetekkXmiBuilder"/>, builds the model, and returns
    /// the serialized JSON string and export statistics.
    /// </summary>
    public class BetekkRevitToXmiModelManager
    {
        /// <summary>
        /// Runs the XMI builder pipeline against the provided Revit document and returns the
        /// export result containing JSON payload and statistics.
        /// </summary>
        /// <param name="doc">Active Revit document to inspect.</param>
        /// <returns>Export result containing JSON and statistics.</returns>
        public ExportResult Export(Document doc)
        {
            BetekkXmiBuilder builder = new BetekkXmiBuilder();
            builder.BuildModel(doc);

            return new ExportResult
            {
                Json = builder.GetJson(),
                Statistics = builder.GetExportStatistics()
            };
        }
    }

    /// <summary>
    /// Contains the result of an export operation.
    /// </summary>
    public class ExportResult
    {
        public string Json { get; set; }
        public ExportStatistics Statistics { get; set; }
    }
}