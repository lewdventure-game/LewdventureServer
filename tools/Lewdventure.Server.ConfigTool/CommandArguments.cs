namespace Server.ConfigTool
{
    internal sealed class CommandArguments
    {
        private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);

        public CommandArguments(string[] args)
        {
            Command = 0 < args.Length ? args[0] : string.Empty;

            for (int i = 1; i < args.Length; i++)
            {
                var argument = args[i];

                if (argument.StartsWith("--", StringComparison.Ordinal) == false)
                    continue;

                var name = argument.Substring(2);
                var hasValue = i + 1 < args.Length && args[i + 1].StartsWith("--", StringComparison.Ordinal) == false;

                _options[name] = hasValue ? args[i + 1] : "true";

                if (hasValue)
                    i++;
            }
        }

        public string Command { get; }

        public bool TryGet(string name, out string value)
        {
            return _options.TryGetValue(name, out value!);
        }

        public string GetRequired(string name)
        {
            if (_options.TryGetValue(name, out var value) == false || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Option --{name} is required.");

            return value;
        }
    }
}
