# XmiLine3d Duplication Debug Logging

## Overview
Added comprehensive logging to track XmiLine3d creation and identify duplication issues.

## Log Location
All logs are written to: `error_log.txt` in the export directory

## What Gets Logged

### 1. During GetOrCreateLine3d Execution (Per Call)

For **each** element that creates a line, you'll see:

```
[GetOrCreateLine3d] Called for element: nativeId='<RevitElementId>', name='<ElementName>'
[GetOrCreateLine3d]   Candidate StartPoint: (X, Y, Z) ID=<PointId>
[GetOrCreateLine3d]   Candidate EndPoint:   (X, Y, Z) ID=<PointId>
[GetOrCreateLine3d]   Found N existing XmiLine3d entities in model
```

### 2. Comparison with Existing Lines

For **each existing line** in the model, you'll see:

```
[GetOrCreateLine3d]   Comparing with existing line [0]: ID=<LineId>, NativeId=<RevitId>, Name=<Name>
[GetOrCreateLine3d]     Existing StartPoint: (X, Y, Z) ID=<PointId>
[GetOrCreateLine3d]     Existing EndPoint:   (X, Y, Z) ID=<PointId>
[GetOrCreateLine3d]     IsCoincident result: True/False
```

### 3. Result of Each Call

**If a match is found (reused):**
```
[GetOrCreateLine3d]   ✓ MATCH FOUND! Reusing existing line ID=<LineId>, NativeId=<RevitId>
[GetOrCreateLine3d] RESULT: Reused existing XmiLine3d (ID=<LineId>, NativeId=<RevitId>)
```

**If no match found (new line created):**
```
[GetOrCreateLine3d] RESULT: Created NEW XmiLine3d (ID=<LineId>, NativeId=<RevitId>)
[GetOrCreateLine3d]   Total XmiLine3d count in model: N
```

### 4. Final Summary Report (End of Export)

```
========================================
[XmiLine3d Summary] Total count: N
========================================
[1] ID=<LineId>, NativeId=<RevitId>, Name=<Name>
    StartPoint: (X, Y, Z) PointID=<PointId>
    EndPoint:   (X, Y, Z) PointID=<PointId>
[2] ID=<LineId>, NativeId=<RevitId>, Name=<Name>
    ...

[XmiLine3d Summary] Checking for coincident lines...
⚠ DUPLICATE FOUND: Line[0] (ID=<LineId>) is coincident with Line[1] (ID=<LineId>)
  Line[0]: NativeId=<RevitId>, Name=<Name>
  Line[1]: NativeId=<RevitId>, Name=<Name>

[XmiLine3d Summary] ⚠ Found N duplicate line(s)!
========================================
```

## How to Debug Duplication Issues

### Step 1: Find the Duplicate Lines
Look at the final summary for lines marked as duplicates:
```
⚠ DUPLICATE FOUND: Line[X] is coincident with Line[Y]
```

### Step 2: Identify When They Were Created
Search backwards in the log for:
```
Created NEW XmiLine3d (ID=<LineId from Line[X]>)
Created NEW XmiLine3d (ID=<LineId from Line[Y]>)
```

### Step 3: Check Why IsCoincident Failed
For the second duplicate that was created, look at the comparison logs just before it:
- Did it compare against the first duplicate?
- What was the `IsCoincident result`?
- Were the coordinates exactly the same?
- Were the Point IDs different?

### Step 4: Analyze the Root Cause

**Possible causes:**

1. **Different XmiPoint3d instances with same coordinates**
   - Check if `PointID` is different even though coordinates match
   - `IsCoincident` uses `XmiPoint3d.Equals()` which may use referential equality

2. **Floating point precision issues**
   - Compare coordinates to 6 decimal places
   - Look for tiny differences (e.g., 1000.000000 vs 1000.000001)

3. **IsCoincident not working as expected**
   - Check if `IsCoincident result` is `False` even when coordinates are identical
   - This indicates a bug in XmiSchema 0.12.0's `IsCoincident` implementation

4. **Line created multiple times from same element**
   - Check if the same `nativeId` appears multiple times
   - May indicate element is being processed twice

## Example Debug Session

```log
[GetOrCreateLine3d] Called for element: nativeId='123456', name='Beam1'
[GetOrCreateLine3d]   Candidate StartPoint: (0.000000, 0.000000, 0.000000) ID=point-abc
[GetOrCreateLine3d]   Candidate EndPoint:   (1000.000000, 0.000000, 0.000000) ID=point-def
[GetOrCreateLine3d]   Found 0 existing XmiLine3d entities in model
[GetOrCreateLine3d] RESULT: Created NEW XmiLine3d (ID=line-1, NativeId=123456_line)

[GetOrCreateLine3d] Called for element: nativeId='789012', name='Beam2'
[GetOrCreateLine3d]   Candidate StartPoint: (1000.000000, 0.000000, 0.000000) ID=point-ghi
[GetOrCreateLine3d]   Candidate EndPoint:   (0.000000, 0.000000, 0.000000) ID=point-jkl
[GetOrCreateLine3d]   Found 1 existing XmiLine3d entities in model
[GetOrCreateLine3d]   Comparing with existing line [0]: ID=line-1, NativeId=123456_line
[GetOrCreateLine3d]     Existing StartPoint: (0.000000, 0.000000, 0.000000) ID=point-abc
[GetOrCreateLine3d]     Existing EndPoint:   (1000.000000, 0.000000, 0.000000) ID=point-def
[GetOrCreateLine3d]     IsCoincident result: False  👈 BUG HERE!
[GetOrCreateLine3d] RESULT: Created NEW XmiLine3d (ID=line-2, NativeId=789012_line)
```

In this example, `IsCoincident` returned `False` even though the lines are reversed (A→B vs B→A).
This indicates that `IsCoincident()` is not working correctly with different Point instances.

## Next Steps Based on Findings

1. **If Point IDs are different:**
   - `IsCoincident()` may be using referential equality on Points
   - Need to ensure same Point instances are reused (check `GetOrReusePoint3D`)

2. **If IsCoincident returns False incorrectly:**
   - Bug in XmiSchema 0.12.0's `IsCoincident` implementation
   - May need to report to XmiSchema maintainers
   - Or implement custom comparison logic

3. **If same element processed twice:**
   - Check why element appears multiple times in Revit collectors
   - Add guard to skip already-processed elements
