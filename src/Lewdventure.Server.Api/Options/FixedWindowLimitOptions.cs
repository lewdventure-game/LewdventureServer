using System.ComponentModel.DataAnnotations;

namespace Server.Api.Options
{
    internal sealed class FixedWindowLimitOptions
    {
        [Range(1, 100000)]
        public int PermitLimit { get; set; } = 30;

        [Range(1, 3600)]
        public int WindowSeconds { get; set; } = 10;

        [Range(0, 1000)]
        public int QueueLimit { get; set; }
    }
}
