using System.Reflection;
using XmiSchema.Enums;
using XmiSchema.Entities.Bases;

namespace Betekk.RevitXmiExporter.Utils
{
    public static class EnumHelper
    {
        public static TEnum? FromEnumValue<TEnum>(string value) where TEnum : struct, Enum
        {
            foreach (FieldInfo field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                EnumValueAttribute? attribute = field.GetCustomAttribute<EnumValueAttribute>();
                if (attribute != null && attribute.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return (TEnum)field.GetValue(null)!;
                }
            }

            return null;
        }
    }
}
