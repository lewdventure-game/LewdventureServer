using System.ComponentModel.DataAnnotations;

namespace Server.Api.Options
{
    internal sealed class GameConfigOptions
    {
        public const string SectionName = "GameConfig";

        public GameConfigSourceType Source { get; set; } = GameConfigSourceType.File;

        public string FilePath { get; set; } = string.Empty;

        public bool FailStartupIfUnavailable { get; set; }

        public string PinnedVersion { get; set; } = string.Empty;

        public string LocalCachePath { get; set; } = string.Empty;

        public string BootstrapFilePath { get; set; } = string.Empty;

        public GameConfigReloadMode ReloadMode { get; set; } = GameConfigReloadMode.Manual;

        [Range(5, 3600)]
        public int PollIntervalSeconds { get; set; } = 30;
    }
}
