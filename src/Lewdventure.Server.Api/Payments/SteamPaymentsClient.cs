using System.Globalization;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server.Api.Payments
{
    internal sealed class SteamPaymentsClient
    {
        public const string TicketIdentity = "lewdventure-server";

        private readonly HttpClient _client;
        private readonly PaymentOptions _options;

        public SteamPaymentsClient(HttpClient client, IOptions<PaymentOptions> options)
        {
            _client = client;
            _options = options.Value;
        }

        private string Interface => _options.SteamSandbox ? "ISteamMicroTxnSandbox" : "ISteamMicroTxn";

        public async Task<string> AuthenticateAsync(string ticket, CancellationToken cancellationToken)
        {
            var response = await SendAsync(HttpMethod.Get, "ISteamUserAuth/AuthenticateUserTicket/v1/", new()
            {
                ["ticket"] = ticket,
                ["identity"] = TicketIdentity,
            }, cancellationToken);
            var parameters = response["params"] as JObject;

            if (parameters?.Value<string>("result") != "OK")
                return string.Empty;

            var subject = parameters.Value<string>("steamid");

            return ulong.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var steamId) && 0 < steamId
                ? steamId.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        public async Task<string> GetCurrencyAsync(string subject, CancellationToken cancellationToken)
        {
            var response = await SendAsync(HttpMethod.Get, Interface + "/GetUserInfo/v2/", new() { ["steamid"] = subject }, cancellationToken);
            var parameters = response["params"] as JObject;

            if (response.Value<string>("result") != "OK" || parameters?.Value<string>("status") == "Locked from purchasing")
                return string.Empty;

            return parameters?.Value<string>("currency") ?? string.Empty;
        }

        public Task<JObject> InitializeAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            return SendAsync(HttpMethod.Post, Interface + "/InitTxn/v3/", new()
            {
                ["orderid"] = order.ExternalId,
                ["steamid"] = order.Subject,
                ["itemcount"] = "1",
                ["language"] = "en",
                ["currency"] = order.Currency,
                ["usersession"] = "client",
                ["itemid[0]"] = order.SteamItemId.ToString(CultureInfo.InvariantCulture),
                ["qty[0]"] = "1",
                ["amount[0]"] = order.Amount.ToString(CultureInfo.InvariantCulture),
                ["description[0]"] = order.Name,
            }, cancellationToken);
        }

        public Task<JObject> QueryAsync(string orderId, CancellationToken cancellationToken)
        {
            return SendAsync(HttpMethod.Get, Interface + "/QueryTxn/v3/", new() { ["orderid"] = orderId }, cancellationToken);
        }

        public Task<JObject> FinalizeAsync(string orderId, CancellationToken cancellationToken)
        {
            return SendAsync(HttpMethod.Post, Interface + "/FinalizeTxn/v2/", new() { ["orderid"] = orderId }, cancellationToken);
        }

        public Task<JObject> RefundAsync(string orderId, CancellationToken cancellationToken)
        {
            return SendAsync(HttpMethod.Post, Interface + "/RefundTxn/v2/", new() { ["orderid"] = orderId }, cancellationToken);
        }

        public Task<JObject> GetReportAsync(DateTime time, CancellationToken cancellationToken)
        {
            return SendAsync(HttpMethod.Get, Interface + "/GetReport/v5/", new()
            {
                ["type"] = "SETTLEMENT",
                ["time"] = time.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["maxresults"] = "5000",
            }, cancellationToken);
        }

        private async Task<JObject> SendAsync(HttpMethod method, string path, Dictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            parameters["appid"] = _options.SteamAppId.ToString(CultureInfo.InvariantCulture);
            parameters["key"] = _options.SteamPublisherKey;

            using var content = new FormUrlEncodedContent(parameters);
            var target = method == HttpMethod.Get ? path + "?" + await content.ReadAsStringAsync(cancellationToken) : path;
            using var request = new HttpRequestMessage(method, target);

            if (method == HttpMethod.Post)
                request.Content = content;

            try
            {
                using var response = await _client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                using var source = new StringReader(payload);
                using var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None, MaxDepth = 24 };
                var root = JObject.Load(reader);

                return root["response"] as JObject ?? throw new SteamPaymentsException();
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException ||
                (exception is OperationCanceledException && cancellationToken.IsCancellationRequested == false))
            {
                throw new SteamPaymentsException();
            }
        }
    }
}
