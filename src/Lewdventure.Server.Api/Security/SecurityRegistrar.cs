using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Server.Infrastructure.Mongo.Players;
using Server.Api.Options;
using Server.Infrastructure.Players;

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
            services.AddSingleton<AccessTokenIssuer>();
            services.AddSingleton<PlayerIdentityReader>();

            services.AddAuthentication()
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(SecurityNames.AdminScheme, null)
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(SecurityNames.ConfigPublisherScheme, null)
                .AddJwtBearer(SecurityNames.PlayerScheme, ConfigurePlayerScheme);

            services.AddOptions<ApiKeyAuthenticationOptions>(SecurityNames.AdminScheme).Configure<IOptions<AdminOptions>>(ConfigureAdminScheme);
            services.AddOptions<ApiKeyAuthenticationOptions>(SecurityNames.ConfigPublisherScheme).Configure<IOptions<ConfigPublisherOptions>>(ConfigurePublisherScheme);

            services.AddAuthorizationBuilder()
                .AddPolicy(SecurityNames.AdminPolicy, BuildAdminPolicy)
                .AddPolicy(SecurityNames.ConfigPublisherPolicy, BuildPublisherPolicy)
                .AddPolicy(SecurityNames.PlayerPolicy, BuildPlayerPolicy);

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
            services.AddOptions<AuthOptions>().Bind(_configuration.GetSection(AuthOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

            services.AddSingleton<IValidateOptions<AdminOptions>, AccessKeyOptionsValidator>();
            services.AddSingleton<IValidateOptions<ConfigPublisherOptions>, AccessKeyOptionsValidator>();
            services.AddSingleton<IValidateOptions<ReverseProxyOptions>, ReverseProxyOptionsValidator>();
            services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
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

        private void ConfigurePlayerScheme(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions options)
        {
            var authOptions = new AuthOptions();

            _configuration.GetSection(AuthOptions.SectionName).Bind(authOptions);

            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = authOptions.Issuer,
                ValidAudience = authOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(CreateSigningMaterial(authOptions.SigningKey))),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnTokenValidated = ValidateAccountAsync,
            };
        }

        private async Task ValidateAccountAsync(Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext context)
        {
            var mongoOptions = context.HttpContext.RequestServices.GetRequiredService<IOptions<Server.Infrastructure.Mongo.MongoOptions>>().Value;

            if (mongoOptions.Enabled == false)
                return;

            var playerIdentityReader = context.HttpContext.RequestServices.GetRequiredService<PlayerIdentityReader>();
            var userId = playerIdentityReader.Read(context.Principal!);

            if (string.IsNullOrEmpty(userId))
            {
                context.Fail("Token has no subject.");

                return;
            }

            var userRepository = context.HttpContext.RequestServices.GetRequiredService<UserRepository>();
            var user = await userRepository.GetAsync(userId, context.HttpContext.RequestAborted);

            if (user == null)
            {
                context.Fail("Account does not exist.");

                return;
            }

            if (string.Equals(user.Status, UserDocument.ActiveStatus, StringComparison.Ordinal) == false)
                context.Fail($"Account status is {user.Status}.");
        }

        private string CreateSigningMaterial(string signingKey)
        {
            return string.IsNullOrEmpty(signingKey) ? new string('0', 32) : signingKey;
        }

        private void BuildPlayerPolicy(AuthorizationPolicyBuilder policy)
        {
            policy.AddAuthenticationSchemes(SecurityNames.PlayerScheme).RequireAuthenticatedUser();
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
