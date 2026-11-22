using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json.Linq;

namespace Tests.Integration.Mongo
{
    internal sealed class SteamPaymentTestHandler : HttpMessageHandler
    {
        private int _initialized;
        private int _finalized;
        private string _orderId = string.Empty;

        public int Initialized => Volatile.Read(ref _initialized);
        public int Finalized => Volatile.Read(ref _finalized);
        public int QueryFailuresBeforeSuccess { get; set; }
        public int InitializationFailuresBeforeSuccess { get; set; }
        public bool LoseFinalizeResponse { get; set; } = true;
        public JArray ReportOrders { get; set; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.Contains("/AuthenticateUserTicket/", StringComparison.Ordinal))
                return Reply(new JObject { ["result"] = "OK", ["steamid"] = "76561197972751825" });

            if (path.Contains("/GetUserInfo/", StringComparison.Ordinal))
                return Reply(new JObject { ["currency"] = "USD", ["status"] = "Active" });

            if (path.Contains("/InitTxn/", StringComparison.Ordinal) && request.Content != null)
            {
                Interlocked.Increment(ref _initialized);
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                var parameters = QueryHelpers.ParseQuery(body);

                if (_orderId.Length == 0)
                    _orderId = parameters["orderid"].ToString();
                else
                    Assert.That(parameters["orderid"].ToString(), Is.EqualTo(_orderId));

                Assert.That(parameters["amount[0]"].ToString(), Is.EqualTo("199"));

                if (0 < InitializationFailuresBeforeSuccess)
                {
                    InitializationFailuresBeforeSuccess -= 1;

                    return Failure(4, "Initialization outcome unknown");
                }

                return Reply(new JObject { ["orderid"] = _orderId, ["transid"] = "10" });
            }

            if (path.Contains("/FinalizeTxn/", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _finalized);

                if (LoseFinalizeResponse)
                    throw new HttpRequestException("Simulated disconnect after Steam accepted finalization.");

                return Reply(new JObject { ["orderid"] = _orderId, ["transid"] = "10" });
            }

            if (path.Contains("/QueryTxn/", StringComparison.Ordinal))
            {
                if (0 < QueryFailuresBeforeSuccess)
                {
                    QueryFailuresBeforeSuccess -= 1;

                    return Failure(2, "Order not found");
                }

                return Reply(new JObject
                {
                    ["orderid"] = _orderId,
                    ["transid"] = "10",
                    ["steamid"] = "76561197972751825",
                    ["status"] = Finalized == 0 ? "Approved" : "Succeeded",
                    ["currency"] = "USD",
                    ["items"] = new JArray(new JObject
                    {
                        ["itemid"] = 1001,
                        ["qty"] = 1,
                        ["amount"] = 169,
                        ["vat"] = 30,
                        ["itemstatus"] = Finalized == 0 ? "Approved" : "Succeeded",
                    }),
                });
            }

            if (path.Contains("/GetReport/", StringComparison.Ordinal))
                return Reply(new JObject { ["count"] = ReportOrders.Count, ["orders"] = ReportOrders });

            throw new InvalidOperationException("Unexpected Steam provider call in test.");
        }

        private HttpResponseMessage Reply(JObject parameters)
        {
            var body = new JObject { ["response"] = new JObject { ["result"] = "OK", ["params"] = parameters } };

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json") };
        }

        private HttpResponseMessage Failure(int code, string description)
        {
            var body = new JObject
            {
                ["response"] = new JObject
                {
                    ["result"] = "Failure",
                    ["error"] = new JObject { ["errorcode"] = code, ["errordesc"] = description },
                },
            };

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json") };
        }
    }
}
