using System.Globalization;
using System.Text;

namespace Server.Admin.Analytics
{
    internal sealed class SvgChartBuilder
    {
        public const int Width = 720;
        public const int Height = 240;
        public const int PaddingLeft = 48;
        public const int PaddingRight = 12;
        public const int PaddingTop = 12;
        public const int PaddingBottom = 28;
        public const int YTickCount = 4;
        public const int MaxXTicks = 8;

        public SvgChart Build(List<ChartSeries> series, bool isHourly)
        {
            var chart = new SvgChart { Width = Width, Height = Height };
            var times = CollectTimes(series);

            if (times.Count == 0)
            {
                chart.IsEmpty = true;

                return chart;
            }

            var maxValue = FindMax(series);
            var plotWidth = Width - PaddingLeft - PaddingRight;
            var plotHeight = Height - PaddingTop - PaddingBottom;

            for (int i = 0; i < series.Count; i++)
                chart.Lines.Add(BuildLine(series[i], i, times, maxValue, plotWidth, plotHeight, isHourly));

            for (int i = 0; i <= YTickCount; i++)
            {
                var value = maxValue * i / YTickCount;

                chart.YTicks.Add(new SvgTick { Position = PaddingTop + plotHeight - plotHeight * i / (double)YTickCount, Label = FormatValue(value) });
            }

            var step = Math.Max(1, (int)Math.Ceiling(times.Count / (double)MaxXTicks));

            for (int i = 0; i < times.Count; i += step)
                chart.XTicks.Add(new SvgTick { Position = XFor(i, times.Count, plotWidth), Label = FormatTime(times[i], isHourly) });

            return chart;
        }

        private SvgLine BuildLine(ChartSeries series, int index, List<DateTime> times, double maxValue, double plotWidth, double plotHeight, bool isHourly)
        {
            var line = new SvgLine { Name = series.Name, ColorIndex = index % AnalyticsReportService.MaxSeries };
            var points = new StringBuilder();

            for (int i = 0; i < times.Count; i++)
            {
                var value = ValueAt(series, times[i]);
                var x = XFor(i, times.Count, plotWidth);
                var y = PaddingTop + plotHeight - (maxValue <= 0d ? 0d : plotHeight * value / maxValue);

                line.Total += value;
                points.Append(x.ToString("0.#", CultureInfo.InvariantCulture)).Append(',').Append(y.ToString("0.#", CultureInfo.InvariantCulture)).Append(' ');
                line.Dots.Add(new SvgDot { X = x, Y = y, Title = $"{series.Name}: {FormatValue(value)} · {FormatTime(times[i], isHourly)}" });
            }

            line.Points = points.ToString().TrimEnd();

            return line;
        }

        private double XFor(int index, int count, double plotWidth)
        {
            return count <= 1 ? PaddingLeft + plotWidth / 2d : PaddingLeft + plotWidth * index / (count - 1);
        }

        private List<DateTime> CollectTimes(List<ChartSeries> series)
        {
            var times = new List<DateTime>();

            for (int i = 0; i < series.Count; i++)
            {
                for (int j = 0; j < series[i].Points.Count; j++)
                {
                    if (times.Contains(series[i].Points[j].Time) == false)
                        times.Add(series[i].Points[j].Time);
                }
            }

            times.Sort();

            return times;
        }

        private double FindMax(List<ChartSeries> series)
        {
            var max = 0d;

            for (int i = 0; i < series.Count; i++)
            {
                for (int j = 0; j < series[i].Points.Count; j++)
                    max = Math.Max(max, series[i].Points[j].Value);
            }

            return max <= 0d ? 1d : max * 1.1d;
        }

        private double ValueAt(ChartSeries series, DateTime time)
        {
            for (int i = 0; i < series.Points.Count; i++)
            {
                if (series.Points[i].Time == time)
                    return series.Points[i].Value;
            }

            return 0d;
        }

        private string FormatTime(DateTime time, bool isHourly)
        {
            return time.ToString(isHourly ? "dd.MM HH:00" : "dd.MM", CultureInfo.InvariantCulture);
        }

        private string FormatValue(double value)
        {
            if (1000000d <= value)
                return (value / 1000000d).ToString("0.#", CultureInfo.InvariantCulture) + "M";

            if (1000d <= value)
                return (value / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "K";

            return value.ToString(value < 10d ? "0.##" : "0", CultureInfo.InvariantCulture);
        }
    }
}
