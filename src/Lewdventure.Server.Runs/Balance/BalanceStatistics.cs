namespace Server.Balance
{
    internal sealed class BalanceStatistics
    {
        public BalanceDistribution Distribute(List<float> values)
        {
            var distribution = new BalanceDistribution { Count = values.Count };

            if (values.Count == 0)
                return distribution;

            values.Sort();

            var sum = 0f;

            for (int i = 0; i < values.Count; i++)
                sum += values[i];

            distribution.Min = values[0];
            distribution.Max = values[values.Count - 1];
            distribution.Average = sum / values.Count;
            distribution.Median = values[(values.Count - 1) / 2];
            distribution.P90 = values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * 0.9) - 1)];

            return distribution;
        }
    }
}
