namespace Server.Api.Endpoints
{
    internal sealed class PlayerSummonRequest
    {
        public int SummonId { get; set; }

        public string RequestId { get; set; } = string.Empty;
    }
}
