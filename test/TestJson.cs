//using System;
//using System.Collections.Generic;
//using XmiSchema.Core.Results;

//using Autodesk.Revit.DB;
//using Lists;
//using Revit_to_XMI.utils;


//namespace Utils
//{
//    internal static class TestCaller
//    {
//        /// <summary>
//        /// 构建并导出 XMI JSON 文件（基于当前全局数据）
//        /// </summary>
//        public static string BuildJson(Document doc)
//        {
//            // 执行提取逻辑，确保列表已填充
//            //Looper.Point3DLooper(doc); // 提取 ReferencePoints -> Point3DList
//            //Looper.StructuralStoreyLooper(doc);
//            //Looper.StructuralMaterialLooper(doc);
//            //Looper.StructuralPointConnectionLooper(doc);
//            //Looper.StructuralCurveMemberColumnsLooper(doc);
//            //Looper.StructuralCurveMemberBeamsLooper(doc);
//            //Looper.StructuralSurfaceMemberWallsLooper(doc);
//            //Looper.StructuralSurfaceMemberSlabsLooper(doc);

//            var builder = new XmiSchemaJsonBuilder();

//            // 注册所有点（真实数据）
//            //builder.AddEntities(StructuralDataContext.Point3DList);
//            //builder.AddEntities(StructuralDataContext.StructuralStoreyList);
//            //builder.AddEntities(StructuralDataContext.StructuralMaterialList);
//            builder.AddEntities(StructuralDataContext.StructuralCurveMemberList);
//            //builder.AddEntities(StructuralDataContext.StructuralCurveMemberBeamsList);
//            //builder.AddEntities(StructuralDataContext.StructuralSurfaceMemberWallsList);
//            //builder.AddEntities(StructuralDataContext.StructuralSurfaceMemberSlabsList);
//            // ✅ 可扩展添加其他数据，例如材料、楼层、构件等：
//            // builder.AddEntities(StructuralDataContext.StructuralMaterialList);
//            // builder.AddEntities(StructuralDataContext.StructuralSurfaceMemberList);
//            // builder.AddEntities(...)

//            string modelJson = ModelInfoBuilder.BuildModelInfoJson(doc);
//            // 构建并导出 JSON 文件
//            string entitiesJson = builder.BuildJsonString();
//            string trimmedEntitiesJson = entitiesJson.Trim();
//            if (trimmedEntitiesJson.StartsWith("{") && trimmedEntitiesJson.EndsWith("}"))
//            {
//                trimmedEntitiesJson = trimmedEntitiesJson.Substring(1, trimmedEntitiesJson.Length - 2);
//            }

//            string finalJson = "{\n" + modelJson + ",\n" + trimmedEntitiesJson + "\n}";
//            return finalJson;
//        }
//    }
//}
