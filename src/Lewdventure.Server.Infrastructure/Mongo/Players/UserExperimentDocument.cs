namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class UserExperimentDocument
    {
        public string ExperimentId { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public DateTime AssignedAt { get; set; }

        public string Country { get; set; } = string.Empty;

        public bool Forced { get; set; }

        public string ForcedBy { get; set; } = string.Empty;
    }
}
