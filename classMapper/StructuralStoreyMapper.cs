using System;
using System.Linq;
using Autodesk.Revit.DB;
using Betekk.RevitXmiExporter.ClassMapper.Base;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Manager;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal class StructuralStoreyMapper : StructuralBaseEntityMapper
    {
        public static XmiStructuralStorey Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
            {
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

                double storeyElevation = 0;
                if (element is Level level)
                {
                    storeyElevation = Converters.ConvertValueToMillimeter(level.Elevation);
                }

                XmiStructuralStorey existingStorey = manager
                    .GetEntitiesOfType<XmiStructuralStorey>(modelIndex)
                    .FirstOrDefault(s => string.Equals(s.NativeId, nativeId, StringComparison.OrdinalIgnoreCase));
                if (existingStorey != null)
                {
                    return existingStorey;
                }

                return manager.CreateStructuralStorey(
                    modelIndex,
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    description,
                    storeyElevation,
                    1f,
                    null,
                    null,
                    null);
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[StructuralStoreyMapper] {ex}");
                throw;
            }
        }
    }
}
