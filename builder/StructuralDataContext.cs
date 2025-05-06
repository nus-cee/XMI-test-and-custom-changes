using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XmiCore;

namespace Lists

{
    public static class StructuralDataContext
    {
  
        //public static List<Dictionary<string, object>> StructuralSegmentList { get; set; } = new List<Dictionary<string, object>>();

         public static List<Dictionary<string, object>> StructuralUnitList { get; set; } = new List<Dictionary<string, object>>();



        // start here
        public static List<XmiStructuralPointConnection> StructuralPointConnectionList { get; set; } = new List<XmiStructuralPointConnection>();


        public static List<XmiPoint3D> Point3DList { get; set; } = new List<XmiPoint3D>();

        public static List<XmiStructuralStorey> StructuralStoreyList { get; set; } = new List<XmiStructuralStorey>();

        public static List<XmiStructuralMaterial> StructuralMaterialList { get; set; } = new List<XmiStructuralMaterial>();

        public static List<XmiStructuralCurveMember> StructuralCurveMemberList { get; set; } = new List<XmiStructuralCurveMember>();

        
        public static List<XmiStructuralSurfaceMember> StructuralSurfaceMemberWallsList { get; set; } = new List<XmiStructuralSurfaceMember>();

        public static List<XmiStructuralSurfaceMember> StructuralSurfaceMemberSlabsList { get; set; } = new List<XmiStructuralSurfaceMember>();



        public static List<XmiStructuralCrossSection> StructuralCrossSectionList { get; set; } = new List<XmiStructuralCrossSection>();


    }
}