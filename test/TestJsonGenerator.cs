using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Newtonsoft.Json;

namespace Betekk.RevitXmiExporter.test
{
    public static class TestJsonGenerator
    {
        private static class SchemaKeys
        {
            public const string ElementId = "ElementId";
            public const string Category = "Category";
            public const string Name = "Name";
            public const string Class = "Class";
            public const string IsAnalytical = "IsAnalytical";
            public const string AnalyzeAs = "AnalyzeAs";
            public const string StructuralRole = "StructuralRole";
            public const string FromInstance = "FromInstance";
            public const string Elevation = "Elevation";
        }

        public static string GenerateStructuredModelJson(Document doc)
        {
            var structured = new Dictionary<string, object>
            {
                { "StructuralModel", new List<Dictionary<string, string>>() },
                { "StructuralMaterial", new List<Dictionary<string, string>>() },
                { "StructuralCrossSection", new List<Dictionary<string, string>>() },
                { "StructuralStorey", new List<Dictionary<string, string>>() },
                { "StructuralPointConnection", new List<Dictionary<string, string>>() },
                { "StructuralCurveMember", new List<Dictionary<string, string>>() },
                { "StructuralSurfaceMember", new List<Dictionary<string, string>>() },
                { "StructuralUnit", GetUnits(doc) },
                { "StructuralPointSupport", new List<Dictionary<string, string>>() },
                { "StructuralLineSupport", new List<Dictionary<string, string>>() },
                { "StructuralAreaSupport", new List<Dictionary<string, string>>() },
                { "StructuralReinforcement", new List<Dictionary<string, string>>() }
            };

            var analyticalElements = new FilteredElementCollector(doc)
                .OfClass(typeof(AnalyticalElement));

            foreach (Element elem in analyticalElements)
            {
                var data = CollectElementData(elem, doc);
                string role = data.ContainsKey(SchemaKeys.StructuralRole) ? data[SchemaKeys.StructuralRole].ToLower() : "unknown";

                ((List<Dictionary<string, string>>)structured["StructuralModel"]).Add(data);

                if (role.Contains("beam") || role.Contains("column") || role.Contains("brace"))
                {
                    ((List<Dictionary<string, string>>)structured["StructuralCurveMember"]).Add(data);
                }
                else if (role.Contains("wall") || role.Contains("slab") || role.Contains("deck"))
                {
                    ((List<Dictionary<string, string>>)structured["StructuralSurfaceMember"]).Add(data);
                }
            }

            var materials = new FilteredElementCollector(doc).OfClass(typeof(Material));
            foreach (Material mat in materials)
            {
                var data = new Dictionary<string, string>
                {
                    [SchemaKeys.ElementId] = mat.Id.ToString(),
                    [SchemaKeys.Name] = mat.Name,
                    [SchemaKeys.Class] = mat.MaterialClass ?? "N/A"
                };
                ((List<Dictionary<string, string>>)structured["StructuralMaterial"]).Add(data);
            }

            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level));
            foreach (Level lvl in levels)
            {
                var data = new Dictionary<string, string>
                {
                    [SchemaKeys.ElementId] = lvl.Id.ToString(),
                    [SchemaKeys.Name] = lvl.Name,
                    [SchemaKeys.Elevation] = lvl.Elevation.ToString()
                };
                ((List<Dictionary<string, string>>)structured["StructuralStorey"]).Add(data);
            }

            var rebars = new FilteredElementCollector(doc).OfClass(typeof(Rebar));
            foreach (Rebar rebar in rebars)
            {
                var data = CollectElementData(rebar, doc);
                ((List<Dictionary<string, string>>)structured["StructuralReinforcement"]).Add(data);
            }

            var famInstances = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance));
            foreach (FamilyInstance inst in famInstances)
            {
                var type = doc.GetElement(inst.GetTypeId()) as Element;
                if (type != null)
                {
                    var data = CollectElementData(type, doc);
                    data[SchemaKeys.FromInstance] = inst.Id.ToString();
                    ((List<Dictionary<string, string>>)structured["StructuralCrossSection"]).Add(data);
                }
            }

            return JsonConvert.SerializeObject(structured, Formatting.Indented);
        }

        private static Dictionary<string, string> CollectElementData(Element element, Document doc)
        {
            var elementData = new Dictionary<string, string>
            {
                [SchemaKeys.ElementId] = element.Id.ToString(),
                [SchemaKeys.Category] = element.Category?.Name ?? "N/A",
                [SchemaKeys.Name] = element.Name,
                [SchemaKeys.Class] = element.GetType().Name
            };

            if (element is AnalyticalElement analytical)
            {
                elementData[SchemaKeys.IsAnalytical] = "true";
                elementData[SchemaKeys.AnalyzeAs] = analytical.AnalyzeAs.ToString();
                elementData[SchemaKeys.StructuralRole] = analytical.StructuralRole.ToString();
            }
            else
            {
                elementData[SchemaKeys.IsAnalytical] = "false";
                elementData[SchemaKeys.AnalyzeAs] = "N/A";
                elementData[SchemaKeys.StructuralRole] = "N/A";
            }

            foreach (Parameter param in element.Parameters)
            {
                if (param != null && param.HasValue && param.Definition != null)
                {
                    string paramName = "[Instance] " + param.Definition.Name;
                    if (!elementData.ContainsKey(paramName))
                    {
                        elementData[paramName] = GetParameterValue(param);
                    }
                }
            }

            Element type = doc.GetElement(element.GetTypeId());
            if (type != null)
            {
                foreach (Parameter param in type.Parameters)
                {
                    if (param != null && param.HasValue && param.Definition != null)
                    {
                        string paramName = "[Type] " + param.Definition.Name;
                        if (!elementData.ContainsKey(paramName))
                        {
                            elementData[paramName] = GetParameterValue(param);
                        }
                    }
                }
            }

            return elementData;
        }

        private static string GetParameterValue(Parameter param)
        {
            return param.StorageType switch
            {
                StorageType.String => param.AsString() ?? "",
                StorageType.Double => param.AsValueString() ?? "",
                StorageType.Integer => param.AsInteger().ToString(),
                StorageType.ElementId => param.AsElementId().Value.ToString(),
                _ => ""
            };
        }

        private static Dictionary<string, string> GetUnits(Document doc)
        {
            var unitInfo = new Dictionary<string, string>();
            Units units = doc.GetUnits();
            IList<ForgeTypeId> specTypeIds = UnitUtils.GetAllMeasurableSpecs();

            foreach (ForgeTypeId spec in specTypeIds)
            {
                try
                {
                    FormatOptions formatOptions = units.GetFormatOptions(spec);
                    ForgeTypeId unitTypeId = formatOptions.GetUnitTypeId();
                    string unitLabel = LabelUtils.GetLabelForUnit(unitTypeId);
                    string specLabel = LabelUtils.GetLabelForSpec(spec);
                    double accuracy = formatOptions.Accuracy;
                    string info = $"Unit: {unitLabel}, Accuracy: {accuracy}";
                    unitInfo[specLabel] = info;
                }
                catch (Exception ex)
                {
                    unitInfo[spec.TypeId] = $"Error: {ex.Message}";
                }
            }

            return unitInfo;
        }
    }
}
