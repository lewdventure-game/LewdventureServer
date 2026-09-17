using Microsoft.AspNetCore.Authentication;

namespace Server.Api.Security
{
    internal sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public bool Enabled { get; set; }

        public string ApiKey { get; set; } = string.Empty;

        public string HeaderName { get; set; } = string.Empty;

        public string LegacyHeaderName { get; set; } = string.Empty;
    }
}
