using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DynamoMcp.Commands
{
    internal interface IBridgeCommand
    {
        string Name { get; }
        JToken Execute(JObject p);
    }

    internal sealed class CommandNotFoundException : Exception
    {
        public CommandNotFoundException(string name) : base($"Method '{name}' not found") { }
    }

    internal sealed class CommandRegistry
    {
        private readonly Dictionary<string, IBridgeCommand> _commands =
            new Dictionary<string, IBridgeCommand>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> Names => _commands.Keys;

        public CommandRegistry Register(IBridgeCommand command)
        {
            _commands[command.Name] = command;
            return this;
        }

        public JToken Execute(string name, JObject p)
        {
            if (string.IsNullOrEmpty(name) || !_commands.TryGetValue(name, out var command))
                throw new CommandNotFoundException(name);
            return command.Execute(p ?? new JObject());
        }
    }
}
