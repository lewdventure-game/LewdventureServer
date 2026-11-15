using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Server.Api.Payments
{
    internal sealed class NutakuPaymentsClient
    {
        private readonly HttpClient _client;
        private readonly PaymentOptions _options;

        public NutakuPaymentsClient(HttpClient client, IOptions<PaymentOptions> options)
        {
            _client = client;
            _options = options.Value;
        }

        public async Task<string> CheckStatusAsync(string paymentId, CancellationToken cancellationToken)
        {
            var host = _options.NutakuSandbox ? "https://sbox-gi.nutaku.com" : "https://gi.nutaku.com";
            var path = host + "/payment/" + Uri.EscapeDataString(_options.NutakuTitleId) + "/" + Uri.EscapeDataString(paymentId);
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("GI-NutakuS2sKey", _options.NutakuServerKey);

            try
            {
                using var response = await _client.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode == false)
                    return string.Empty;

                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

                return body.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("status", out var status) &&
                    status.ValueKind == JsonValueKind.String ? status.GetString() ?? string.Empty : string.Empty;
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException ||
                (exception is OperationCanceledException && cancellationToken.IsCancellationRequested == false))
            {
                return string.Empty;
            }
        }
    }
}
