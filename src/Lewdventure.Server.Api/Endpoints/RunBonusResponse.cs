namespace Server.Api.Endpoints
{
    internal sealed class RunBonusResponse
    {
        public int BonusId { get; set; }

        public int Count { get; set; }

        public int RemainingBattles { get; set; }
    }
}
