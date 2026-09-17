namespace Server.LoadTest
{
    internal sealed class LoadTestSettings
    {
        public string Target { get; set; } = "http://localhost:5000";

        public string CasesPath { get; set; } = string.Empty;

        public string Endpoint { get; set; } = "replay";

        public int Concurrency { get; set; } = 16;

        public int DurationSeconds { get; set; } = 30;

        public int WarmupSeconds { get; set; } = 3;

        public int RequestsPerSecond { get; set; }

        public double MaxErrorRate { get; set; } = 0.01;

        public double MaxP95Milliseconds { get; set; }

        public bool Verify { get; set; } = true;
    }
}
