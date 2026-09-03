using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using DynamoMcp.Commands;

namespace DynamoMcp.Bridge
{
    /// <summary>
    /// Loopback-only TCP server speaking newline-delimited JSON-RPC 2.0.
    /// One request per line, one response per line. Each client gets its own thread;
    /// all Dynamo access is marshalled to the UI thread by the commands themselves.
    /// </summary>
    internal sealed class SocketServer
    {
        private readonly CommandRegistry _registry;
        private TcpListener _listener;
        private volatile bool _running;

        public int Port { get; }
        public bool IsRunning => _running;

        public SocketServer(int port, CommandRegistry registry)
        {
            Port = port;
            _registry = registry;
        }

        public void Start()
        {
            if (_running) return;
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            _running = true;
            new Thread(AcceptLoop) { IsBackground = true, Name = "DynamoMcp.Accept" }.Start();
            Log.Write($"Listening on 127.0.0.1:{Port}");
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _listener?.Stop(); } catch { /* shutting down */ }
            _listener = null;
            Log.Write("Stopped");
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; } // listener stopped
                new Thread(() => Serve(client)) { IsBackground = true, Name = "DynamoMcp.Client" }.Start();
            }
        }

        private void Serve(TcpClient client)
        {
            var utf8 = new UTF8Encoding(false);
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, utf8))
                using (var writer = new StreamWriter(stream, utf8) { AutoFlush = true, NewLine = "\n" })
                {
                    string line;
                    while (_running && (line = reader.ReadLine()) != null)
                    {
                        if (line.Trim().Length == 0) continue;
                        writer.WriteLine(Handle(line));
                    }
                }
            }
            catch (IOException) { /* client went away */ }
            catch (Exception ex) { Log.Write("Client error: " + ex); }
        }

        private string Handle(string line)
        {
            RpcRequest req;
            try { req = RpcRequest.Parse(line); }
            catch (Exception ex) { return RpcResponse.Fail(null, RpcResponse.ParseError, "Parse error: " + ex.Message); }

            Log.Write($"-> {req.Method}");
            try
            {
                var result = _registry.Execute(req.Method, req.Params);
                return RpcResponse.Ok(req.Id, result);
            }
            catch (CommandNotFoundException ex)
            {
                return RpcResponse.Fail(req.Id, RpcResponse.MethodNotFound, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return RpcResponse.Fail(req.Id, RpcResponse.InvalidParams, ex.Message);
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException();
                Log.Write($"{req.Method} failed: {root}");
                return RpcResponse.Fail(req.Id, RpcResponse.ServerError, $"{root.GetType().Name}: {root.Message}");
            }
        }
    }
}
