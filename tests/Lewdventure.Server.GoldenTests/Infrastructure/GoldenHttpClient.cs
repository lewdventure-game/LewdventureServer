using System.Text;

namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenHttpClient
    {
        public const string SimulatePath = "/api/battle/simulate";
        public const string ReplayPath = "/api/battle/replay";
        public const string ConfigBundlePath = "/api/config/bundle";

        private readonly HttpClient _httpClient;

        public GoldenHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<GoldenResponse> GetAsync(string path, string entityTag)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);

            if (string.IsNullOrEmpty(entityTag) == false)
                request.Headers.TryAddWithoutValidation("If-None-Match", entityTag);

            using var response = await _httpClient.SendAsync(request);

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseTag = response.Headers.ETag == null ? string.Empty : response.Headers.ETag.ToString();

            return new GoldenResponse((int)response.StatusCode, responseBody, responseTag);
        }

        public async Task<GoldenResponse> PostAsync(string path, string body)
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(path, content);

            var responseBody = await response.Content.ReadAsStringAsync();

            return new GoldenResponse((int)response.StatusCode, responseBody);
        }
    }
}
