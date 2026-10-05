namespace Server.Api.Endpoints
{
    internal sealed class QaAccountResponse
    {
        public string UserId { get; set; } = string.Empty;

        public bool IsQa { get; set; }

        public string Alias { get; set; } = string.Empty;

        public string MarkedBy { get; set; } = string.Empty;

        public DateTime? MarkedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
