using System.ComponentModel.DataAnnotations;

namespace Server.Api.Options
{
    internal sealed class RequestLimitsOptions
    {
        public const string SectionName = "RequestLimits";

        [Range(1024, 104857600)]
        public long MaxRequestBodyBytes { get; set; } = 1048576;

        [Range(1024, 104857600)]
        public long BattleMaxRequestBodyBytes { get; set; } = 262144;
    }
}
