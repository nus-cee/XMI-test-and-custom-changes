# XmiSchema Improvement Requests

This document outlines recommended improvements to the XmiSchema library based on implementation experience with the Revit to XMI exporter.

## Priority: HIGH

### 1. Make `crossSection` Parameter Optional in `CreateStructuralCurveMember`

**Current Signature (v0.9.1):**
```csharp
public XmiStructuralCurveMember CreateStructuralCurveMember(
    string id,
    string name,
    string ifcGuid,
    string nativeId,
    string description,
    XmiCrossSection crossSection,  // ❌ Required (non-nullable)
    XmiStorey? storey,
    XmiStructuralCurveMemberTypeEnum curveMemberType,
    List<XmiStructuralPointConnection> nodes,
    List<XmiSegment>? segments,
    XmiSystemLineEnum systemLine,
    XmiStructuralPointConnection beginNode,
    XmiStructuralPointConnection endNode,
    double length,
    string localAxisX,
    string localAxisY,
    string localAxisZ,
    double beginNodeXOffset,
    double endNodeXOffset,
    double beginNodeYOffset,
    double endNodeYOffset,
    double beginNodeZOffset,
    double endNodeZOffset,
    string endFixityStart,
    string endFixityEnd)
```

**Recommended Change:**
```csharp
public XmiStructuralCurveMember CreateStructuralCurveMember(
    // ... other parameters
    XmiCrossSection? crossSection,  // ✅ Make nullable
    // ... remaining parameters
)
```

**Rationale:**
- In real-world BIM workflows, analytical elements may not have section types assigned yet
- Cross-sections are often assigned during later design stages
- The XMI schema should support partial/incomplete models
- Other optional parameters (like `storey` and `segments`) are already nullable

**Impact:**
- **Without this change:** Cannot export analytical elements that don't have section types assigned
- **With this change:** Can export complete analytical models at any design stage

**Current Workaround:**
Creating a dummy/placeholder cross-section for analytical members without section types, which pollutes the XMI data with meaningless entities.

---

## Priority: MEDIUM

### 2. Add `pointType` Property to `XmiHasPoint3D` Relationship

**Current Implementation (v0.9.1):**
```csharp
public class XmiHasPoint3D : XmiBaseRelationship
{
    // No pointType property - using Description field as workaround
}
```

**Recommended Addition:**
```csharp
public class XmiHasPoint3D : XmiBaseRelationship
{
    /// <summary>
    /// Indicates the point type: "startNode", "endNode", "midPoint", etc.
    /// </summary>
    public string? PointType { get; set; }
}
```

**Rationale:**
- Physical elements (beams, columns) connect to multiple Point3D entities (start/end points)
- Need to distinguish which point is which in the relationship
- Currently using the generic `Description` field as a workaround
- A dedicated property improves data clarity and schema semantics

**Current Workaround:**
```csharp
XmiHasPoint3D startPointRel = new XmiHasPoint3D(physicalEntity, startPoint);
startPointRel.Description = "startNode";  // ❌ Misusing Description field
```

---

## Priority: LOW

### 3. Add Factory Methods for Physical Element Creation

**Currently Missing:**
- `CreateBeam()` factory method
- `CreateColumn()` factory method

**Current Implementation:**
- Must use constructors directly + manual `AddXmiBeam()` / `AddXmiColumn()` calls
- Inconsistent with analytical elements (which have `CreateStructuralCurveMember()`)

**Recommended Additions:**
```csharp
public XmiBeam CreateBeam(
    string id,
    string name,
    string ifcGuid,
    string nativeId,
    string description,
    XmiSystemLineEnum systemLine,
    double length,
    string localAxisX,
    string localAxisY,
    string localAxisZ,
    double beginNodeXOffset = 0,
    double endNodeXOffset = 0,
    double beginNodeYOffset = 0,
    double endNodeYOffset = 0,
    double beginNodeZOffset = 0,
    double endNodeZOffset = 0)
{
    var beam = new XmiBeam(id, name, ifcGuid, nativeId, description, systemLine,
        length, localAxisX, localAxisY, localAxisZ,
        beginNodeXOffset, endNodeXOffset, beginNodeYOffset,
        endNodeYOffset, beginNodeZOffset, endNodeZOffset);

    AddXmiBeam(beam);
    return beam;
}

public XmiColumn CreateColumn(/* similar parameters */)
{
    // Similar implementation
}
```

**Rationale:**
- API consistency (other entity types have Create methods)
- Encapsulates entity creation + registration
- Reduces boilerplate code

---

## Priority: LOW

### 4. Add Convenience Factory Methods for Relationships

**Recommended Additions:**
```csharp
public XmiHasPoint3D CreateHasPoint3DRelationship(
    XmiBaseEntity source,
    XmiBaseEntity target,
    string pointType = "")
{
    var relationship = new XmiHasPoint3D(source, target);
    if (!string.IsNullOrWhiteSpace(pointType))
    {
        relationship.PointType = pointType; // Assumes Priority #2 is implemented
    }
    AddXmiHasPoint3D(relationship);
    return relationship;
}

public XmiHasCrossSection CreateHasCrossSectionRelationship(
    XmiBaseEntity source,
    XmiCrossSection target)
{
    var relationship = new XmiHasCrossSection(source, target);
    AddXmiHasCrossSection(relationship);
    return relationship;
}

public XmiHasStructuralCurveMember CreateHasStructuralCurveMemberRelationship(
    XmiBasePhysicalEntity source,
    XmiBaseStructuralAnalyticalEntity target)
{
    var relationship = new XmiHasStructuralCurveMember(source, target);
    AddXmiHasStructuralCurveMember(relationship);
    return relationship;
}
```

**Rationale:**
- Reduces repetitive code
- Ensures relationships are properly registered
- API consistency

---

## Implementation Status in Revit Exporter

### ✅ Successfully Implemented
- Material extraction and mapping
- Cross-section extraction from analytical members' "Section Type" property
- Complete analytical model export (with or without physical associations)
- Point deduplication
- Dual representation (physical + analytical domains)

### ⚠️ Workarounds Applied
- **Priority #1:** **WORKAROUND ACTIVE** - Using synthetic placeholder cross-sections for analytical members without section types
  - Placeholder ID: `"placeholder-no-section-type"`
  - Placeholder Name: `"[PLACEHOLDER] No Section Type Assigned"`
  - Placeholder NativeId: `"synthetic:placeholder:no-section-type"`
  - Single shared instance to avoid duplicate placeholders
  - Clearly documented in Description field with explanation
  - **Will be removed** once XmiSchema supports nullable cross-sections
- **Priority #2:** Using `Description` field to store pointType ("startNode"/"endNode")
- **Priority #3:** Using constructors + manual Add calls for beams/columns

### 📋 Recommended Testing
Once improvements are implemented in XmiSchema:
1. Test with analytical elements that have no section types assigned
2. Validate that nullable cross-sections serialize/deserialize correctly
3. Ensure backwards compatibility with existing XMI files

---

## Summary

**Most Critical:** Priority #1 (nullable cross-section) blocks complete analytical model export and should be addressed in the next XmiSchema release.

**Nice to Have:** Priorities #2-4 improve API ergonomics and data semantics but have acceptable workarounds.
