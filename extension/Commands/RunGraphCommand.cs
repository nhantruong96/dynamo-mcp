using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dynamo.Graph.Nodes;
using Dynamo.Models;
using DynamoMcp.Bridge;
using Newtonsoft.Json.Linq;

namespace DynamoMcp.Commands
{
    /// <summary>
    /// Runs the home workspace and blocks (on the socket thread, never the UI thread) until Dynamo raises
    /// EvaluationCompleted or the timeout elapses. Returns evaluation status, every node in a warning or
    /// error state, and the cached values of nodes marked "Is Output".
    /// </summary>
    internal sealed class RunGraphCommand : IBridgeCommand
    {
        private static readonly ElementState[] IssueStates =
            { ElementState.Warning, ElementState.PersistentWarning, ElementState.Error, ElementState.AstBuildBroken };

        private readonly DynamoContext _ctx;
        public RunGraphCommand(DynamoContext ctx) { _ctx = ctx; }
        public string Name => "run_graph";

        public JToken Execute(JObject p)
        {
            var force = p.Value<bool?>("force") ?? false;
            var timeoutSeconds = p.Value<int?>("timeoutSeconds") ?? 300;
            var home = _ctx.Home ?? throw new InvalidOperationException("No home workspace is open");

            var tcs = new TaskCompletionSource<EvaluationCompletedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<EvaluationCompletedEventArgs> handler = (s, e) => tcs.TrySetResult(e);
            home.EvaluationCompleted += handler;
            var sw = Stopwatch.StartNew();
            try
            {
                _ctx.OnUi(() =>
                {
                    if (force) _ctx.Model.ForceRun();
                    else _ctx.ViewModel.ExecuteCommand(new DynamoModel.RunCancelCommand(showErrors: false, cancelRun: false));
                });

                var completed = tcs.Task.Wait(TimeSpan.FromSeconds(timeoutSeconds));
                var result = new JObject { ["completed"] = completed, ["elapsedMs"] = sw.ElapsedMilliseconds };
                if (completed)
                {
                    var e = tcs.Task.Result;
                    result["evaluationTookPlace"] = e.EvaluationTookPlace;
                    result["succeeded"] = e.EvaluationSucceeded;
                    // EvaluationCompletedEventArgs.Error throws when the evaluation succeeded.
                    result["error"] = e.EvaluationSucceeded ? null : e.Error?.Message;
                }
                else
                {
                    result["error"] = $"EvaluationCompleted was not raised within {timeoutSeconds}s; the graph may still be running";
                }

                _ctx.OnUi(() =>
                {
                    var nodes = home.Nodes.ToList();
                    result["nodeCount"] = nodes.Count;
                    result["nodesWithIssues"] = new JArray(nodes.Where(n => IssueStates.Contains(n.State)).Select(Serializer.NodeSummary));
                    result["outputs"] = new JArray(nodes.Where(n => n.IsSetAsOutput).Select(n => Serializer.NodeDetail(n, 2, 20)));
                });
                return result;
            }
            finally
            {
                home.EvaluationCompleted -= handler;
            }
        }
    }
}
