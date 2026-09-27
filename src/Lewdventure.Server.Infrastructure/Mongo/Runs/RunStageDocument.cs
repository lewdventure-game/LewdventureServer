namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunStageDocument
    {
        public int StageId { get; set; }

        public int EventId { get; set; }

        public bool IsBoss { get; set; }

        public bool Resolved { get; set; }
    }
}
