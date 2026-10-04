namespace Server.Admin.Backend.Models
{
    public sealed class ExperimentPlayerModel
    {
        public string UserId { get; set; } = string.Empty;

        public string ExperimentId { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public DateTime? AssignedAt { get; set; }

        public string Country { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string ConfigVersion { get; set; } = string.Empty;
    }
}
