namespace Server.Admin.Backend.Models
{
    public sealed class ServerErrorModel
    {
        public DateTime CreatedAt { get; set; }

        public string Level { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Exception { get; set; } = string.Empty;

        public string CorrelationId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;
    }
}
