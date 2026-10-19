namespace Server.Admin.Backend.Models
{
    public sealed class BalanceDistributionModel
    {
        public int Count { get; set; }

        public float Min { get; set; }

        public float Average { get; set; }

        public float Median { get; set; }

        public float P90 { get; set; }

        public float Max { get; set; }
    }
}
