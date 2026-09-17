using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Server.Api.Security
{
    internal sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private readonly ApiKeyComparer _apiKeyComparer;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ApiKeyComparer apiKeyComparer)
            : base(options, logger, encoder)
        {
            _apiKeyComparer = apiKeyComparer;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Options.Enabled == false)
                return Task.FromResult(AuthenticateResult.NoResult());

            var provided = ReadHeader(Options.HeaderName);

            if (string.IsNullOrEmpty(provided) && string.IsNullOrEmpty(Options.LegacyHeaderName) == false)
            {
                provided = ReadHeader(Options.LegacyHeaderName);

                if (string.IsNullOrEmpty(provided) == false)
                    Logger.LogWarning("[Security] deprecated header used header = {Header} path = {Path}", Options.LegacyHeaderName, Request.Path);
            }

            if (string.IsNullOrEmpty(provided))
                return Task.FromResult(AuthenticateResult.NoResult());

            if (_apiKeyComparer.AreEqual(provided, Options.ApiKey) == false)
            {
                Logger.LogWarning("[Security] invalid api key scheme = {Scheme} path = {Path} remote = {Remote}", Scheme.Name, Request.Path, Context.Connection.RemoteIpAddress);

                return Task.FromResult(AuthenticateResult.Fail("Invalid api key."));
            }

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, Scheme.Name) }, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        private string ReadHeader(string headerName)
        {
            if (string.IsNullOrEmpty(headerName))
                return string.Empty;

            return Request.Headers[headerName].ToString();
        }
    }
}
