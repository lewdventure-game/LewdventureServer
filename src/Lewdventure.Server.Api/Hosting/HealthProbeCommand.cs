namespace Server.Api.Hosting
{
    internal sealed class HealthProbeCommand
    {
        private const string CommandName = "--health-probe";
        private const string DefaultUrl = "http://127.0.0.1:9090/health/live";

        public bool IsRequested(string[] args)
        {
            return 0 < args.Length && string.Equals(args[0], CommandName, StringComparison.Ordinal);
        }

        public async Task<int> RunAsync(string[] args)
        {
            var url = 1 < args.Length ? args[1] : DefaultUrl;

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                using var response = await httpClient.GetAsync(url);

                return response.IsSuccessStatusCode ? 0 : 1;
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException)
            {
                return 1;
            }
        }
    }
}
