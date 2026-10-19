using System.Text.Json;

namespace Server.Admin.Backend.Models
{
    public sealed class BalanceJobModel
    {
        public string Id { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Actor { get; set; } = string.Empty;

        public int Done { get; set; }

        public int Total { get; set; }

        public string Error { get; set; } = string.Empty;

        public int StatusCode { get; set; }

        public string RunId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? FinishedAt { get; set; }

        public string Request { get; set; } = string.Empty;

        public JsonElement? Result { get; set; }
    }
}
