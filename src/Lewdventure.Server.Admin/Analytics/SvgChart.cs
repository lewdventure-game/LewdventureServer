namespace Server.Admin.Analytics
{
    public sealed class SvgChart
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public List<SvgLine> Lines { get; set; } = new();

        public List<SvgTick> YTicks { get; set; } = new();

        public List<SvgTick> XTicks { get; set; } = new();

        public bool IsEmpty { get; set; }
    }
}
