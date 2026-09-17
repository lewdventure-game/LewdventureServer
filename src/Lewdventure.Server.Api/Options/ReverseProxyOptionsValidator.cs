using System.Net;
using Microsoft.Extensions.Options;

namespace Server.Api.Options
{
    internal sealed class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
    {
        public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
        {
            var failures = new List<string>();

            for (int i = 0; i < options.KnownNetworks.Count; i++)
            {
                if (IPNetwork.TryParse(options.KnownNetworks[i], out _) == false)
                    failures.Add($"ReverseProxy:KnownNetworks:{i} is not a valid CIDR network.");
            }

            for (int i = 0; i < options.KnownProxies.Count; i++)
            {
                if (IPAddress.TryParse(options.KnownProxies[i], out _) == false)
                    failures.Add($"ReverseProxy:KnownProxies:{i} is not a valid IP address.");
            }

            if (options.Enabled && options.KnownNetworks.Count == 0 && options.KnownProxies.Count == 0)
                failures.Add("ReverseProxy:Enabled requires KnownNetworks or KnownProxies.");

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
