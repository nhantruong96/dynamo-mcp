#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools } from "./tools/index.js";
import { HOST, PORT } from "./bridge/DynamoClient.js";

const server = new McpServer(
  { name: "dynamo-mcp", version: "0.1.0" },
  {
    instructions:
      "Tools for working with the Dynamo graph that is currently open in Autodesk Dynamo (Revit). " +
      "Requires Dynamo to be running with the DynamoMCP view extension switched on. " +
      "Call dynamo_status first to confirm the connection and see the current workspace. " +
      "run_graph executes whatever graph is loaded, including Revit write operations - confirm with the user before running unfamiliar graphs.",
  }
);

async function main() {
  registerTools(server);
  await server.connect(new StdioServerTransport());
  console.error(`dynamo-mcp started (bridge ${HOST}:${PORT})`);
}

main().catch((error) => {
  console.error("dynamo-mcp failed to start:", error);
  process.exit(1);
});
