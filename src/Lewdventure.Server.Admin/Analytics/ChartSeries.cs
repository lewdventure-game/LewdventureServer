namespace Server.Admin.Analytics
{
    public sealed class ChartSeries
    {
        public string Name { get; set; } = string.Empty;

        public List<ChartPoint> Points { get; } = new();
    }
}
