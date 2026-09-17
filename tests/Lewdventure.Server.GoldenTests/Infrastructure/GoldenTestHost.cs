namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenTestHost : IDisposable
    {
        private readonly GoldenWebApplicationFactory _factory;

        public GoldenTestHost()
        {
            Paths = new GoldenPaths();
            _factory = new GoldenWebApplicationFactory(Paths);
            Settings = new GoldenSettings();
            Catalog = new GoldenCaseCatalog(Paths);
            RequestBuilder = new GoldenRequestBuilder();
            Verifier = new GoldenVerifier(Paths, Settings);
            Client = new GoldenHttpClient(_factory.CreateClient());
        }

        public IServiceProvider Services => _factory.Services;

        public GoldenPaths Paths { get; }

        public GoldenSettings Settings { get; }

        public GoldenCaseCatalog Catalog { get; }

        public GoldenRequestBuilder RequestBuilder { get; }

        public GoldenVerifier Verifier { get; }

        public GoldenHttpClient Client { get; }

        public async Task<GoldenResponse> ReplayAsync(GoldenCase goldenCase, ulong seed)
        {
            var body = RequestBuilder.BuildReplayBody(goldenCase.RequestText, seed);

            return await Client.PostAsync(GoldenHttpClient.ReplayPath, body);
        }

        public void Dispose()
        {
            _factory.Dispose();
        }
    }
}
