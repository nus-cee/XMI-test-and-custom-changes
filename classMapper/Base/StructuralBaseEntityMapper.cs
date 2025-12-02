using System;
using Autodesk.Revit.DB;

namespace Betekk.RevitXmiExporter.classMapper.Base
{
    internal abstract class BaseMapper
    {
        protected static (string Id, string Name, string IfcGuid, string NativeId, string Description) ExtractBasicProperties(Element element)
        {
            string id = Guid.NewGuid().ToString();

            string name = string.IsNullOrWhiteSpace(element.Name) ? id : element.Name;
            string ifcGuid = element.UniqueId;
            string nativeId = element.Id.ToString();
            string description = element.LookupParameter("Description")?.AsString() ?? string.Empty;

            return (id, name, ifcGuid, nativeId, description);
        }
    }
}
