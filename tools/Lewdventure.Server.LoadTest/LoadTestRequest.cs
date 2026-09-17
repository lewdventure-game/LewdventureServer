namespace Server.LoadTest
{
    internal sealed class LoadTestRequest
    {
        public LoadTestRequest(string name, string body, string expectedResponse)
        {
            Name = name;
            Body = body;
            ExpectedResponse = expectedResponse;
        }

        public string Name { get; }

        public string Body { get; }

        public string ExpectedResponse { get; }
    }
}
