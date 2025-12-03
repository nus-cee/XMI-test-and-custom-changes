using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Betekk.RevitXmiExporter.ClassMapper.Base;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Utils;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal class StructuralCrossSectionMapper : StructuralBaseEntityMapper
    {
        public static XmiStructuralCrossSection Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
            {
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

                XmiStructuralMaterial material = null;
                if (element is ElementType typeElement)
                {
                    ICollection<ElementId> matIds = typeElement.GetMaterialIds(false);
                    if (matIds.Count > 0)
                    {
                        Material matElement = element.Document.GetElement(matIds.First()) as Material;
                        string matName = matElement?.Name;
                        string matId = matElement?.Id?.ToString();

                        if (!string.IsNullOrWhiteSpace(matName) && !string.IsNullOrWhiteSpace(matId))
                        {
                            material = StructuralMaterialMapper.Map(manager, modelIndex, matElement);
                        }
                        else
                        {
                            ModelInfoBuilder.WriteErrorLogToFile(
                                $"[StructuralCrossSectionMapper] Skipped invalid material: ID={matId}, Name={matName}");
                        }
                    }
                }

                string shapeName = element.Category?.Name ?? element.Name;
                XmiShapeEnum shapeEnum = ExtensionEnumHelper.FromEnumValue<XmiShapeEnum>(shapeName) ?? XmiShapeEnum.Unknown;

                double width = 0;
                double height = 0;
                if (element.LookupParameter("b") is Parameter bParam && bParam.HasValue)
                    width = Converters.ConvertValueToMillimeter(bParam.AsDouble());
                if (element.LookupParameter("h") is Parameter hParam && hParam.HasValue)
                    height = Converters.ConvertValueToMillimeter(hParam.AsDouble());

                string[] parameters = (width > 0 || height > 0)
                    ? new[] { width.ToString("F2"), height.ToString("F2") }
                    : Array.Empty<string>();

                double area = 0;
                if (element is ElementType areaType)
                {
                    ICollection<ElementId> areaMatIds = areaType.GetMaterialIds(false);
                    if (areaMatIds.Count > 0)
                    {
                        try
                        {
                            double areaFt2 = areaType.GetMaterialArea(areaMatIds.First(), false);
                            area = Converters.SquareFeetToSquareMillimeter(areaFt2);
                        }
                        catch
                        {
                            if (element.LookupParameter("Area") is Parameter areaParam && areaParam.HasValue)
                                area = Converters.ConvertValueToMillimeter(areaParam.AsDouble());
                        }
                    }
                }

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

                if (material == null || string.IsNullOrWhiteSpace(material.Id))
                {
                    ModelInfoBuilder.WriteErrorLogToFile(
                        $"[StructuralCrossSectionMapper] Invalid material. Element ID={element.Id}, Name={element.Name}, MatID={material?.Id}");
                    material = null;
                }

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
            catch (Exception ex)
            {
                string info = $"Element ID={element?.Id}, Name={element?.Name}";
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[StructuralCrossSectionMapper] Error: {ex.Message}\n{info}\n{ex}");
                throw;
            }
        }
    }
}
