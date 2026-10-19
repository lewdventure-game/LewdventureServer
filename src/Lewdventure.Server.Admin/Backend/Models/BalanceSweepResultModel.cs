namespace Server.Admin.Backend.Models
{
    public sealed class BalanceSweepResultModel
    {
        public string Kind { get; set; } = string.Empty;

        public BalanceSweepAxisModel Axis { get; set; } = new();

        public List<BalanceSweepPointModel> Points { get; set; } = new();

        public string RunId { get; set; } = string.Empty;
    }
}
