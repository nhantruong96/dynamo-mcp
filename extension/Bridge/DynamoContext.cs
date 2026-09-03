using System;
using System.Threading;
using System.Windows.Threading;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;
using Dynamo.ViewModels;
using Dynamo.Wpf.Extensions;

namespace DynamoMcp.Bridge
{
    /// <summary>Everything a command needs to reach Dynamo, plus the UI-thread marshalling.</summary>
    internal sealed class DynamoContext
    {
        /// <summary>Upper bound for a single UI-thread hop, so a modal dialog in Revit cannot hang the bridge forever.</summary>
        public static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(60);

        public ViewLoadedParams LoadedParams { get; }
        public DynamoViewModel ViewModel { get; }
        public Dispatcher Dispatcher { get; }

        public DynamoModel Model => ViewModel.Model;
        public HomeWorkspaceModel Home => ViewModel.HomeSpace;
        public WorkspaceModel Current => ViewModel.CurrentSpace;

        public DynamoContext(ViewLoadedParams p)
        {
            LoadedParams = p;
            ViewModel = p.DynamoWindow.DataContext as DynamoViewModel
                ?? throw new InvalidOperationException("DynamoWindow.DataContext is not a DynamoViewModel");
            Dispatcher = p.DynamoWindow.Dispatcher;
        }

        public T OnUi<T>(Func<T> func)
        {
            if (Dispatcher.CheckAccess()) return func();
            return Dispatcher.Invoke(func, DispatcherPriority.Normal, CancellationToken.None, UiTimeout);
        }

        public void OnUi(Action action)
        {
            OnUi<object>(() => { action(); return null; });
        }
    }
}
