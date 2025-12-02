using Autodesk.Revit.DB;

namespace Betekk.RevitXmiExporter.Builder
{
    public class JsonExporter
    {
        public string Export(Document doc)
        {
            XmiBuilder builder = new XmiBuilder();
            builder.BuildModel(doc);
            return builder.GetJson();
        }
    }
}
