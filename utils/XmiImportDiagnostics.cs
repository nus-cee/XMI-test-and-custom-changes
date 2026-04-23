using Newtonsoft.Json.Linq;

namespace Betekk.RevitXmiExporter.Utils
{
    public sealed class XmiImportEntityStats
    {
        public int Found { get; private set; }
        public int Created { get; private set; }
        public int Skipped { get; private set; }
        public int Failed { get; private set; }

        public void IncrementFound() => Found++;
        public void IncrementCreated() => Created++;
        public void IncrementSkipped() => Skipped++;
        public void IncrementFailed() => Failed++;
    }

    public sealed class XmiImportDiagnostics
    {
        private readonly Dictionary<string, XmiImportEntityStats> _byEntity =
            new Dictionary<string, XmiImportEntityStats>(StringComparer.Ordinal);

        private readonly List<string> _skipReasons = new List<string>();
        private readonly List<string> _failureReasons = new List<string>();

        public IReadOnlyDictionary<string, XmiImportEntityStats> ByEntity => _byEntity;
        public IReadOnlyList<string> SkipReasons => _skipReasons;
        public IReadOnlyList<string> FailureReasons => _failureReasons;

        public int TotalFound => _byEntity.Values.Sum(x => x.Found);
        public int TotalCreated => _byEntity.Values.Sum(x => x.Created);
        public int TotalSkipped => _byEntity.Values.Sum(x => x.Skipped);
        public int TotalFailed => _byEntity.Values.Sum(x => x.Failed);

        public int UnsupportedSkippedCount { get; private set; }

        public void RecordFound(string entityName)
        {
            GetStats(entityName).IncrementFound();
        }

        public void RecordCreated(string entityName)
        {
            GetStats(entityName).IncrementCreated();
        }

        public void RecordSkipped(string entityName, string reason, bool isUnsupported)
        {
            GetStats(entityName).IncrementSkipped();
            if (isUnsupported)
            {
                UnsupportedSkippedCount++;
            }

            _skipReasons.Add(reason);
        }

        public void RecordFailed(string entityName, string reason)
        {
            GetStats(entityName).IncrementFailed();
            _failureReasons.Add(reason);
        }

        public string BuildSummaryLine()
        {
            return $"found={TotalFound}, created={TotalCreated}, skipped={TotalSkipped}, failed={TotalFailed}, unsupportedSkipped={UnsupportedSkippedCount}";
        }

        public IEnumerable<KeyValuePair<string, XmiImportEntityStats>> GetOrderedEntityStats()
        {
            return _byEntity.OrderBy(x => x.Key, StringComparer.Ordinal);
        }

        private XmiImportEntityStats GetStats(string entityName)
        {
            string key = string.IsNullOrWhiteSpace(entityName) ? "<missing>" : entityName;

            if (!_byEntity.TryGetValue(key, out XmiImportEntityStats? stats))
            {
                stats = new XmiImportEntityStats();
                _byEntity[key] = stats;
            }

            return stats;
        }
    }

    public enum XmiNodeImportStatus
    {
        Created,
        Skipped
    }

    public readonly struct XmiNodeImportResult
    {
        public XmiNodeImportResult(XmiNodeImportStatus status, string reason = "")
        {
            Status = status;
            Reason = reason ?? string.Empty;
        }

        public XmiNodeImportStatus Status { get; }
        public string Reason { get; }

        public static XmiNodeImportResult Created() => new XmiNodeImportResult(XmiNodeImportStatus.Created);
        public static XmiNodeImportResult Skipped(string reason) => new XmiNodeImportResult(XmiNodeImportStatus.Skipped, reason);
    }

