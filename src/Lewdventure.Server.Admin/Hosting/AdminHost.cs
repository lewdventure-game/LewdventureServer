using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;
using Server.Admin.Accounts;
using Server.Admin.Backend;
using Server.Admin.Options;

namespace Server.Admin.Hosting
{
    internal sealed class AdminHost
    {
        private const string CookieName = "lewd_admin";

        public async Task RunAsync(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var options = new AdminPanelOptions();

            builder.Configuration.GetSection(AdminPanelOptions.SectionName).Bind(options);
            Directory.CreateDirectory(options.DataPath);

            Register(builder.Services, builder.Configuration, options);

            var application = builder.Build();

            Configure(application);

            await application.RunAsync();
        }

        private void Register(IServiceCollection services, IConfiguration configuration, AdminPanelOptions options)
        {
            services.AddOptions<AdminPanelOptions>()
                .Bind(configuration.GetSection(AdminPanelOptions.SectionName))
                .Validate(HasEnvironments, "AdminPanel:Environments must list at least one environment with Name, BaseUrl and ApiKey.")
                .ValidateOnStart();

            services.AddDataProtection()
                .SetApplicationName("lewdventure-admin")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(options.DataPath, "keys")));

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(cookie => ConfigureCookie(cookie, options));

            services.AddAuthorizationBuilder()
                .AddPolicy(AdminRoles.AdminPolicy, policy => policy.RequireRole(AdminRoles.Admin));

            services.AddRazorPages(pages =>
            {
                pages.Conventions.AuthorizeFolder("/");
                pages.Conventions.AllowAnonymousToPage("/Login");
                pages.Conventions.AllowAnonymousToPage("/Error");
            });

            services.Configure<ForwardedHeadersOptions>(ConfigureForwardedHeaders);
            services.Configure<WebEncoderOptions>(encoder => encoder.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
            services.AddHttpClient(GameAdminClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(20));

            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(new AdminAccountStore(options.DataPath));
            services.AddSingleton<AdminPasswordHasher>();
            services.AddSingleton<AdminRoles>();
            services.AddSingleton<AdminLoginThrottle>();
            services.AddSingleton<AdminEnvironmentSelector>();
            services.AddSingleton<GameAdminClient>();
            services.AddSingleton(CreateAuditLog);
        }

        private AdminAuditLog CreateAuditLog(IServiceProvider serviceProvider)
        {
            var options = serviceProvider.GetRequiredService<IOptions<AdminPanelOptions>>().Value;

            return new AdminAuditLog(options.DataPath, serviceProvider.GetRequiredService<ILogger<AdminAuditLog>>());
        }

        private bool HasEnvironments(AdminPanelOptions options)
        {
            if (options.Environments.Count == 0)
                return false;

            for (int i = 0; i < options.Environments.Count; i++)
            {
                var environment = options.Environments[i];

                if (string.IsNullOrWhiteSpace(environment.Name) || string.IsNullOrWhiteSpace(environment.BaseUrl) || string.IsNullOrWhiteSpace(environment.ApiKey))
                    return false;
            }

            return true;
        }

        private void ConfigureCookie(CookieAuthenticationOptions cookie, AdminPanelOptions options)
        {
            cookie.Cookie.Name = CookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.LoginPath = "/Login";
            cookie.AccessDeniedPath = "/Error";
            cookie.ExpireTimeSpan = TimeSpan.FromHours(options.SessionHours);
            cookie.SlidingExpiration = true;
        }

        private void ConfigureForwardedHeaders(ForwardedHeadersOptions forwarded)
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
            forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
            forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("127.0.0.0/8"));
            forwarded.KnownProxies.Add(IPAddress.IPv6Loopback);
        }

        private void Configure(WebApplication application)
        {
            application.UseForwardedHeaders();
            application.UseExceptionHandler("/Error");
            application.UseMiddleware<SecurityHeadersMiddleware>();
            application.UseStaticFiles();
            application.UseRouting();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapGet("/health", () => Results.Text("ok")).AllowAnonymous();
            new GrafanaAuthEndpoint().Map(application);
            application.MapRazorPages();
        }
    }
}
