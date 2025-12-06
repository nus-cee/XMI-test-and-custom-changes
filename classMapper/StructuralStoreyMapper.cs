using Autodesk.Revit.DB;
using Betekk.RevitXmiExporter.ClassMapper.Base;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Manager;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal class StoreyMapper : StructuralBaseEntityMapper
    {
        public static XmiStorey Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
            {
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

                double storeyElevation = 0;
                if (element is Level level)
                {
                    storeyElevation = Converters.ConvertValueToMillimeter(level.Elevation);
                }

                XmiStorey existingStorey = manager
                    .GetEntitiesOfType<XmiStorey>(modelIndex)
                    .FirstOrDefault(s => string.Equals(s.NativeId, nativeId, StringComparison.OrdinalIgnoreCase));
                if (existingStorey != null)
                {
                    return existingStorey;
                }

                return manager.CreateStorey(
                    modelIndex,
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    description,
                    storeyElevation);
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[StoreyMapper] {ex}");
                throw;
            }
        }
    }
}
