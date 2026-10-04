namespace Server.Admin.Analytics
{
    public sealed class SvgLine
    {
        public string Name { get; set; } = string.Empty;

        public int ColorIndex { get; set; }

        public string Points { get; set; } = string.Empty;

        public List<SvgDot> Dots { get; set; } = new();

        public double Total { get; set; }
    }
}
