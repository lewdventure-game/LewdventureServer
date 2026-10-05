namespace Server.Admin.Backend.Models
{
    public sealed class QaRunStageModel
    {
        public int Number { get; set; }

        public int StageId { get; set; }

        public int EventId { get; set; }

        public string EventType { get; set; } = string.Empty;

        public bool IsBoss { get; set; }

        public bool Resolved { get; set; }

        public bool IsCurrent { get; set; }

        public List<int> CandidateEventIds { get; set; } = new();
    }
}
