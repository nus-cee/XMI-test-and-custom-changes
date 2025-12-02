using Autodesk.Revit.DB;
using Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Manager;

namespace ClassMapper
{
    internal class StructuralStoreyMapper : BaseMapper
    {
        public static XmiStructuralStorey Map(IXmiManager manager, int modelIndex, Element element)
        {            try
            {
            var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

            double storeyElevation = 0;
            if (element is Level level)
            {
                storeyElevation = Converters.ConvertValueToMillimeter(level.Elevation);
            }

            return manager.CreateStructuralStorey(
                modelIndex,
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                storeyElevation,
                1f,                        // 固定质量
                null,                      // 默认水平反应 X
                null,                      // 默认水平反应 Y
                null                       // 默认垂直反应
            );
        }
        catch (Exception ex)
        {
            Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile($"[StructuralStoreyMapper] Error: {ex}");
            throw;
        }
        }
    }
}
