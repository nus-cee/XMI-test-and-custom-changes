using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using XmiSchema.Core.Enums;

namespace Revit_to_XMI.utils
{

public static class EnumHelper
    {
        public static TEnum? FromEnumValue<TEnum>(string value) where TEnum : struct, Enum
        {
            foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var attribute = field.GetCustomAttribute<EnumValueAttribute>();
                if (attribute != null && attribute.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return (TEnum)field.GetValue(null)!;
                }
            }

            return null;
        }
    }

}
