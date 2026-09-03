using System;
using System.Collections.Generic;
using System.Linq;
using Dynamo.Graph.Connectors;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Workspaces;
using Newtonsoft.Json.Linq;
using ProtoCore.Mirror;

namespace DynamoMcp.Commands
{
    /// <summary>Turns Dynamo model objects into plain JSON. Must be called on the UI thread.</summary>
    internal static class Serializer
    {
        public static JObject Workspace(WorkspaceModel ws, bool includeNodes, bool includeConnectors)
        {
            var home = ws as HomeWorkspaceModel;
            var o = new JObject
            {
                ["id"] = ws.Guid.ToString(),
                ["name"] = ws.Name,
                ["fileName"] = ws.FileName,
                ["isHomeWorkspace"] = home != null,
                ["hasUnsavedChanges"] = ws.HasUnsavedChanges,
                ["runType"] = home?.RunSettings?.RunType.ToString(),
                ["evaluationCount"] = home?.EvaluationCount,
                ["nodeCount"] = ws.Nodes.Count(),
                ["connectorCount"] = ws.Connectors.Count(),
                ["description"] = ws.Description,
                ["customNodeDependencies"] = new JArray(ws.Dependencies.Select(g => g.ToString())),
            };
            if (includeNodes)
                o["nodes"] = new JArray(ws.Nodes.Select(NodeSummary));
            if (includeConnectors)
                o["connectors"] = new JArray(ws.Connectors.Select(Connector));
            return o;
        }

        public static JObject NodeSummary(NodeModel n)
        {
            return new JObject
            {
                ["id"] = n.GUID.ToString(),
                ["name"] = n.Name,
                ["creationName"] = n.CreationName,
                ["nodeType"] = n.NodeType,
                ["category"] = n.Category,
                ["state"] = n.State.ToString(),
                ["isSetAsInput"] = n.IsSetAsInput,
                ["isSetAsOutput"] = n.IsSetAsOutput,
                ["isFrozen"] = n.IsFrozen,
                ["messages"] = Messages(n),
                ["position"] = Position(n),
            };
        }

        public static JObject NodeDetail(NodeModel n, int depth, int maxItems)
        {
            var o = NodeSummary(n);
            o["description"] = n.Description;
            o["inPorts"] = new JArray(n.InPorts.Select(Port));
            o["outPorts"] = new JArray(n.OutPorts.Select(Port));
            if (n.InputData != null)
            {
                o["inputData"] = new JObject
                {
                    ["type"] = n.InputData.Type2.ToString(),
                    ["value"] = n.InputData.Value,
                    ["choices"] = n.InputData.Choices == null ? null : new JArray(n.InputData.Choices),
                    ["min"] = n.InputData.MinimumValue,
                    ["max"] = n.InputData.MaximumValue,
                };
            }
            var code = n.GetType().GetProperty("Code")?.GetValue(n) as string;
            if (code != null) o["code"] = code;
            o["cachedValue"] = Value(n.CachedValue, depth, maxItems);
            return o;
        }

#pragma warning disable CS0618 // ModelBase.X/Y are the only model-side coordinates available to an extension.
        private static JArray Position(NodeModel n) => new JArray(Math.Round(n.X, 1), Math.Round(n.Y, 1));
#pragma warning restore CS0618

        private static JArray Messages(NodeModel n)
        {
            var infos = n.NodeInfos ?? new List<Info>();
            return new JArray(infos.Select(i => new JObject { ["state"] = i.State.ToString(), ["message"] = i.Message }));
        }

        private static JObject Port(PortModel p)
        {
            return new JObject
            {
                ["index"] = p.Index,
                ["name"] = p.Name,
                ["isConnected"] = p.IsConnected,
                ["usingDefaultValue"] = p.UsingDefaultValue,
                ["connectedTo"] = new JArray(p.Connectors.Select(c =>
                {
                    var other = p.PortType == PortType.Input ? c.Start : c.End;
                    return new JObject { ["nodeId"] = other.Owner.GUID.ToString(), ["nodeName"] = other.Owner.Name, ["port"] = other.Index };
                })),
            };
        }

        private static JObject Connector(ConnectorModel c)
        {
            return new JObject
            {
                ["id"] = c.GUID.ToString(),
                ["from"] = new JObject { ["nodeId"] = c.Start.Owner.GUID.ToString(), ["port"] = c.Start.Index },
                ["to"] = new JObject { ["nodeId"] = c.End.Owner.GUID.ToString(), ["port"] = c.End.Index },
            };
        }

        /// <summary>Depth- and size-limited view of a node's cached value.</summary>
        public static JToken Value(MirrorData d, int depth, int maxItems)
        {
            if (d == null || d.IsNull) return JValue.CreateNull();
            if (d.IsCollection)
            {
                if (depth <= 0) return new JValue("[...]");
                var items = d.GetElements().ToList();
                var arr = new JArray(items.Take(maxItems).Select(e => Value(e, depth - 1, maxItems)));
                if (items.Count > maxItems) arr.Add(new JValue($"... {items.Count - maxItems} more"));
                return arr;
            }
            var data = d.Data;
            switch (data)
            {
                case null: return new JValue(d.StringData);
                case string s: return new JValue(s);
                case bool b: return new JValue(b);
                case int i: return new JValue(i);
                case long l: return new JValue(l);
                case double x: return new JValue(x);
                case float f: return new JValue(f);
                default: return new JValue(data.ToString());
            }
        }
    }
}
