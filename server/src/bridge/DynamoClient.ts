import * as net from "node:net";

export const HOST = "127.0.0.1";
export const PORT = Number(process.env.DYNAMO_MCP_PORT ?? 8555);

export class DynamoBridgeError extends Error {
  constructor(message: string, public readonly code?: number) {
    super(message);
    this.name = "DynamoBridgeError";
  }
}

let sequence = 0;

/**
 * Sends one JSON-RPC 2.0 request to the DynamoMCP extension over a fresh TCP connection
 * (newline-delimited) and resolves with its `result`.
 */
export function callDynamo<T = unknown>(
  method: string,
  params: Record<string, unknown> = {},
  timeoutMs = 120_000
): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const id = `${++sequence}-${Date.now()}`;
    const socket = new net.Socket();
    let buffer = "";
    let settled = false;

    const finish = (fn: () => void) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      socket.destroy();
      fn();
    };

    const timer = setTimeout(
      () => finish(() => reject(new DynamoBridgeError(`Dynamo did not answer '${method}' within ${timeoutMs / 1000}s`))),
      timeoutMs
    );

    socket.setNoDelay(true);
    socket.once("error", (err: NodeJS.ErrnoException) =>
      finish(() =>
        reject(
          new DynamoBridgeError(
            err.code === "ECONNREFUSED"
              ? `Cannot reach the DynamoMCP extension on ${HOST}:${PORT}. Is Dynamo open with the extension switched on (Extensions menu > "Dynamo MCP")?`
              : `Socket error: ${err.message}`
          )
        )
      )
    );
    socket.once("close", () =>
      finish(() => reject(new DynamoBridgeError("Connection closed by Dynamo before a response arrived")))
    );
    socket.on("data", (chunk) => {
      buffer += chunk.toString("utf8");
      let newline: number;
      while ((newline = buffer.indexOf("\n")) >= 0) {
        const line = buffer.slice(0, newline).trim();
        buffer = buffer.slice(newline + 1);
        if (!line) continue;
        let message: { id?: string; result?: T; error?: { code?: number; message?: string } };
        try {
          message = JSON.parse(line);
        } catch {
          continue;
        }
        if (message.id !== id) continue;
        finish(() => {
          if (message.error) {
            reject(new DynamoBridgeError(message.error.message ?? "Unknown error from Dynamo", message.error.code));
          } else {
            resolve(message.result as T);
          }
        });
      }
    });

    socket.connect(PORT, HOST, () => {
      socket.write(JSON.stringify({ jsonrpc: "2.0", id, method, params }) + "\n");
    });
  });
}
