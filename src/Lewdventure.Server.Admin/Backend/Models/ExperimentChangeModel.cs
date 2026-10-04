namespace Server.Admin.Backend.Models
{
    public sealed class ExperimentChangeModel
    {
        public string Id { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string ExperimentId { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public string Actor { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }
}
