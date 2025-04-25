using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Utils;
using XmiCore;

namespace ClassMapper
{
    internal class StructuralStoreyMapper : BaseMapper
    {
        public static XmiStructuralStorey Map(Element element)
        {
            var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

            double? storeyElevation = null;

            if (element is Level level)
            {
                storeyElevation = (float)level.Elevation;
            }

            double? storeyMass = 1f; // 固定质量，实际可根据需要修改
            string? storeyHorizontalReactionX = null;
            string? storeyHorizontalReactionY = null;
            string? storeyVerticalReaction = null;
        

        return new XmiStructuralStorey(
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                storeyElevation ?? 0f,
                storeyMass ?? 0f,
                storeyHorizontalReactionX,
                storeyHorizontalReactionY,
                storeyVerticalReaction
            );
        }
    }
}
