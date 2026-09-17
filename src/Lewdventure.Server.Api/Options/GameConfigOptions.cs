namespace Server.Api.Options
{
    internal sealed class GameConfigOptions
    {
        public const string SectionName = "GameConfig";

        public GameConfigSourceType Source { get; set; } = GameConfigSourceType.GoogleSheets;

        public string FilePath { get; set; } = string.Empty;

        public bool FailStartupIfUnavailable { get; set; }
    }
}
