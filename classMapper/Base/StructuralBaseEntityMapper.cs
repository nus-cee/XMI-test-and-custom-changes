using XmiCore;
using Autodesk.Revit.DB;

namespace ClassMapper
{
    internal abstract class BaseMapper
    {
        protected static (string id, string name, string ifcGuid, string nativeId, string description) ExtractBasicProperties(Element element)
        {
            string id = $"node_{element.Id}";
            string name = element.Name;
            string ifcGuid = element.UniqueId;
            string nativeId = element.Id.ToString();
            string description = element.LookupParameter("Description")?.AsString() ?? "";

            return (id, name, ifcGuid, nativeId, description);
        }
    }
}
