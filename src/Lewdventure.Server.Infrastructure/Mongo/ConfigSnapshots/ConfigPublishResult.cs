using Server.GameConfigs;

namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigPublishResult
    {
        public bool Succeeded { get; set; }

        public string Version { get; set; } = string.Empty;

        public string PreviousVersion { get; set; } = string.Empty;

        public bool Stored { get; set; }

        public bool Activated { get; set; }

        public List<string> Errors { get; } = new();

        public List<string> Warnings { get; } = new();

        public List<ConfigDomainDiff> Changes { get; } = new();
    }
}
