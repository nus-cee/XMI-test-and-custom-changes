using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using ClassMapper;
using XmiSchema.Core.Entities;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB;
using Revit_to_XMI.utils;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models;

namespace JsonExporter
{
    public class JsonExporter
    {
        public string Export(Document doc)
        {
            var builder = new XmiBuilder();
            builder.BuildModel(doc);
            return builder.GetJson();
        }
    }
}
//    {
//        //private readonly IXmiManager _manager;

//        public BuildJson(Document doc)
//        {
//            IXmiManager manager = new XmiManager(); // 

//            XmiModel model = new XmiModel();

//            manager.Models.Add(model);

//            // 接下来的方法后期换成从revit中mapping来的数据

//            // 创建点
//            var point1 = new XmiPoint3D("p1", "Point1", "", "", "", 0, 0, 0);
//            var point2 = new XmiPoint3D("p2", "Point2", "", "", "", 5, 0, 0);

//            // 添加点实体
//            manager.AddXmiPoint3DToModel(0, point1);
//            manager.AddXmiPoint3DToModel(0, point2);

//            // 创建楼层
//            var storey = new XmiStructuralStorey("s1", "Storey1", "", "", "", 3.5f, 5000, "RX", "RY", "RZ");
//            manager.AddXmiStructuralStoreyToModel(0, storey);

//            // 创建节点
//            var node1 = new XmiStructuralPointConnection("n1", "Node1", "", "", "", storey, point1);
//            var node2 = new XmiStructuralPointConnection("n2", "Node2", "", "", "", storey, point2);
//            manager.AddXmiStructuralPointConnectionToModel(0, node1);
//            manager.AddXmiStructuralPointConnectionToModel(0, node2);

//            // 创建材料
//            var material = new XmiStructuralMaterial("m1", "Concrete", "", "", "", XmiStructuralMaterialTypeEnum.Concrete,
//                30, 25, "30000", "12000", "0.2f", 1.2e-5f);
//            manager.AddXmiStructuralMaterialToModel(0, material);

//            // 创建截面
//            var crossSection = new XmiStructuralCrossSection(
//                "cs1", "RectSection", "", "", "", material, XmiShapeEnum.Rectangular,
//                new[] { "b=300", "h=500" }, 150000, 6250000, 6250000, 144.3f, 144.3f,
//                200000, 200000, 250000, 250000, 12000000);
//            manager.AddXmiStructuralCrossSectionToModel(0, crossSection);

//            // 创建段
//            var segment = new XmiSegment("seg1", "Segment1", "", "", "", null, 0f, node1, node2, XmiSegmentTypeEnum.Line);

//            // 创建构件
//            var curve = new XmiStructuralCurveMember(
//                "cm1", "Beam1", "", "", "", crossSection, storey,
//                XmiStructuralCurveMemberTypeEnum.Beam,
//                new List<XmiStructuralPointConnection> { node1, node2 },
//                new List<XmiBaseEntity> { segment },
//                XmiStructuralCurveMemberSystemLineEnum.MiddleMiddle,
//                node1, node2,
//                5.0f,
//                "1,0,0", "0,1,0", "0,0,1",
//                0, 0, 0, 0, 0, 0,
//                "Fixed", "Pinned");
//            manager.AddXmiStructuralCurveMemberToModel(0, curve);

//            // 生成 JSON 并保存
//            var outputPath = "xmi_graph.json";
//            manager.Save(outputPath);

//        }

//        /// <summary>
//        /// 接收一个 XmiModel，添加数据并生成 JSON 表示。
//        /// </summary>
//        /// <param name="inputModel">输入模型</param>
//        /// <returns>JSON 字符串</returns>
//        public string ProcessModel(XmiModel inputModel)
//        {
//            // 1. 初始化模型集合
//            _manager.Models = new List<XmiModel> { inputModel };

//            int modelIndex = 0;

