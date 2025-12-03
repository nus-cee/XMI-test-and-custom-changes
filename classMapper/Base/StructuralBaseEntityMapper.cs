using System;
using Autodesk.Revit.DB;

namespace Betekk.RevitXmiExporter.ClassMapper.Base
{
    /// <summary>
    ///     Provides helper routines for deriving stable identifiers and metadata shared by structural mappers.
    /// </summary>
    internal abstract class StructuralBaseEntityMapper
    {
        /// <summary>
        ///     Extracts the common properties required by the XMI schema from a generic Revit element.
        /// </summary>
        /// <param name="element">The source Revit element that will be exported.</param>
        /// <returns>Tuple containing the generated identifier, display name, IFC GUID, native id, and description.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="element" /> is null.</exception>
        protected static (string Id, string Name, string IfcGuid, string NativeId, string Description) ExtractBasicProperties(Element element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            string id = Guid.NewGuid().ToString();
            string name = ResolveElementName(element.Name, id);
            string ifcGuid = string.IsNullOrWhiteSpace(element.UniqueId) ? id : element.UniqueId;
            string nativeId = ResolveNativeId(element);
            string description = ResolveDescription(element);

            return (id, name, ifcGuid, nativeId, description);
        }

        /// <summary>
        ///     Ensures that every exported element has a readable name by falling back to the generated id when needed.
        /// </summary>
        private static string ResolveElementName(string rawName, string fallbackId)
        {
            return string.IsNullOrWhiteSpace(rawName) ? fallbackId : rawName;
        }

        /// <summary>
        ///     Converts the Revit <see cref="ElementId" /> to the string representation expected by the schema.
        /// </summary>
        private static string ResolveNativeId(Element element)
        {
            ElementId elementId = element.Id;
            if (elementId == null || elementId == ElementId.InvalidElementId)
            {
                return string.Empty;
            }

            return elementId.ToString();
        }

        /// <summary>
        ///     Retrieves the best description available on the element, falling back to comments when necessary.
        /// </summary>
        private static string ResolveDescription(Element element)
        {
            Parameter parameter = element.LookupParameter("Description") ?? element.LookupParameter("Comments");
            if (parameter != null && parameter.StorageType == StorageType.String)
            {
                string value = parameter.AsString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }
}
