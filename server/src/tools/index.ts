import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { callDynamo } from "../bridge/DynamoClient.js";

type ToolResult = { content: { type: "text"; text: string }[]; isError?: boolean };

function text(value: unknown): ToolResult {
  return { content: [{ type: "text", text: typeof value === "string" ? value : JSON.stringify(value, null, 2) }] };
}

/** Forwards a tool call to the extension and formats the outcome, never throwing into the MCP layer. */
async function bridge(method: string, params: Record<string, unknown> = {}, timeoutMs?: number): Promise<ToolResult> {
  try {
    return text(await callDynamo(method, params, timeoutMs));
  } catch (error) {
    return { ...text(`${method} failed: ${error instanceof Error ? error.message : String(error)}`), isError: true };
  }
}

const nodeRef = {
  nodeId: z.string().optional().describe("Node GUID (from get_workspace). Preferred when names are not unique."),
  name: z.string().optional().describe("Node name as shown on the canvas. Used when nodeId is omitted."),
};

export function registerTools(server: McpServer) {
  server.registerTool(
    "dynamo_status",
    {
      description:
        "Check the connection to Dynamo and return Dynamo/host versions, the current workspace summary and the bridge commands available.",
      inputSchema: {},
    },
    async () => bridge("get_status")
  );

  server.registerTool(
    "get_workspace",
    {
      description:
        "Return the graph currently open in Dynamo: workspace metadata, every node (id, name, category, state, warnings, input/output flags) and optionally the connectors between them.",
      inputSchema: {
        includeNodes: z.boolean().optional().describe("Include the node list (default true)."),
        includeConnectors: z.boolean().optional().describe("Include connectors (default true)."),
      },
    },
    async (args) => bridge("get_workspace", args)
  );

  server.registerTool(
    "get_node",
    {
      description:
        "Return one node in detail: ports and their connections, input configuration, code (for Code Block nodes), messages and the cached value from the last run.",
      inputSchema: {
        ...nodeRef,
        depth: z.number().int().min(0).max(5).optional().describe("How deep nested lists are expanded (default 2)."),
        maxItems: z.number().int().min(1).max(500).optional().describe("Max items returned per list level (default 20)."),
      },
    },
    async (args) => bridge("get_node", args)
  );

  server.registerTool(
    "open_graph",
    {
      description:
        "Open a .dyn file in Dynamo, replacing the current workspace, and return its node list. Opens in manual run mode by default so nothing executes until run_graph is called.",
      inputSchema: {
        path: z.string().describe("Absolute path to the .dyn file on the machine running Dynamo."),
        forceManualRun: z.boolean().optional().describe("Force manual run mode (default true)."),
      },
    },
    async (args) => bridge("open_graph", args)
  );

  server.registerTool(
    "set_input_value",
    {
      description:
        "Set the value of an input node (Number, String, Boolean, Integer/Number Slider, Code Block, dropdown). The graph is not run automatically in manual mode; call run_graph afterwards.",
      inputSchema: {
        ...nodeRef,
        value: z.union([z.string(), z.number(), z.boolean()]).describe("New value. Code Block nodes take DesignScript source, dropdowns take the selected index."),
      },
    },
    async (args) => bridge("set_input_value", args)
  );

  server.registerTool(
    "run_graph",
    {
      description:
        "Run the current graph and wait for Dynamo to finish. Returns whether evaluation succeeded, all nodes in a warning/error state with their messages, and the values of nodes marked 'Is Output'.",
      inputSchema: {
        force: z.boolean().optional().describe("Re-execute every node even if nothing changed (default false)."),
        timeoutSeconds: z.number().int().min(1).max(3600).optional().describe("How long to wait for completion (default 300)."),
      },
    },
    async (args) => bridge("run_graph", args, ((args.timeoutSeconds ?? 300) + 30) * 1000)
  );
}
