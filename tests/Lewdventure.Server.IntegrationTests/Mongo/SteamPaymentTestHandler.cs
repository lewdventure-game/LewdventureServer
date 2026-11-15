using System.Net;
using System.Text;
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
                var parameters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(body);
                _orderId = parameters["orderid"].ToString();
                Assert.That(parameters["amount[0]"].ToString(), Is.EqualTo("199"));

                return Reply(new JObject { ["orderid"] = _orderId, ["transid"] = "10" });
            }

            if (path.Contains("/FinalizeTxn/", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _finalized);

                throw new HttpRequestException("Simulated disconnect after Steam accepted finalization.");
            }

            if (path.Contains("/QueryTxn/", StringComparison.Ordinal))
            {
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

            throw new InvalidOperationException("Unexpected Steam provider call in test.");
        }

        private HttpResponseMessage Reply(JObject parameters)
        {
            var body = new JObject { ["response"] = new JObject { ["result"] = "OK", ["params"] = parameters } };

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json") };
        }
    }
}
