namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenResponse
    {
        public GoldenResponse(int statusCode, string body)
            : this(statusCode, body, string.Empty)
        {
        }

        public GoldenResponse(int statusCode, string body, string entityTag)
        {
            StatusCode = statusCode;
            Body = body;
            EntityTag = entityTag;
        }

        public int StatusCode { get; }

        public string Body { get; }

        public string EntityTag { get; }
    }
}