//            // 2. 示例：向模型中添加一些实体对象（你可以替换为实际逻辑）
//            var point = new XmiPoint3D { X = 0, Y = 0, Z = 0 };
//            _manager.AddXmiPoint3DToModel(modelIndex, point);

//            var material = new XmiStructuralMaterial { Name = "Concrete", Grade = "C30" };
//            _manager.AddXmiStructuralMaterialToModel(modelIndex, material);

//            var crossSection = new XmiStructuralCrossSection { Name = "Rect100x200" };
//            _manager.AddXmiStructuralCrossSectionToModel(modelIndex, crossSection);

//            var member = new XmiStructuralCurveMember { Name = "Beam-1" };
//            _manager.AddXmiStructuralCurveMemberToModel(modelIndex, member);

//            // 3. 构建 JSON 输出
//            string json = _manager.BuildJson(modelIndex);
//            return json;



//        }
//    }
//}

//{
//    internal string class woshi
//    {
//        /// <summary>
//        /// 构建并导出 XMI JSON 文件（基于当前全局数据）
//        /// </summary>
//        public static string Ma(Document doc)
//        {
//            // 执行提取逻辑，确保列表已填充
//            Looper.StructuralPointConnectionLooper(doc); // 提取 ReferencePoints -> PointConnection
//            Looper.StructuralStoreyLooper(doc);
//            //Looper.StructuralMaterialLooper(doc);
//            Looper.StructuralCurveMemberLooper(doc);
//            //Looper.StructuralCurveMemberBeamsLooper(doc);
//            //Looper.StructuralSurfaceMemberWallsLooper(doc);
//            //Looper.StructuralSurfaceMemberSlabsLooper(doc);

//            var builder = new XmiSchemaJsonBuilder();

//            // 注册所有点（真实数据）
//            builder.AddEntities(StructuralDataContext.Point3DList);
//            builder.AddEntities(StructuralDataContext.StructuralPointConnectionList);


//            //Autodesk.Revit.UI.TaskDialog.Show("调试输出", "Point3D Count: " + StructuralDataContext.Point3DList.Count);


//            builder.AddEntities(StructuralDataContext.StructuralStoreyList);
//            builder.AddEntities(StructuralDataContext.StructuralMaterialList);
//            builder.AddEntities(StructuralDataContext.StructuralCrossSectionList);
//            builder.AddEntities(StructuralDataContext.StructuralCurveMemberList);

//            builder.AddEntities(StructuralDataContext.StructuralSurfaceMemberWallsList);
//            builder.AddEntities(StructuralDataContext.StructuralSurfaceMemberSlabsList);
//            // ✅ 可扩展添加其他数据，例如材料、楼层、构件等：

//            // 显示每个列表的数量
//            string statsMessage =
//                $"Point3D: {StructuralDataContext.Point3DList.Count}\n" +
//                $"StructuralPointConnection: {StructuralDataContext.StructuralPointConnectionList.Count}\n" +
//                $"StructuralStorey: {StructuralDataContext.StructuralStoreyList.Count}\n" +
//                $"StructuralMaterial: {StructuralDataContext.StructuralMaterialList.Count}\n" +
//                $"StructuralCrossSection: {StructuralDataContext.StructuralCrossSectionList.Count}\n" +
//                $"StructuralCurveMember: {StructuralDataContext.StructuralCurveMemberList.Count}\n" +
//                $"StructuralSurfaceMemberWalls: {StructuralDataContext.StructuralSurfaceMemberWallsList.Count}\n" +
//                $"StructuralSurfaceMemberSlabs: {StructuralDataContext.StructuralSurfaceMemberSlabsList.Count}";

//            // 显示统计弹窗
//            Autodesk.Revit.UI.TaskDialog statsDialog = new Autodesk.Revit.UI.TaskDialog("导出成功");
//            statsDialog.MainInstruction = "已成功导出以下两个文件";
//            statsDialog.MainContent = "数据统计：\n" + statsMessage;

//            statsDialog.Show();

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
