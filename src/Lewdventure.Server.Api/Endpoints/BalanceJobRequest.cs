using System.Text.Json;

namespace Server.Api.Endpoints
{
    internal sealed class BalanceJobRequest
    {
        public string Kind { get; set; } = string.Empty;

        public JsonElement Request { get; set; }
    }
}
