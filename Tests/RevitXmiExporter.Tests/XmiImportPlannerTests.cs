using Betekk.RevitXmiExporter.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace RevitXmiExporter.Tests;

public class XmiImportPlannerTests
{
    [Fact]
    public void BuildPlan_ColumnAndWallsJson_BuildsExpectedPlanEvidence()
    {
        string json = ReadFixture("column-and-walls.json");

        XmiImportPlan plan = XmiImportPlanner.BuildPlan(json, new[] { "XmiColumn" });

        Assert.Equal(14, plan.Nodes.Count);
        Assert.Equal(14, plan.Edges.Count);
        Assert.Equal(14, plan.NodeIndex.Count);
        Assert.Empty(plan.Diagnostics.SkipReasons);
        Assert.Empty(plan.Diagnostics.FailureReasons);
    }

    [Fact]
    public void RouteNodes_ColumnAndWallsJson_CollectsEntityDiagnostics()
    {
        string json = ReadFixture("column-and-walls.json");

        XmiImportPlan plan = XmiImportPlanner.BuildPlan(json, new[] { "XmiColumn" });

        RouteWithColumnOnlyHandler(plan);

        Assert.Equal(14, plan.Diagnostics.TotalFound);
        Assert.Equal(1, plan.Diagnostics.TotalCreated);
        Assert.Equal(13, plan.Diagnostics.TotalSkipped);
        Assert.Equal(0, plan.Diagnostics.TotalFailed);
        Assert.Equal(13, plan.Diagnostics.UnsupportedSkippedCount);

        Assert.True(plan.Diagnostics.ByEntity.TryGetValue("XmiColumn", out XmiImportEntityStats? columnStats));
        Assert.NotNull(columnStats);
        Assert.Equal(1, columnStats!.Found);
        Assert.Equal(1, columnStats.Created);

        Assert.Contains(
            plan.Diagnostics.SkipReasons,
            reason => reason.Contains("Unsupported entity 'XmiStorey'", StringComparison.Ordinal));
    }

    [Fact]
    public void RouteNodes_ColumnAndWalls2Json_CollectsEntityDiagnostics()
    {
        string json = ReadFixture("column-and-walls-2.json");

        XmiImportPlan plan = XmiImportPlanner.BuildPlan(json, new[] { "XmiColumn" });

        RouteWithColumnOnlyHandler(plan);

        Assert.Equal(12, plan.Diagnostics.TotalFound);
        Assert.Equal(2, plan.Diagnostics.TotalCreated);
        Assert.Equal(10, plan.Diagnostics.TotalSkipped);
        Assert.Equal(0, plan.Diagnostics.TotalFailed);
        Assert.Equal(10, plan.Diagnostics.UnsupportedSkippedCount);

        Assert.True(plan.Diagnostics.ByEntity.TryGetValue("XmiColumn", out XmiImportEntityStats? columnStats));
        Assert.NotNull(columnStats);
        Assert.Equal(2, columnStats!.Found);
        Assert.Equal(2, columnStats.Created);

        Assert.Contains(
            plan.Diagnostics.SkipReasons,
            reason => reason.Contains("Unsupported entity 'XmiLine3d'", StringComparison.Ordinal));
    }

    [Fact]
    public void RouteNodes_WhenHandlerThrows_RecordsFailureWithoutRevitRuntime()
    {
        string json = """
        {
          "nodes": [
            { "Id": "n1", "Name": "Broken Column", "EntityName": "XmiColumn" },
            { "Id": "n2", "Name": "Other", "EntityName": "XmiWall" }
          ],
          "edges": []
        }
        """;

        XmiImportPlan plan = XmiImportPlanner.BuildPlan(json, new[] { "XmiColumn" });

        Dictionary<string, Func<JToken, XmiNodeImportResult>> handlers =
            new Dictionary<string, Func<JToken, XmiNodeImportResult>>(StringComparer.Ordinal)
            {
                ["XmiColumn"] = _ => throw new InvalidOperationException("simulated failure")
            };

        XmiImportNodeRouter.RouteNodes(
            plan.Nodes,
            plan.Diagnostics,
            handlers,
            node => XmiNodeImportResult.Skipped(
                $"Unsupported entity '{node["EntityName"]?.Value<string>() ?? "<missing>"}'."));

        Assert.Equal(2, plan.Diagnostics.TotalFound);
        Assert.Equal(0, plan.Diagnostics.TotalCreated);
        Assert.Equal(1, plan.Diagnostics.TotalSkipped);
        Assert.Equal(1, plan.Diagnostics.TotalFailed);
        Assert.Equal(1, plan.Diagnostics.UnsupportedSkippedCount);
        Assert.Contains(plan.Diagnostics.FailureReasons, r => r.Contains("simulated failure", StringComparison.Ordinal));
    }

    private static void RouteWithColumnOnlyHandler(XmiImportPlan plan)
    {
        Dictionary<string, Func<JToken, XmiNodeImportResult>> handlers =
            new Dictionary<string, Func<JToken, XmiNodeImportResult>>(StringComparer.Ordinal)
            {
                ["XmiColumn"] = _ => XmiNodeImportResult.Created()
            };

        XmiImportNodeRouter.RouteNodes(
            plan.Nodes,
            plan.Diagnostics,
            handlers,
            node =>
            {
                string entityName = node["EntityName"]?.Value<string>() ?? "<missing>";
                string nodeId = node["Id"]?.Value<string>() ?? "<missing-id>";
                string nodeName = node["Name"]?.Value<string>() ?? "<unnamed>";

                return XmiNodeImportResult.Skipped(
                    $"Unsupported entity '{entityName}' (node Id='{nodeId}', Name='{nodeName}').");
            });
    }

    private static string ReadFixture(string fileName)
    {
        string fullPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return File.ReadAllText(fullPath);
    }
}
