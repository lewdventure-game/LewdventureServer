using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Server.Api.Options;

namespace Server.Api.Security
{
    internal sealed class SecurityRegistrar
    {
        private readonly IConfiguration _configuration;

        public SecurityRegistrar(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Register(IServiceCollection services)
        {
            RegisterOptions(services);

            services.AddSingleton<ApiKeyComparer>();
            services.AddSingleton<BattleConcurrencyLimiter>();

            services.AddAuthentication()
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(SecurityNames.AdminScheme, null)
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(SecurityNames.ConfigPublisherScheme, null);

            services.AddOptions<ApiKeyAuthenticationOptions>(SecurityNames.AdminScheme).Configure<IOptions<AdminOptions>>(ConfigureAdminScheme);
            services.AddOptions<ApiKeyAuthenticationOptions>(SecurityNames.ConfigPublisherScheme).Configure<IOptions<ConfigPublisherOptions>>(ConfigurePublisherScheme);

            services.AddAuthorizationBuilder()
                .AddPolicy(SecurityNames.AdminPolicy, BuildAdminPolicy)
                .AddPolicy(SecurityNames.ConfigPublisherPolicy, BuildPublisherPolicy);

            services.AddRateLimiter(ConfigureRateLimiter);
            services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitOptions>>(ConfigureRateLimiterPolicies);
            services.AddOptions<KestrelServerOptions>().Configure<IOptions<RequestLimitsOptions>>(ConfigureKestrelLimits);
            services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ReverseProxyOptions>>(ConfigureForwardedHeaders);
        }

        private void RegisterOptions(IServiceCollection services)
        {
            services.AddOptions<AdminOptions>().Bind(_configuration.GetSection(AdminOptions.SectionName)).ValidateOnStart();
            services.AddOptions<ConfigPublisherOptions>().Bind(_configuration.GetSection(ConfigPublisherOptions.SectionName)).ValidateOnStart();
            services.AddOptions<RateLimitOptions>().Bind(_configuration.GetSection(RateLimitOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
            services.AddOptions<RequestLimitsOptions>().Bind(_configuration.GetSection(RequestLimitsOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
            services.AddOptions<ReverseProxyOptions>().Bind(_configuration.GetSection(ReverseProxyOptions.SectionName)).ValidateOnStart();

            services.AddSingleton<IValidateOptions<AdminOptions>, AccessKeyOptionsValidator>();
            services.AddSingleton<IValidateOptions<ConfigPublisherOptions>, AccessKeyOptionsValidator>();
            services.AddSingleton<IValidateOptions<ReverseProxyOptions>, ReverseProxyOptionsValidator>();
        }

        private void ConfigureAdminScheme(ApiKeyAuthenticationOptions options, IOptions<AdminOptions> adminOptions)
        {
            options.Enabled = adminOptions.Value.Enabled;
            options.ApiKey = adminOptions.Value.ApiKey;
            options.HeaderName = adminOptions.Value.HeaderName;
        }

        private void ConfigurePublisherScheme(ApiKeyAuthenticationOptions options, IOptions<ConfigPublisherOptions> publisherOptions)
        {
            options.Enabled = publisherOptions.Value.Enabled;
            options.ApiKey = publisherOptions.Value.ApiKey;
            options.HeaderName = publisherOptions.Value.HeaderName;
            options.LegacyHeaderName = publisherOptions.Value.LegacyHeaderName;
        }

        private void BuildAdminPolicy(AuthorizationPolicyBuilder policy)
        {
            policy.AddAuthenticationSchemes(SecurityNames.AdminScheme).RequireAuthenticatedUser();
        }

        private void BuildPublisherPolicy(AuthorizationPolicyBuilder policy)
        {
            policy.AddAuthenticationSchemes(SecurityNames.ConfigPublisherScheme).RequireAuthenticatedUser();
        }

        private void ConfigureRateLimiter(RateLimiterOptions options)
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        }

        private void ConfigureRateLimiterPolicies(RateLimiterOptions options, IOptions<RateLimitOptions> rateLimitOptions)
        {
            new RateLimiterConfigurator(rateLimitOptions.Value).Configure(options);
        }

        private void ConfigureKestrelLimits(KestrelServerOptions options, IOptions<RequestLimitsOptions> requestLimitsOptions)
        {
            options.Limits.MaxRequestBodySize = requestLimitsOptions.Value.MaxRequestBodyBytes;
        }

        private void ConfigureForwardedHeaders(ForwardedHeadersOptions options, IOptions<ReverseProxyOptions> reverseProxyOptions)
        {
            var proxy = reverseProxyOptions.Value;

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            for (int i = 0; i < proxy.KnownNetworks.Count; i++)
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(proxy.KnownNetworks[i]));

            for (int i = 0; i < proxy.KnownProxies.Count; i++)
                options.KnownProxies.Add(IPAddress.Parse(proxy.KnownProxies[i]));
        }
    }
}
