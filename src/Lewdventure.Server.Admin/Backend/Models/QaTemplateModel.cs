namespace Server.Admin.Backend.Models
{
    public sealed class QaTemplateModel
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;

        public string SourceUserId { get; set; } = string.Empty;

        public string ConfigVersion { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; }

        public string Summary { get; set; } = string.Empty;
    }
}
