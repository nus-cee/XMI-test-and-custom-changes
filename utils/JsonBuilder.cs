using System;
using System.Collections.Generic;
using XmiBuilder;
using XmiCore;
using Autodesk.Revit.DB;
using Lists;


namespace Utils
{
    internal static class JsonBuilder
    {
        /// <summary>
        /// 构建并导出 XMI JSON 文件（基于当前全局数据）
        /// </summary>
        public static string BuildJson(Document doc)
        {
            // 执行提取逻辑，确保列表已填充
            Looper.Point3DLooper(doc); // 提取 ReferencePoints -> Point3DList
            Looper.StructrualStoreyLooper(doc);
            Looper.StructrualMaterialLooper(doc);


            var builder = new XmiSchemaJsonBuilder();

            // 注册所有点（真实数据）
            builder.AddEntities(StructuralDataContext.Point3DList);
            builder.AddEntities(StructuralDataContext.StructuralStoreyList);
            builder.AddEntities(StructuralDataContext.StructuralMaterialList);

            // ✅ 可扩展添加其他数据，例如材料、楼层、构件等：
            // builder.AddEntities(StructuralDataContext.StructuralMaterialList);
            // builder.AddEntities(StructuralDataContext.StructuralSurfaceMemberList);
            // builder.AddEntities(...)

            // 构建并导出 JSON 文件
            return builder.BuildJsonString();

        }
    }
}
