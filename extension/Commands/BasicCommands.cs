using System;
using Dynamo.Models;
using System.IO;
using System.Linq;
using System.Reflection;
using DynamoMcp.Bridge;
using Newtonsoft.Json.Linq;

namespace DynamoMcp.Commands
{
    internal sealed class GetStatusCommand : IBridgeCommand
    {
        private readonly DynamoContext _ctx;
        private readonly Func<CommandRegistry> _registry;
        private readonly int _port;
        public GetStatusCommand(DynamoContext ctx, int port, Func<CommandRegistry> registry) { _ctx = ctx; _port = port; _registry = registry; }
        public string Name => "get_status";

        public JToken Execute(JObject p) => _ctx.OnUi(() => new JObject
        {
            ["extensionVersion"] = Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
            ["dynamoVersion"] = DynamoModel.Version,
            ["hostVersion"] = _ctx.Model.HostVersion,
            ["port"] = _port,
            ["logFile"] = Log.Path,
            ["currentWorkspace"] = Serializer.Workspace(_ctx.Current, includeNodes: false, includeConnectors: false),
            ["commands"] = new JArray(_registry().Names.OrderBy(n => n)),
        });
    }

    internal sealed class GetWorkspaceCommand : IBridgeCommand
    {
        private readonly DynamoContext _ctx;
        public GetWorkspaceCommand(DynamoContext ctx) { _ctx = ctx; }
        public string Name => "get_workspace";

        public JToken Execute(JObject p) => _ctx.OnUi(() => Serializer.Workspace(
            _ctx.Current,
            includeNodes: p.Value<bool?>("includeNodes") ?? true,
            includeConnectors: p.Value<bool?>("includeConnectors") ?? true));
    }

    internal sealed class GetNodeCommand : IBridgeCommand
    {
        private readonly DynamoContext _ctx;
        public GetNodeCommand(DynamoContext ctx) { _ctx = ctx; }
        public string Name => "get_node";

        public JToken Execute(JObject p) => _ctx.OnUi(() => Serializer.NodeDetail(
            NodeLookup.Find(_ctx.Current, p),
            depth: p.Value<int?>("depth") ?? 2,
            maxItems: p.Value<int?>("maxItems") ?? 20));
    }

    internal sealed class OpenGraphCommand : IBridgeCommand
    {
        private readonly DynamoContext _ctx;
        public OpenGraphCommand(DynamoContext ctx) { _ctx = ctx; }
        public string Name => "open_graph";

        public JToken Execute(JObject p)
        {
            var path = (string)p["path"];
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("'path' is required");
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) throw new ArgumentException($"File not found: {path}");
            var forceManual = p.Value<bool?>("forceManualRun") ?? true;

            return _ctx.OnUi(() =>
            {
                _ctx.Model.OpenFileFromPath(path, forceManual);
                return Serializer.Workspace(_ctx.Current, includeNodes: true, includeConnectors: false);
            });
        }
    }

    /// <summary>
    /// Sets the value of an input node (Number, String, Boolean, sliders, Code Block, dropdowns) without
    /// referencing CoreNodeModels: it writes the first settable property among Value / Code / SelectedIndex.
    /// </summary>
    internal sealed class SetInputValueCommand : IBridgeCommand
    {
        private static readonly string[] Candidates = { "Value", "Code", "SelectedIndex" };
        private readonly DynamoContext _ctx;
        public SetInputValueCommand(DynamoContext ctx) { _ctx = ctx; }
        public string Name => "set_input_value";

        public JToken Execute(JObject p)
        {
            var value = p["value"] ?? throw new ArgumentException("'value' is required");
            return _ctx.OnUi(() =>
            {
                var node = NodeLookup.Find(_ctx.Current, p);
                var prop = Candidates
                    .Select(c => node.GetType().GetProperty(c, BindingFlags.Public | BindingFlags.Instance))
                    .FirstOrDefault(pi => pi != null && pi.CanWrite);
                if (prop == null)
                    throw new ArgumentException($"Node '{node.Name}' ({node.GetType().Name}) has no settable Value/Code/SelectedIndex property");

                object converted;
                try { converted = value.ToObject(prop.PropertyType); }
                catch (Exception ex) { throw new ArgumentException($"Cannot convert {value} to {prop.PropertyType.Name}: {ex.Message}"); }

                prop.SetValue(node, converted);
                var result = Serializer.NodeDetail(node, depth: 1, maxItems: 10);
                result["propertySet"] = prop.Name;
                return result;
            });
        }
    }
}
