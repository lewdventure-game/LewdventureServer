namespace Server.Api.Endpoints
{
    internal sealed class RunStageResponse
    {
        public int StageId { get; set; }

        public int EventId { get; set; }

        public bool IsBoss { get; set; }

        public bool Resolved { get; set; }
    }
}
