namespace Server.Balance
{
    internal sealed class BalanceSweepPoint
    {
        public string Value { get; set; } = string.Empty;

        public double Number { get; set; }

        public string Error { get; set; } = string.Empty;

        public float WinRate { get; set; }

        public float TurnsAverage { get; set; }

        public float HeroHealthOnWin { get; set; }

        public float CompletionRate { get; set; }

        public float StagesClearedAverage { get; set; }

        public float ExperienceLevelAverage { get; set; }
    }
}
