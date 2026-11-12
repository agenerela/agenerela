using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// The dev menu's command line: each command a name, a usage line and a handler that returns
    /// what happened. The menu's buttons and a session's <c>unity command script eval</c> both go
    /// through it, so anything a button does can be done from code.
    /// </summary>
    public sealed class DevCommands
    {
        private sealed class Command
        {
            public string Name, Usage, Help;
            public Func<string[], string> Run;
        }

        private readonly Dictionary<string, Command> commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

        /// <param name="handler">Given the words after the name; returns the result line. Throw <see cref="ArgumentException"/> for a usage error.</param>
        public DevCommands Add(string name, string usage, string help, Func<string[], string> handler)
        {
            commands[name] = new Command { Name = name, Usage = usage, Help = help, Run = handler };
            return this;
        }

        public IEnumerable<string> Names => commands.Keys.OrderBy(n => n, StringComparer.Ordinal);

        /// <summary>One line per command: its usage and what it does.</summary>
        public string Help() => string.Join("\n", commands.Values.OrderBy(c => c.Name, StringComparer.Ordinal).Select(c => $"{c.Usage}  —  {c.Help}"));

        /// <summary>Runs one line. An unknown command or a usage error comes back as text, never as an exception.</summary>
        public string Run(string line)
        {
            var words = (line ?? "").Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "Commands: " + string.Join(", ", Names);
            if (!commands.TryGetValue(words[0], out var c))
                return $"Unknown command '{words[0]}'. Commands: {string.Join(", ", Names)}";
            try { return c.Run(words.Skip(1).ToArray()) ?? "ok"; }
            catch (ArgumentException e) { return $"{e.Message} Usage: {c.Usage}"; }
            catch (FormatException) { return "Usage: " + c.Usage; }
        }
    }
}
