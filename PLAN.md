# Revit to XMI Schema Exporter - Implementation Plan

## Overview
Building a Revit 2026 plugin from scratch to export structural framing elements (beams and columns) to XMI schema format. This is a clean rebuild on the XMI-210b experimental branch.

## Quick Summary

**Goal:** Export Revit StructuralFraming elements (beams & columns) to XMI schema JSON with dual representation (physical + analytical domains) and deduplicated geometry points.

**Key Implementation Details:**
- Work directly with `XmiModel` API (not XmiManager convenience methods)
- Create `BetekkXmiBuilder.cs` as main orchestrator
- Use `FilteredElementCollector` for StructuralFraming elements
- Extract geometry from `LocationCurve` (start/end points)
- Deduplicate points with 1e-10 tolerance
- Map Beams → `XmiBeam`, Columns → `XmiColumn` (physical)
- Create `XmiStructuralCurveMember` for analytical representation
- Create graph relationships: physical→geometry, physical→analytical, analytical→connections→geometry
- Document missing XmiManager convenience methods in separate file

**Files to Create:**
1. `builder/BetekkXmiBuilder.cs` - Main implementation
2. `docs/XMIMANAGER_MISSING_METHODS.md` - API documentation for user to implement

## Architecture

### XMI Schema Relationship Model
The exporter creates a graph-based dual representation:

**Physical Domain:**
- `XmiBeam` / `XmiColumn` (physical elements from Revit)
- Connected to geometry via: `XmiBeam` → `XmiHasPoint3d` → `XmiPoint3d`
- The `XmiHasPoint3d` relationship has a property indicating point type (startNode/endNode)

**Analysis Domain:**
- `XmiStructuralCurveMember` (analytical representation)
- Connected via: `XmiBeam` → `XmiHasStructuralCurveMember` → `XmiStructuralCurveMember`
- Points via: `XmiStructuralCurveMember` → `XmiHasStructuralPointConnection` → `XmiStructuralPointConnection` → `XmiHasPoint3d` → `XmiPoint3d`

**Point Deduplication:**
- All points deduplicated with tolerance of 1e-10
- Same `XmiPoint3d` instance referenced by both physical and analysis domains when coordinates match

## Current State
- ✅ `BetekkRevitModelToXmiExportCommand.cs` - Command entry point with UI dialogs
- ✅ `BetekkRevitToXmiModelManager.cs` - Facade that calls builder
- ❌ `BetekkXmiBuilder.cs` - Missing, needs creation
- Dependencies: XmiSchema.Core v0.9.1, Revit API 2026

## Implementation Steps

### 1. Create BetekkXmiBuilder.cs
**File:** `builder/BetekkXmiBuilder.cs`

**Responsibilities:**
- Instantiate `XmiManager` and `XmiModel` from XmiSchema.Core library
- Work directly with `XmiModel` level API (not XmiManager) for entity creation
- Create single XmiModel representing current Revit document
- Coordinate element processing loops
- Maintain point deduplication cache
- Serialize final model to JSON via XmiManager

**Key Methods:**
- `BuildModel(Document doc)` - Main orchestration method
- `GetJson()` - Returns JSON string via `XmiManager.BuildJson()`
- `ProcessStructuralFraming(Document doc)` - Loop through beams and columns

**API Strategy:**
- Use `XmiModel` directly for creating entities (XmiBeam, XmiColumn, relationships)
- XmiManager used only for initialization and final JSON serialization

### 2. Create XmiManager API Documentation
**File:** `docs/XMIMANAGER_MISSING_METHODS.md` (new documentation file)

**Purpose:**
Document the methods that need to be added to XmiManager for convenience, since we're working at XmiModel level directly.

**Methods to Document:**
- `CreateBeam()` - Factory method for XmiBeam entities
- `CreateColumn()` - Factory method for XmiColumn entities
- `CreateHasPoint3d()` - Create relationship with pointType property
- `CreateHasStructuralCurveMember()` - Link physical to analytical
- `CreateHasStructuralPointConnection()` - Link analytical to connections
- Any other relationship creation methods

**Include:**
- Proposed method signatures
- Parameters needed
- Expected behavior
- XmiModel-level equivalents that currently work

### 3. Point Deduplication Strategy
**File:** `builder/Point3dCache.cs` (new utility class)

**Responsibilities:**
- Cache `XmiPoint3d` entities by coordinate tuple (X, Y, Z)
- Tolerance-based equality comparison (1e-10)
- Return existing point or create new one

**Implementation:**
```csharp
Dictionary<(double X, double Y, double Z), XmiPoint3d> cache
- Key: Rounded coordinates to tolerance
- Value: XmiPoint3d entity reference
```

### 4. Revit Element Collection
**Location:** Inside `BetekkXmiBuilder.ProcessStructuralFraming()`

