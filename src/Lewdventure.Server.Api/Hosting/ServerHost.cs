using Microsoft.Extensions.Options;
using Server.Api.Composition;
using Server.Api.Endpoints;
using Server.Api.Health;
using Server.Api.Http;
using Server.Api.Options;
using Server.Api.Security;

namespace Server.Api.Hosting
{
    internal sealed class ServerHost
    {
        private const string LocalEnvironmentName = "Local";

        public async Task RunAsync(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            if (builder.Environment.IsEnvironment(LocalEnvironmentName))
                builder.Configuration.AddUserSecrets(typeof(Program).Assembly, true);

            var serverOptions = BindServerOptions(builder.Configuration);

            builder.WebHost.ConfigureKestrel(new KestrelConfigurator(serverOptions).Configure);
            builder.Host.UseDefaultServiceProvider(ConfigureServiceProvider);
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            new ServerComposition(builder.Configuration).Register(builder.Services);

            var application = builder.Build();

            ConfigureMiddleware(application, serverOptions);
            MapEndpoints(application, serverOptions);

            await new GameConfigStartupLoader().RunAsync(application.Services);
            await application.RunAsync();
        }

        private ServerOptions BindServerOptions(IConfiguration configuration)
        {
            var serverOptions = new ServerOptions();

            configuration.GetSection(ServerOptions.SectionName).Bind(serverOptions);

            return serverOptions;
        }

        private void ConfigureServiceProvider(ServiceProviderOptions options)
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        }

        private void ConfigureMiddleware(WebApplication application, ServerOptions serverOptions)
        {
            var reverseProxyOptions = application.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;

            if (reverseProxyOptions.Enabled)
                application.UseForwardedHeaders();

            application.UseExceptionHandler(new UnhandledExceptionResponder().Configure);
            application.UseMiddleware<CorrelationIdMiddleware>();
            application.UseWhen(new HttpLoggingFilter().ShouldLog, ConfigureHttpLogging);

            if (serverOptions.EnableSwagger)
            {
                application.UseSwagger();
                application.UseSwaggerUI(ConfigureSwaggerUi);
            }

            application.UseRouting();
            application.UseMiddleware<OpsPortGuardMiddleware>();
            application.UseRateLimiter();
            application.UseAuthentication();
            application.UseAuthorization();
        }

        private void ConfigureHttpLogging(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseHttpLogging();
        }

        private void ConfigureSwaggerUi(Swashbuckle.AspNetCore.SwaggerUI.SwaggerUIOptions swaggerUiOptions)
        {
            swaggerUiOptions.SwaggerEndpoint("/swagger/v1/swagger.json", "Lewdventure Server API v1");
        }

        private void MapEndpoints(WebApplication application, ServerOptions serverOptions)
        {
            new SystemEndpoints().Map(application);
            new ConfigEndpoints().Map(application);
            new BattleEndpoints().Map(application);
            new HealthEndpoints(serverOptions).Map(application);
        }
    }
}
