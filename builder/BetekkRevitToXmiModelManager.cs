using Autodesk.Revit.DB;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Helper facade that instantiates <see cref="BetekkXmiBuilder"/>, builds the model, and returns
    /// the serialized JSON string.
    /// </summary>
    public class BetekkRevitToXmiModelManager
    {
        /// <summary>
        /// Runs the XMI builder pipeline against the provided Revit document and returns the
        /// JSON payload produced by <see cref="BetekkXmiBuilder"/>.
        /// </summary>
        /// <param name="doc">Active Revit document to inspect.</param>
        /// <returns>Serialized JSON that follows the XMI schema.</returns>
        public string Export(Document doc)
        {
            BetekkXmiBuilder builder = new BetekkXmiBuilder();
            builder.BuildModel(doc);
            return builder.GetJson();
        }
    }
}
