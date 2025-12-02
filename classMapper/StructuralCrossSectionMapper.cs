using Autodesk.Revit.DB;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Utils;
using System.Linq;
using Utils;

namespace ClassMapper
{
    internal class StructuralCrossSectionMapper : BaseMapper
    {
        public static XmiStructuralCrossSection Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
            {
                // 1️⃣ 基础属性
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

                // 2️⃣ 材料处理（委托 MaterialMapper）
                XmiStructuralMaterial material = null;
                if (element is ElementType typeElement)
                {
                    var matIds = typeElement.GetMaterialIds(false);
                    if (matIds.Count > 0)
                    {
                        var matElement = element.Document.GetElement(matIds.First()) as Material;

                        // 🛡️ 新增前置检查：ID 和 Name 是否合法
                        var matName = matElement?.Name;
                        var matId = matElement?.Id?.ToString();

                        if (!string.IsNullOrWhiteSpace(matName) && !string.IsNullOrWhiteSpace(matId))
                        {
                            material = StructuralMaterialMapper.Map(manager, modelIndex, matElement);
                        }
                        else
                        {
                            Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile(
                                $"[StructuralCrossSectionMapper] Skipped invalid material: ID={matId}, Name={matName}");
                        }
                    }
                }


                //if (material == null)
                //{
                //    Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile(
                //        $"[StructuralCrossSectionMapper] Warning: Material is null. Creating default for element ID={element.Id}, Name={element.Name}");

                //    material = manager.CreateStructuralMaterial(
                //        modelIndex,
                //        "MATERIAL-PLACEHOLDER",
                //        "Default_Material",
                //        "",
                //        "MATERIAL-PLACEHOLDER",
                //        "",
                //        XmiStructuralMaterialTypeEnum.Unknown,
                //        0, 0,
                //        string.Empty,
                //        string.Empty,
                //        string.Empty,
                //        0
                //    );
                //}

                // 3️⃣ 形状
                var shapeName = element.Category?.Name ?? element.Name;
                var shapeEnum = ExtensionEnumHelper.FromEnumValue<XmiShapeEnum>(shapeName) ?? XmiShapeEnum.Unknown;

                // 4️⃣ 参数：宽高
                double width = 0, height = 0;
                if (element.LookupParameter("b") is Parameter bParam && bParam.HasValue)
                    width = Converters.ConvertValueToMillimeter(bParam.AsDouble());
                if (element.LookupParameter("h") is Parameter hParam && hParam.HasValue)
                    height = Converters.ConvertValueToMillimeter(hParam.AsDouble());

                string[] parameters = (width > 0 || height > 0)
                    ? new[] { width.ToString("F2"), height.ToString("F2") }
                    : [];

                // 5️⃣ 面积
                double area = 0;
                if (element is ElementType areaType)
                {
                    var matIds = areaType.GetMaterialIds(false);
                    if (matIds.Count > 0)
                    {
                        try
                        {
                            double areaFt2 = areaType.GetMaterialArea(matIds.First(), false);
                            area = Converters.SquareFeetToSquareMillimeter(areaFt2);
                        }
                        catch
                        {
                            if (element.LookupParameter("Area") is Parameter areaParam && areaParam.HasValue)
                                area = Converters.ConvertValueToMillimeter(areaParam.AsDouble());
                        }
                    }
                }

                // 6️⃣ 截面参数（惯性矩等）
                double GetParam(string param) =>
                    element.LookupParameter(param)?.HasValue == true
                        ? Converters.ConvertValueToMillimeter(element.LookupParameter(param).AsDouble())
                        : 0;

                double Ix = GetParam("Ix");
                double Iy = GetParam("Iy");
                double rx = GetParam("rx");
                double ry = GetParam("ry");
                double Sx = GetParam("Sx");
                double Sy = GetParam("Sy");
                double Zx = GetParam("Zx");
                double Zy = GetParam("Zy");
                double J = GetParam("J");

                // 7️⃣ 日志
                Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile(
                    $"[StructuralCrossSectionMapper] Creating: {name}, Shape={shapeEnum}, Area={area}, Mat={material?.Name}");


                // ✅ 在这里强力检查 material 是否可用
                if (material == null || string.IsNullOrWhiteSpace(material.ID))
                {
                    Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile(
                        $"[StructuralCrossSectionMapper] Invalid material — forcing to null before CreateStructuralCrossSection. Element ID={element.Id}, Name={element.Name}, MatID={material?.ID}");
                    material = null;
                }


                // 8️⃣ 创建实体
                return manager.CreateStructuralCrossSection(
                    modelIndex,
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    description,
                    material,
                    shapeEnum,
                    parameters,
                    area,
                    Ix,
                    Iy,
                    rx,
                    ry,
                    Sx,
                    Sy,
                    Zx,
                    Zy,
                    J
                );
            }
            catch (System.Exception ex)
            {
                string info = $"Element ID={element?.Id}, Name={element?.Name}";
                Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile(
                    $"[StructuralCrossSectionMapper] Error: {ex.Message}\n{info}\n{ex}");
                throw;
            }
        }
    }
}
