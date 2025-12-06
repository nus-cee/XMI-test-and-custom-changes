
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.ClassMapper;

using XmiSchema.Core.Manager;
using XmiSchema.Core.Models;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Coordinates the extraction of structural data from a Revit document and writes it into
    /// an <see cref="IXmiManager"/> instance for JSON serialization.
    /// </summary>
    public class BetekkXmiBuilder
    {
        private readonly IXmiManager _manager;
        private const int ModelIndex = 0;

        /// <summary>
        /// Initializes the XMI manager with a single model slot used for export.
        /// </summary>
        public BetekkXmiBuilder()
        {
            _manager = new XmiManager();
            _manager.Models = new List<XmiModel> { new XmiModel() };
        }

        /// <summary>
        /// Executes each structural extraction loop in a fixed order so downstream mappers can
        /// rely on data created earlier in the pipeline.
        /// </summary>
        /// <param name="doc">Document to traverse.</param>
        public void BuildModel(Document doc)
        {
            (HashSet<ElementId> usedMaterialIds, HashSet<ElementId> usedTypeIds) usage = CollectUsedElementData(doc);

            MaterialLooper(doc, usage.usedMaterialIds);
            CrossSectionLooper(doc, usage.usedTypeIds);
            StructuralPointConnectionLooper(doc);
            StoreyLooper(doc);
            StructuralCurveMemberLooper(doc);
            StructuralSurfaceMemberLooper(doc);
        }

        /// <summary>
        /// Serializes the built model to JSON using the backing XMI manager.
        /// </summary>
        /// <returns>JSON string representing the structural model.</returns>
        public string GetJson()
        {
            return _manager.BuildJson(ModelIndex);
        }

        /// <summary>
        /// Iterates analytical node elements and registers structural point connections.
        /// </summary>
        /// <param name="doc">Document providing analytical nodes.</param>
        public void StructuralPointConnectionLooper(Document doc)
        {
            IEnumerable<Element> nodes = new FilteredElementCollector(doc)
                .OfClass(typeof(ReferencePoint))
                .OfCategory(BuiltInCategory.OST_AnalyticalNodes)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element pointConnection in nodes)
            {
                StructuralPointConnectionMapper.Map(_manager, ModelIndex, pointConnection);
            }
        }

        /// <summary>
        /// Collects level elements and maps them into structural storeys.
        /// </summary>
        /// <param name="doc">Document providing level data.</param>
        public void StoreyLooper(Document doc)
        {
            IEnumerable<Element> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element level in levels)
            {
                StoreyMapper.Map(_manager, ModelIndex, level);
            }
        }

        /// <summary>
        /// Registers all Revit materials with the XMI manager.
        /// </summary>
        /// <param name="doc">Document providing materials.</param>
        /// <param name="allowedMaterialIds">Optional filter restricting exports to materials referenced by placed elements.</param>
        public void MaterialLooper(Document doc, ISet<ElementId>? allowedMaterialIds = null)
        {
            IEnumerable<Element> materials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .OfCategory(BuiltInCategory.OST_Materials)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Material material in materials)
            {
                if (allowedMaterialIds != null && !allowedMaterialIds.Contains(material.Id))
                {
                    continue;
                }

                MaterialMapper.Map(_manager, ModelIndex, material);
            }
        }

        /// <summary>
        /// Maps analytical curve members (beams, braces, etc.) into structural curve members.
        /// </summary>
        /// <param name="doc">Document providing analytical members.</param>
        public void StructuralCurveMemberLooper(Document doc)
        {
            IEnumerable<AnalyticalMember> analyticalMembers = new FilteredElementCollector(doc)
                .OfClass(typeof(AnalyticalMember))
                .Cast<AnalyticalMember>();

            foreach (AnalyticalMember member in analyticalMembers)
            {
                StructuralCurveMemberMapper.Map(_manager, ModelIndex, member);
            }
        }

        /// <summary>
        /// Iterates element types capable of having structural cross sections and maps them.
        /// </summary>
        /// <param name="doc">Document providing type definitions.</param>
        /// <param name="allowedTypeIds">Optional filter restricting exports to types actually placed in the model.</param>
        public void CrossSectionLooper(Document doc, ISet<ElementId>? allowedTypeIds = null)
        {
            ElementMulticlassFilter filter = new ElementMulticlassFilter(new[]
            {
                typeof(FloorType),
                typeof(WallType),
                typeof(FamilySymbol),
                typeof(RoofType),
                typeof(CeilingType)
            });

            IEnumerable<Element> elementTypes = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .WherePasses(filter)
                .ToElements();

            foreach (Element element in elementTypes)
            {
                if (allowedTypeIds != null && !allowedTypeIds.Contains(element.Id))
                {
                    continue;
                }

                CrossSectionMapper.Map(_manager, ModelIndex, element);
            }
        }

        /// <summary>
        /// Scans placed elements to determine which materials and type definitions are actually in use.
        /// </summary>
        /// <param name="doc">Document to analyze.</param>
        /// <returns>Tuple containing the used material and type IDs.</returns>
        private static (HashSet<ElementId> usedMaterialIds, HashSet<ElementId> usedTypeIds) CollectUsedElementData(Document doc)
        {
            HashSet<ElementId> usedMaterialIds = new HashSet<ElementId>();
            HashSet<ElementId> usedTypeIds = new HashSet<ElementId>();

            IEnumerable<Element> placedElements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element element in placedElements)
            {
                ElementId typeId = element.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    usedTypeIds.Add(typeId);
                }

                ICollection<ElementId> materialIds = element.GetMaterialIds(false);
                foreach (ElementId materialId in materialIds)
                {
                    if (materialId != null && materialId != ElementId.InvalidElementId)
                    {
                        usedMaterialIds.Add(materialId);
                    }
                }
            }

            return (usedMaterialIds, usedTypeIds);
        }

        /// <summary>
        /// Converts analytical panels into structural surface members.
        /// </summary>
        /// <param name="doc">Document providing analytical panels.</param>
        public void StructuralSurfaceMemberLooper(Document doc)
        {
            IEnumerable<AnalyticalPanel> analyticalPanels = new FilteredElementCollector(doc)
                .OfClass(typeof(AnalyticalPanel))
                .Cast<AnalyticalPanel>();

            foreach (AnalyticalPanel surface in analyticalPanels)
            {
                StructuralSurfaceMemberMapper.Map(_manager, ModelIndex, surface);
            }
        }
    }
}
