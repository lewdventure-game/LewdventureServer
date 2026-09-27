namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunBonusDocument
    {
        public int BonusId { get; set; }

        public int Count { get; set; }

        public int RemainingBattles { get; set; }
    }
}
