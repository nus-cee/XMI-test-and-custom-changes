# XmiManager / XmiModel Missing Methods Documentation

This document outlines the methods that need to be implemented in XmiManager (or clarifies how to use XmiModel directly) for the Revit to XMI exporter.

## Current Issues

Based on compilation errors, we need clarity on:

1. **How to add entities to XmiModel** - Is there a specific API or do we just add to collections?
2. **Constructor signatures** - What parameters are required for each entity type?
3. **How to create relationships** - How to link entities via relationships like `XmiHasPoint3d`?

## Required Entity Creation Methods

### 1. XmiStorey (Building Level/Floor)

**Purpose:** Represent Revit Level elements as XMI Storeys

**Current Issues:**
- Constructor requires parameters (not object initializer)
- `XmiModel.Entities` doesn't have a `Storeys` collection
- Need to know proper way to add storey to model

**Needed API:**
```csharp
// Option A: Factory method on XmiManager
XmiStorey CreateStorey(
    int modelIndex,
    string id,
    string name,
    string ifcGuid,
    string nativeId,
    string description,
    double elevation,
    double mass = 0.0
);

// Option B: Direct instantiation + add to model
XmiStorey storey = new XmiStorey(id, name, ifcGuid, nativeId, description, elevation, mass);
model.AddEntity(storey); // Or model.Entities.Add(storey)?
```

**Constructor Signature Needed:**
- What are the exact parameters for `XmiStorey` constructor?
- Are there optional parameters?
- What properties can be set after construction?

---

### 2. XmiPoint3D (3D Coordinates)

**Purpose:** Represent geometric points with X, Y, Z coordinates

