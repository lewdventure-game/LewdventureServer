using System.Globalization;

namespace Server.LoadTest
{
    internal sealed class LoadTestArgumentsParser
    {
        public LoadTestSettings Parse(string[] args)
        {
            var settings = new LoadTestSettings();

            for (int i = 0; i < args.Length; i++)
            {
                var name = args[i];

                if (name == "--no-verify")
                {
                    settings.Verify = false;

                    continue;
                }

                if (args.Length <= i + 1)
                    throw new ArgumentException($"Option {name} requires a value.");

                var value = args[++i];

                switch (name)
                {
                    case "--target":
                        settings.Target = value.TrimEnd('/');
                        break;
                    case "--cases":
                        settings.CasesPath = value;
                        break;
                    case "--endpoint":
                        settings.Endpoint = ParseEndpoint(value);
                        break;
                    case "--concurrency":
                        settings.Concurrency = ParsePositive(name, value);
                        break;
                    case "--duration":
                        settings.DurationSeconds = ParsePositive(name, value);
                        break;
                    case "--warmup":
                        settings.WarmupSeconds = ParseNonNegative(name, value);
                        break;
                    case "--rps":
                        settings.RequestsPerSecond = ParseNonNegative(name, value);
                        break;
                    case "--max-error-rate":
                        settings.MaxErrorRate = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--max-p95-ms":
                        settings.MaxP95Milliseconds = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    default:
                        throw new ArgumentException($"Unknown option {name}.");
                }
            }

            if (settings.Endpoint == "simulate")
                settings.Verify = false;

            return settings;
        }

        private string ParseEndpoint(string value)
        {
            if (value == "replay" || value == "simulate")
                return value;

            throw new ArgumentException("Option --endpoint must be replay or simulate.");
        }

        private int ParsePositive(string name, string value)
        {
            var result = int.Parse(value, CultureInfo.InvariantCulture);

            if (result <= 0)
                throw new ArgumentException($"Option {name} must be positive.");

            return result;
        }

        private int ParseNonNegative(string name, string value)
        {
            var result = int.Parse(value, CultureInfo.InvariantCulture);

            if (result < 0)
                throw new ArgumentException($"Option {name} must not be negative.");

            return result;
        }
    }
}
