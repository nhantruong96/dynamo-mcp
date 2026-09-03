using System;
using System.Linq;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Workspaces;
using Newtonsoft.Json.Linq;

namespace DynamoMcp.Commands
{
    internal static class NodeLookup
    {
        /// <summary>Finds a node by "nodeId" (GUID) or, failing that, by exact then case-insensitive "name".</summary>
        public static NodeModel Find(WorkspaceModel ws, JObject p)
        {
            var id = (string)p["nodeId"];
            var name = (string)p["name"];
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name))
                throw new ArgumentException("Provide 'nodeId' or 'name'");

            if (!string.IsNullOrEmpty(id))
            {
                if (!Guid.TryParse(id, out var guid)) throw new ArgumentException($"'{id}' is not a GUID");
                return ws.Nodes.FirstOrDefault(n => n.GUID == guid)
                    ?? throw new ArgumentException($"No node with id {id} in workspace '{ws.Name}'");
            }

            var matches = ws.Nodes.Where(n => n.Name == name).ToList();
            if (matches.Count == 0)
                matches = ws.Nodes.Where(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0) throw new ArgumentException($"No node named '{name}' in workspace '{ws.Name}'");
            if (matches.Count > 1)
                throw new ArgumentException($"{matches.Count} nodes are named '{name}'; use nodeId: " +
                                            string.Join(", ", matches.Select(n => n.GUID)));
            return matches[0];
        }
    }
}