**Current Issues:**
- Constructor signature unknown
- Where to add in XmiModel? (`model.Geometries.Point3D.Add()` doesn't exist)

**Needed API:**
```csharp
// Option A: Factory method
XmiPoint3D CreatePoint3D(
    int modelIndex,
    string id,
    string name,
    string ifcGuid,
    string nativeId,
    string description,
    double x,
    double y,
    double z
);

// Option B: Direct instantiation
XmiPoint3D point = new XmiPoint3D(id, name, ifcGuid, nativeId, description, x, y, z);
model.AddGeometry(point); // Or how to add?
```

---

### 3. XmiStructuralPointConnection (Analytical Node)

**Purpose:** Analytical model node that references a Point3D and belongs to a Storey

**Current Issues:**
- Constructor signature unknown
- How to reference `XmiStorey` and `XmiPoint3D`?
- Where to add in model structure?

**Needed API:**
```csharp
// Option A: Factory method
XmiStructuralPointConnection CreateStructuralPointConnection(
    int modelIndex,
    string id,
    string name,
    string ifcGuid,
    string nativeId,
    string description,
    XmiStorey storey,
    XmiPoint3D point
);

// Option B: Direct instantiation
XmiStructuralPointConnection connection = new XmiStructuralPointConnection(id, name, ifcGuid, nativeId, description);
// Then set storey and point references... how?
connection.StoreyId = storey.Id;
connection.PointId = point.Id;
model.Entities.StructuralAnalytical.StructuralPointConnections.Add(connection); // Does this path exist?
```

---

### 4. XmiStructuralCurveMember (Analytical Beam/Column)

**Purpose:** Analytical representation of linear structural elements

**Current Issues:**
- Constructor has many required parameters
- `Type` property appears to conflict with enum types
- How to reference connections?

**Needed API:**
```csharp
// Option A: Factory method
XmiStructuralCurveMember CreateStructuralCurveMember(
    int modelIndex,
    string id,
    string name,
    string ifcGuid,
    string nativeId,
    string description,
    XmiStructuralCurveMemberTypeEnum memberType,
    XmiSystemLineEnum systemLine,
    double length,
    XmiStructuralPointConnection beginNode,
    XmiStructuralPointConnection endNode,
    string localAxisX = "1,0,0",
    string localAxisY = "0,1,0",
    string localAxisZ = "0,0,1",
    // ... other optional parameters
);

// Option B: Constructor details needed
```

---

### 5. XmiBeam (Physical Beam Element)

**Purpose:** Physical domain representation of Revit beam

**Important:**
- Physical elements need to be distinguished by their **structural category**, not just StructuralType parameter
- **OST_StructuralFraming** → XmiBeam
- Structural classification is critical for proper BIM data exchange

**Current Issues:**
- Type exists but constructor/usage unknown
- How to add to model?
- How to create relationships?

**Needed API:**
```csharp
// Option A: Factory method
XmiBeam CreateBeam(
    int modelIndex,
    string id,
    string name,
    string ifcGuid,
    string nativeId,
    string description
    // other properties?
);

// Option B: Direct instantiation
XmiBeam beam = new XmiBeam(...);
model.Entities.Physical.Beams.Add(beam); // Does this exist?
```

---

### 6. XmiColumn (Physical Column Element)

**Purpose:** Physical domain representation of Revit column

**Important:**
- Physical elements need to be distinguished by their **structural category**, not just StructuralType parameter
- **OST_StructuralColumns** → XmiColumn
- Structural classification is critical for proper BIM data exchange

**Similar to XmiBeam - need constructor and add method**

---

## Required Relationship Creation Methods

### 1. XmiHasPoint3d (Link Physical Element → Point3D)

**Purpose:** Connect XmiBeam/XmiColumn to their start/end point coordinates

**Properties Needed:**
- `pointType`: "startNode" or "endNode"
- Source entity ID (beam/column)
- Target entity ID (point3d)

**Needed API:**
```csharp
// Option A: Factory method
XmiHasPoint3d CreateHasPoint3dRelationship(
    int modelIndex,
    string sourceEntityId,  // XmiBeam or XmiColumn ID
    string targetPointId,   // XmiPoint3D ID
    string pointType        // "startNode" or "endNode"
);

// Option B: Direct instantiation
XmiHasPoint3d relationship = new XmiHasPoint3d
{
    Id = Guid.NewGuid().ToString(),
    SourceId = beam.Id,
    TargetId = point.Id,
    PointType = "startNode"
};
model.Relationships.Add(relationship); // Where do relationships go?
```

---

### 2. XmiHasStructuralCurveMember (Link Physical → Analytical)

**Purpose:** Connect XmiBeam/XmiColumn to their analytical XmiStructuralCurveMember

**Needed API:**
```csharp
XmiHasStructuralCurveMember CreateHasStructuralCurveMemberRelationship(
    int modelIndex,
    string physicalElementId,      // XmiBeam or XmiColumn
    string analyticalMemberId      // XmiStructuralCurveMember
);
```

---

### 3. XmiHasStructuralPointConnection (Link Analytical Member → Connection)

**Purpose:** Connect XmiStructuralCurveMember to its start/end XmiStructuralPointConnections

**Needed API:**
```csharp
XmiHasStructuralPointConnection CreateHasStructuralPointConnectionRelationship(
    int modelIndex,
    string curveMemberId,
    string connectionId,
    string connectionType  // "beginNode" or "endNode"?
);
```

---

## Questions for Implementation

1. **XmiModel Structure:**
   - How are entities organized in XmiModel?
   - Is it `model.Entities.Add(entity)` or specific collections like `model.Entities.Storeys.Add(storey)`?
   - Where do geometries (Point3D) go?

2. **Relationships:**
   - Are relationships first-class entities in the model?
   - Do we create explicit relationship objects, or are they properties on entities?
   - Where are relationships stored in XmiModel?

3. **Constructor vs. Factory:**
   - Should we use entity constructors directly or expect factory methods?
   - What's the recommended pattern for XmiModel v0.9.1?

4. **Entity References:**
   - Do entities reference each other by ID (string) or by object reference?
   - Example: Does `XmiStructuralPointConnection` have a `Storey` property (object ref) or `StoreyId` property (string)?

5. **Serialization:**
   - Does `XmiManager.BuildJson(modelIndex)` properly serialize all entities and relationships?
   - Are there any registration steps needed before serialization?

---

## Recommended Implementation Approach

**Option 1: Work with current XmiModel API directly**
- I need documentation/examples of how to properly use XmiModel
- Show me the correct way to instantiate entities and add them to the model
- Show me how to create relationships

**Option 2: Implement convenience methods in XmiManager**
- Implement factory methods like `CreateBeam()`, `CreateColumn()`, etc.
- These methods handle entity creation and addition to model
- Relationship creation methods that properly wire up the graph

**Option 3: Hybrid approach**
- Use existing XmiModel capabilities where they exist
- Document what's missing for future XmiManager enhancements
- Work around limitations in BetekkXmiBuilder for now

---

## Current BetekkXmiBuilder Status

The builder is now fully implemented with:
- ✅ Basic structure and orchestration
- ✅ Revit element collection logic:
  - **OST_StructuralFraming** → XmiBeam (physical beams)
  - **OST_StructuralColumns** → XmiColumn (physical columns)
  - Category-based classification ensures structural elements are correctly typed
- ✅ Geometry extraction from LocationCurve
- ✅ Point deduplication algorithm (1e-10 tolerance)
- ✅ Entity creation using XmiModel API
- ✅ Relationship creation using XmiModel API
- ✅ Dual representation (Physical + Analytical domains)
- ✅ IFC GUID extraction (reads from native Revit, does not generate)
- ✅ Project builds successfully

**Implementation Details:**
- Using `XmiModel.CreateStorey()`, `CreatePoint3D()`, `CreateStructurePointConnection()`, `CreateStructuralCurveMember()`
- Using constructors for `XmiBeam` and `XmiColumn` (no Create methods available)
- Adding entities via `AddXmiBeam()`, `AddXmiColumn()`, etc.
- Relationships created with constructors, added via `AddXmiHasPoint3D()`, `AddXmiHasStructuralCurveMember()`
- Using `Description` field on `XmiHasPoint3D` to store pointType ("startNode"/"endNode")

**Recommendations for XmiSchema.Core Library:**

### 1. Add `pointType` Property to XmiHasPoint3D
Currently using the `Description` field as a workaround to store "startNode" vs "endNode" designation.

**Recommended Addition:**
```csharp
public class XmiHasPoint3D : XmiBaseRelationship
{
    // Existing properties...

    /// <summary>
    /// Indicates the point type: "startNode", "endNode", "midPoint", etc.
    /// </summary>
    public string? PointType { get; set; }
}
```

### 2. Add CreateBeam() Factory Method to XmiModel
For consistency with other entity types (CreateStorey, CreatePoint3D, etc.)

**Recommended Addition:**
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
```

### 3. Add CreateColumn() Factory Method to XmiModel
Same rationale as CreateBeam()

**Recommended Addition:**
```csharp
public XmiColumn CreateColumn(
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
    var column = new XmiColumn(id, name, ifcGuid, nativeId, description, systemLine,
        length, localAxisX, localAxisY, localAxisZ,
        beginNodeXOffset, endNodeXOffset, beginNodeYOffset,
        endNodeYOffset, beginNodeZOffset, endNodeZOffset);

    AddXmiColumn(column);
    return column;
}
```

### 4. Add Factory Methods for Relationships (Optional)
For convenience and consistency

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
        relationship.PointType = pointType; // Assumes pointType property added
    }
    AddXmiHasPoint3D(relationship);
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

**Next Steps:**
1. Test with sample Revit model containing beams and columns
2. Validate JSON output structure
3. Verify point deduplication is working correctly
4. Add cross-section and material mapping in future iterations
