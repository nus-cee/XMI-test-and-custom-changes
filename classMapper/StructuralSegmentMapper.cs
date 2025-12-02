using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Geometries;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal static class StructuralSegmentMapper
    {
        public static List<XmiSegment> MapCurveSegments(string ownerId, string ownerName, string ownerNativeId, Curve curve)
        {
            return BuildSegments(ownerId, ownerName, ownerNativeId, curve != null ? new[] { curve } : Array.Empty<Curve>());
        }

        public static List<XmiSegment> MapLoopSegments(string ownerId, string ownerName, string ownerNativeId, IEnumerable<Curve> curves)
        {
            return BuildSegments(ownerId, ownerName, ownerNativeId, curves);
        }

        private static List<XmiSegment> BuildSegments(string ownerId, string ownerName, string ownerNativeId, IEnumerable<Curve> curves)
        {
            List<XmiSegment> segments = new();
            if (curves == null)
            {
                return segments;
            }

            int index = 1;
            foreach (Curve curve in curves)
            {
                if (curve == null)
                {
                    continue;
                }

                double rawLength = curve.ApproximateLength;
                if (rawLength <= 0)
                {
                    rawLength = curve.Length;
                }

                float mmLength = (float)Converters.ConvertValueToMillimeter(rawLength);
                segments.Add(CreateSegment(ownerId, ownerName, ownerNativeId, index, mmLength, ResolveSegmentType(curve)));
                index++;
            }

            return segments;
        }

        private static XmiSegment CreateSegment(string ownerId, string ownerName, string ownerNativeId, int index, float length, XmiSegmentTypeEnum type)
        {
            string segId = $"{ownerId}_seg_{index}";
            string segName = $"{ownerName} Segment {index}";
            string segNativeId = $"{ownerNativeId}_SEG_{index}";
            string description = $"Segment {index} for {ownerName}";
            return new XmiSegment(
                segId,
                segName,
                Guid.NewGuid().ToString(),
                segNativeId,
                description,
                length,
                type);
        }

        private static XmiSegmentTypeEnum ResolveSegmentType(Curve curve)
        {
            return curve switch
            {
                Line => XmiSegmentTypeEnum.Line,
                Arc => XmiSegmentTypeEnum.Arc,
                NurbSpline => XmiSegmentTypeEnum.Spline,
                HermiteSpline => XmiSegmentTypeEnum.Spline,
                _ => XmiSegmentTypeEnum.Unknown
            };
        }
    }
}
