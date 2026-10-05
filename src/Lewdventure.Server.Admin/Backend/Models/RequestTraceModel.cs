namespace Server.Admin.Backend.Models
{
    public sealed class RequestTraceModel
    {
        public DateTime CreatedAt { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string CorrelationId { get; set; } = string.Empty;

        public string Method { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string ClientVersion { get; set; } = string.Empty;

        public int StatusCode { get; set; }

        public long DurationMs { get; set; }

        public string RequestBody { get; set; } = string.Empty;

        public string ResponseBody { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;
    }
}
