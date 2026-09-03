using System;
using System.Windows.Controls;
using Dynamo.Models;
using Dynamo.Wpf.Extensions;
using DynamoMcp.Bridge;
using DynamoMcp.Commands;

namespace DynamoMcp
{
    /// <summary>
    /// Dynamo view extension that exposes the live Dynamo session to the dynamo-mcp server over a
    /// loopback TCP socket. Loaded by Dynamo from a *_ViewExtensionDefinition.xml file.
    /// </summary>
    public sealed class DynamoMcpViewExtension : IViewExtension
    {
        public const int DefaultPort = 8555;

        private SocketServer _server;
        private MenuItem _menuItem;
        private DynamoModel _model;

        public string UniqueId => "b7c3e1f0-5d2a-4f8e-9c1b-2a6d4e8f0a11";
        public string Name => "Dynamo MCP";

        public void Startup(ViewStartupParams p) { }

        public void Loaded(ViewLoadedParams p)
        {
            var port = ResolvePort();
            try
            {
                var ctx = new DynamoContext(p);
                CommandRegistry registry = null;
                registry = new CommandRegistry()
                    .Register(new GetStatusCommand(ctx, port, () => registry))
                    .Register(new GetWorkspaceCommand(ctx))
                    .Register(new GetNodeCommand(ctx))
                    .Register(new OpenGraphCommand(ctx))
                    .Register(new RunGraphCommand(ctx))
                    .Register(new SetInputValueCommand(ctx));
                _server = new SocketServer(port, registry);

                // Closing the Dynamo window calls Shutdown(), but closing Revit with Dynamo still open
                // does not, so also stop the listener when the model shuts down or the process exits.
                _model = ctx.Model;
                _model.ShutdownStarted += OnModelShutdownStarted;
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

                _menuItem = new MenuItem { IsCheckable = true };
                _menuItem.Click += (s, e) => Toggle();
                p.AddExtensionMenuItem(_menuItem);

                _server.Start();
                Log.Write($"Loaded in Dynamo {Dynamo.Models.DynamoModel.Version} (host {ctx.Model.HostVersion})");
            }
            catch (Exception ex)
            {
                Log.Write("Failed to start: " + ex);
            }
            RefreshMenu();
        }

        private void Toggle()
        {
            try
            {
                if (_server.IsRunning) _server.Stop(); else _server.Start();
            }
            catch (Exception ex)
            {
                Log.Write("Toggle failed: " + ex);
            }
            RefreshMenu();
        }

        private void RefreshMenu()
        {
            if (_menuItem == null) return;
            var running = _server?.IsRunning == true;
            _menuItem.IsChecked = running;
            _menuItem.Header = running
                ? $"Dynamo MCP: ON (127.0.0.1:{_server.Port})"
                : "Dynamo MCP: OFF - click to start";
        }

        private static int ResolvePort()
        {
            var env = Environment.GetEnvironmentVariable("DYNAMO_MCP_PORT");
            return int.TryParse(env, out var port) && port > 0 && port < 65536 ? port : DefaultPort;
        }

        private void OnModelShutdownStarted(DynamoModel model) => Shutdown();

        private void OnProcessExit(object sender, EventArgs e) => Shutdown();

        public void Shutdown() => _server?.Stop();

        public void Dispose()
        {
            Shutdown();
            if (_model != null) _model.ShutdownStarted -= OnModelShutdownStarted;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
    }
}
