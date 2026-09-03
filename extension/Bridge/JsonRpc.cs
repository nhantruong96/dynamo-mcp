using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamoMcp.Bridge
{
    internal sealed class RpcRequest
    {
        public string Id;
        public string Method;
        public JObject Params;

        public static RpcRequest Parse(string line)
        {
            var o = JObject.Parse(line);
            return new RpcRequest
            {
                Id = o["id"]?.ToString(),
                Method = (string)o["method"],
                Params = o["params"] as JObject ?? new JObject(),
            };
        }
    }

    internal static class RpcResponse
    {
        public const int ParseError = -32700;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int ServerError = -32000;

        public static string Ok(string id, JToken result)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = result ?? JValue.CreateNull(),
            }.ToString(Formatting.None);
        }

        public static string Fail(string id, int code, string message)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JObject { ["code"] = code, ["message"] = message },
            }.ToString(Formatting.None);
        }
    }
}
