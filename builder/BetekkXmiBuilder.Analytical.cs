using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.Utils;
using System.Linq;
using XmiSchema.Entities.Bases;
using XmiSchema.Entities.Commons;
using XmiSchema.Entities.Geometries;
using XmiSchema.Entities.Physical;
using XmiSchema.Entities.Relationships;
using XmiSchema.Entities.StructuralAnalytical;
using XmiSchema.Enums;
using XmiSchema.Managers;
using XmiSchema.Parameters;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Partial class containing physical-to-analytical mapping methods.
    /// Handles relationships between physical elements (beams/columns) and analytical members.
    /// </summary>
    public partial class BetekkXmiBuilder
    {
        private void CreatePhysicalToAnalyticalRelationship(
            XmiBasePhysicalEntity physicalEntity,
            XmiStructuralCurveMember analyticalMember)
        {
            if (physicalEntity != null && analyticalMember != null)
            {
                XmiHasStructuralCurveMember relationship = new XmiHasStructuralCurveMember(
                    physicalEntity,
                    analyticalMember
                );

                _model.AddXmiHasStructuralCurveMember(relationship);
            }
        }

        private void ProcessingPhysicalToAnalytical()
        {
            ProcessBeamPhysicalToAnalyticalMapping();
            ProcessColumnPhysicalToAnalyticalMapping();
            ProcessFloorPhysicalToAnalyticalMapping();
            ProcessWallPhysicalToAnalyticalMapping();
        }

        private void ProcessBeamPhysicalToAnalyticalMapping()
        {
            foreach (var link in _beamToAnalyticalLinks)
            {
                if (string.IsNullOrEmpty(link.analyticalNativeId))
                {
                    continue;
                }

                if (!_analyticalMemberCache.TryGetValue(link.analyticalNativeId, out XmiStructuralCurveMember analyticalMember))
                {
                    continue;
                }

                bool exists = _model.Relationships
                    .OfType<XmiHasStructuralCurveMember>()
                    .Any(r => ReferenceEquals(r.Source, link.beam) && ReferenceEquals(r.Target, analyticalMember));

                if (!exists)
                {
                    CreatePhysicalToAnalyticalRelationship(link.beam, analyticalMember);
                }
            }
        }

        private void ProcessColumnPhysicalToAnalyticalMapping()
        {
            foreach (var link in _columnToAnalyticalLinks)
            {
                if (string.IsNullOrEmpty(link.analyticalNativeId))
                {
                    continue;
                }

                if (!_analyticalMemberCache.TryGetValue(link.analyticalNativeId, out XmiStructuralCurveMember analyticalMember))
                {
                    continue;
                }

                bool exists = _model.Relationships
                    .OfType<XmiHasStructuralCurveMember>()
                    .Any(r => ReferenceEquals(r.Source, link.column) && ReferenceEquals(r.Target, analyticalMember));

                if (!exists)
                {
                    CreatePhysicalToAnalyticalRelationship(link.column, analyticalMember);
                }
            }
        }

        private void ProcessFloorPhysicalToAnalyticalMapping()
        {
            // Floors currently export materials only; no physical-to-analytical links to create.
        }

        private void ProcessWallPhysicalToAnalyticalMapping()
        {
            // Walls currently export materials only; no physical-to-analytical links to create.
        }

        /// <summary>
        /// Gets the associated analytical element ID from a physical element using Revit 2023+ API.
        /// Returns null if no analytical association exists.
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="physicalElement">Physical structural element (beam or column)</param>
        /// <returns>Analytical element ID as string, or null if no analytical model exists</returns>
        private string? GetAnalyticalElementId(Document doc, Element physicalElement)
        {
            try
            {
                AnalyticalToPhysicalAssociationManager manager =
                    AnalyticalToPhysicalAssociationManager.GetAnalyticalToPhysicalAssociationManager(doc);

                if (manager != null)
                {
                    ElementId analyticalElementId = manager.GetAssociatedElementId(physicalElement.Id);

                    if (analyticalElementId != null && analyticalElementId != ElementId.InvalidElementId)
                    {
                        return analyticalElementId.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Failed to get analytical element ID for {physicalElement.Id}: {ex.Message}");
            }

            // Return null if no analytical association exists
            return null;
        }

        /// <summary>
        /// Gets or creates a deduplicated XmiMaterial from a Revit Material.
        /// Uses Revit Material ElementId as cache key.
    }
}
