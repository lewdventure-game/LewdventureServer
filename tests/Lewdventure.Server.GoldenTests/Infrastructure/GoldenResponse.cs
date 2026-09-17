namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenResponse
    {
        public GoldenResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body;
        }

        public int StatusCode { get; }

        public string Body { get; }
    }
}
