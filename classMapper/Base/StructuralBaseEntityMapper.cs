using XmiCore;
using Autodesk.Revit.DB;
using SessionUuid;

namespace ClassMapper
{
    internal abstract class BaseMapper
    {
        // 静态缓存，每个 element 只生成一次 UUID
        private static readonly Dictionary<ElementId, string> ElementUuidMap = new();

        protected static (string sessionUuid, string name, string ifcGuid, string nativeId, string description) ExtractBasicProperties(Element element)
        {

            string sessionUuid = Guid.NewGuid().ToString();

            string name = element.Name;
            string ifcGuid = element.UniqueId;
            string nativeId = element.Id.ToString();
            string description = element.LookupParameter("Description")?.AsString() ?? "";

            return (sessionUuid, name, ifcGuid, nativeId, description);
        }
    }
}