    public static class XmiImportNodeRouter
    {
        public static void RouteNodes(
            IReadOnlyList<JToken> nodes,
            XmiImportDiagnostics diagnostics,
            IReadOnlyDictionary<string, Func<JToken, XmiNodeImportResult>> handlers,
            Func<JToken, XmiNodeImportResult> unsupportedHandler)
        {
            foreach (JToken node in nodes)
            {
                string entityName = node["EntityName"]?.Value<string>() ?? "<missing>";
                string nodeId = node["Id"]?.Value<string>() ?? "<missing-id>";
                string nodeName = node["Name"]?.Value<string>() ?? "<unnamed>";

                diagnostics.RecordFound(entityName);

                Func<JToken, XmiNodeImportResult> handler =
                    handlers.TryGetValue(entityName, out Func<JToken, XmiNodeImportResult>? matched)
                        ? matched
                        : unsupportedHandler;

                bool isUnsupported = !handlers.ContainsKey(entityName);

                try
                {
                    XmiNodeImportResult result = handler(node);
                    if (result.Status == XmiNodeImportStatus.Created)
                    {
                        diagnostics.RecordCreated(entityName);
                    }
                    else
                    {
                        string reason = string.IsNullOrWhiteSpace(result.Reason)
                            ? $"Skipped entity '{entityName}' (node Id='{nodeId}', Name='{nodeName}')."
                            : result.Reason;

                        diagnostics.RecordSkipped(entityName, reason, isUnsupported);
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.RecordFailed(
                        entityName,
                        $"Failed entity '{entityName}' (node Id='{nodeId}', Name='{nodeName}'): {ex.Message}");
                }
            }
        }
    }

    public readonly struct XmiGraphEdge
    {
        public XmiGraphEdge(string source, string target, string entityName)
        {
            Source = source;
            Target = target;
            EntityName = entityName;
        }

        public string Source { get; }
        public string Target { get; }
        public string EntityName { get; }
    }

    public sealed class XmiImportPlan
    {
        public XmiImportPlan(
            IReadOnlyList<JToken> nodes,
            IReadOnlyDictionary<string, JToken> nodeIndex,
            IReadOnlyList<XmiGraphEdge> edges,
            XmiImportDiagnostics diagnostics,
            ISet<string> supportedEntities)
        {
            Nodes = nodes;
            NodeIndex = nodeIndex;
            Edges = edges;
            Diagnostics = diagnostics;
            SupportedEntities = supportedEntities;
        }

        public IReadOnlyList<JToken> Nodes { get; }
        public IReadOnlyDictionary<string, JToken> NodeIndex { get; }
        public IReadOnlyList<XmiGraphEdge> Edges { get; }
        public XmiImportDiagnostics Diagnostics { get; }
        public ISet<string> SupportedEntities { get; }
    }

    public static class XmiImportPlanner
    {
        public static XmiImportPlan BuildPlan(string json, IEnumerable<string> supportedEntities)
        {
            JObject root = JObject.Parse(json);

            JArray? nodesArray = root["nodes"] as JArray;
            JArray? edgesArray = root["edges"] as JArray;

            if (nodesArray == null || nodesArray.Count == 0)
            {
                throw new InvalidOperationException("No nodes found in the JSON file.");
            }

            HashSet<string> supportedSet = new HashSet<string>(
                supportedEntities ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            Dictionary<string, JToken> nodeIndex = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (JToken node in nodesArray)
            {
                string? id = node["Id"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    nodeIndex[id] = node;
                }
            }

            List<XmiGraphEdge> edges = new List<XmiGraphEdge>();
            if (edgesArray != null)
            {
                foreach (JToken edge in edgesArray)
                {
                    string? source = edge["Source"]?.Value<string>();
                    string? target = edge["Target"]?.Value<string>();
                    string? entityName = edge["EntityName"]?.Value<string>();

                    if (!string.IsNullOrWhiteSpace(source)
                        && !string.IsNullOrWhiteSpace(target)
                        && !string.IsNullOrWhiteSpace(entityName))
                    {
                        edges.Add(new XmiGraphEdge(source, target, entityName));
                    }
                }
            }

            XmiImportDiagnostics diagnostics = new XmiImportDiagnostics();

            return new XmiImportPlan(nodesArray.ToList(), nodeIndex, edges, diagnostics, supportedSet);
        }
    }

    public sealed class XmiImportResult
    {
        public XmiImportResult(XmiImportDiagnostics diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public XmiImportDiagnostics Diagnostics { get; }
        public int CreatedCount => Diagnostics.TotalCreated;
    }
}