**Revit API Approach:**
```csharp
FilteredElementCollector collector = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_StructuralFraming)
    .WhereElementIsNotElementType();

foreach (Element element in collector)
{
    FamilyInstance familyInstance = element as FamilyInstance;
    // Determine if beam or column based on StructuralType property
    // FamilyInstance.StructuralType: Beam, Column, Brace, etc.
}
```

**Reference:** https://www.revitapidocs.com/2026/

### 5. Geometry Extraction
**For each Revit FamilyInstance:**

Extract location curve:
```csharp
LocationCurve locationCurve = familyInstance.Location as LocationCurve;
Curve curve = locationCurve.Curve;
XYZ startPoint = curve.GetEndPoint(0);
XYZ endPoint = curve.GetEndPoint(1);
```

Convert Revit internal units (feet) to millimeters:
- Use existing `Converters.ConvertValueToMillimeter()` utility
- Apply to X, Y, Z coordinates

### 6. XMI Entity Creation Pattern (Using XmiModel Direct API)
**For each Beam/Column:**

1. **Extract geometry points:**
   - Get deduplicated `XmiPoint3d` for start point
   - Get deduplicated `XmiPoint3d` for end point

2. **Create Physical Element:**
   - `XmiBeam` or `XmiColumn` via `XmiManager.CreateBeam()` / `CreateColumn()`
   - Set properties: ID (GUID), Name, NativeId (Revit UniqueId)

3. **Link Physical to Geometry:**
   - Create `XmiHasPoint3d` relationship for start point (property: pointType = "startNode")
   - Create `XmiHasPoint3d` relationship for end point (property: pointType = "endNode")

4. **Create Analysis Element:**
   - `XmiStructuralCurveMember` via `XmiManager.CreateStructuralCurveMember()`

5. **Link Physical to Analysis:**
   - Create `XmiHasStructuralCurveMember` relationship

6. **Create Analysis Point Connections:**
   - Create `XmiStructuralPointConnection` for start (if not exists in cache)
   - Create `XmiStructuralPointConnection` for end (if not exists in cache)
   - Link via `XmiHasStructuralPointConnection`
   - Link connections to same deduplicated `XmiPoint3d` instances

### 7. File Structure
```
builder/
├── BetekkRevitModelToXmiExportCommand.cs  (existing)
├── BetekkRevitToXmiModelManager.cs        (existing)
├── BetekkXmiBuilder.cs                     (NEW)
└── Point3dCache.cs                         (NEW - optional, may inline in builder)

docs/
└── XMIMANAGER_MISSING_METHODS.md          (NEW - API documentation)
```

## Critical Files Reference
- Command: `builder/BetekkRevitModelToXmiExportCommand.cs`
- Manager: `builder/BetekkRevitToXmiModelManager.cs`
- Builder: `builder/BetekkXmiBuilder.cs` (to be created)
- Utils: `Utils/Converters.cs` (existing - unit conversions)
- Utils: `Utils/ModelInfoBuilder.cs` (existing - error logging)
- Project: `RevitXmiExporter.csproj`
- Documentation: `docs/XMIMANAGER_MISSING_METHODS.md` (to be created)

## Deferred to Future Iterations
- Cross-section mapping
- Material properties
- Storey/Level associations
- StructuralSurfaceMember (walls, slabs)
- Advanced analytical properties
- Unit tests

## Technical Notes
- **Revit API Version:** 2026
- **Target Framework:** .NET 8.0
- **XMI Schema:** v0.9.1
- **Unit Conversion:** Revit internal (feet) → Millimeters
- **Point Tolerance:** 1e-10 for deduplication
- **Element Types:** FamilyInstance with StructuralType = Beam or Column
- **Category:** BuiltInCategory.OST_StructuralFraming

## Implementation Priority Order

### Phase 1: Core Infrastructure
1. Create `BetekkXmiBuilder.cs` skeleton with XmiManager + XmiModel setup
2. Implement `ProcessStoreys()` for level extraction
3. Implement point deduplication logic (inline in builder)
4. Implement Revit element collection (FilteredElementCollector for StructuralFraming)

### Phase 2: Entity Creation (XmiModel Direct API)
5. Implement geometry extraction from LocationCurve
6. Create XmiPoint3d entities with deduplication
7. Create XmiBeam and XmiColumn entities
8. Create relationship edges (XmiHasPoint3d with pointType)
9. Create XmiStructuralCurveMember (analytical domain)
10. Create XmiStructuralPointConnection entities
11. Wire up all relationships (physical→geometry, physical→analytical, analytical→connections→geometry)

### Phase 3: Documentation & Testing
12. Create `docs/XMIMANAGER_MISSING_METHODS.md` documenting needed XmiManager convenience methods
13. Test export with sample Revit model
14. Validate JSON output structure
15. Verify point deduplication (check for duplicate coordinates in JSON)
