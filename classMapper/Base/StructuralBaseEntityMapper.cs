using System;
using Autodesk.Revit.DB;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal abstract class BaseMapper
    {
        protected static (string sessionUuid, string name, string ifcGuid, string nativeId, string description) ExtractBasicProperties(Element element)
        {
            string sessionUuid = Guid.NewGuid().ToString();

            string name = string.IsNullOrWhiteSpace(element.Name) ? sessionUuid : element.Name;
            string ifcGuid = element.UniqueId;
            string nativeId = element.Id.ToString();
            string description = element.LookupParameter("Description")?.AsString() ?? string.Empty;

            return (sessionUuid, name, ifcGuid, nativeId, description);
        }
    }
}
