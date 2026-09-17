using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.GoogleSheets;

namespace Server.ConfigTool
{
    internal sealed class ConfigToolApplication
    {
        private const string Usage = "Usage: config-tool <import|validate|hash|diff> [--out file] [--credentials file] [--file file] [--from file] [--to file]";
        private const string EnvironmentVariable = "DOTNET_ENVIRONMENT";
        private const string LocalEnvironmentName = "Local";

        public async Task<int> RunAsync(string[] args)
        {
            var arguments = new CommandArguments(args);

            if (string.IsNullOrEmpty(arguments.Command))
            {
                Console.Error.WriteLine(Usage);

                return 2;
            }

            var environmentName = Environment.GetEnvironmentVariable(EnvironmentVariable);
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = Array.Empty<string>(),
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = string.IsNullOrWhiteSpace(environmentName) ? LocalEnvironmentName : environmentName,
            });

            if (arguments.TryGet("credentials", out var credentialsPath))
                builder.Configuration["GoogleSheets:CredentialsPath"] = Path.GetFullPath(credentialsPath);

            builder.Configuration["Logging:Console:FormatterName"] = "simple";
            builder.Configuration["Logging:LogLevel:Default"] = "Warning";
            builder.Configuration["Logging:LogLevel:Server.Bonuses.BonusWorkModeParser"] = "Error";
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(ConfigureConsole);
            RegisterServices(builder);

            using var host = builder.Build();

            try
            {
                return await ExecuteAsync(arguments, host.Services, builder.Environment);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is InvalidDataException || exception is OptionsValidationException)
            {
                Console.Error.WriteLine(exception.Message);

                return 2;
            }
        }

        private void ConfigureConsole(Microsoft.Extensions.Logging.Console.SimpleConsoleFormatterOptions options)
        {
            options.SingleLine = true;
        }

        private void RegisterServices(HostApplicationBuilder builder)
        {
            builder.Services.AddOptions<GoogleSheetsOptions>().Bind(builder.Configuration.GetSection(GoogleSheetsOptions.SectionName));
            builder.Services.AddSingleton<IValidateOptions<GoogleSheetsOptions>, GoogleSheetsOptionsValidator>();
            builder.Services.AddSingleton<ConfigDomainNames>();
            builder.Services.AddSingleton<ConfigSnapshotHasher>();
            builder.Services.AddSingleton<ConfigSnapshotSerializer>();
            builder.Services.AddSingleton<ConfigSnapshotValidator>();
            builder.Services.AddSingleton<ConfigSnapshotDiff>();
            builder.Services.AddSingleton<ConfigRowsParser>();
            builder.Services.AddSingleton<FileConfigSnapshotSource>();
            builder.Services.AddSingleton<GameConfigSetBuilder>();
            builder.Services.AddSingleton<IBonusWorkModeParser, BonusWorkModeParser>();
            builder.Services.AddSingleton<GoogleCredentialProvider>();
            builder.Services.AddSingleton<GoogleSheetsConfigImporter>();
            builder.Services.AddSingleton<ConfigToolCommands>();
        }

        private async Task<int> ExecuteAsync(CommandArguments arguments, IServiceProvider services, IHostEnvironment environment)
        {
            var commands = services.GetRequiredService<ConfigToolCommands>();

            switch (arguments.Command)
            {
                case "import":
                    return await commands.ImportAsync(arguments.GetRequired("out"), environment);
                case "validate":
                    return await commands.ValidateAsync(arguments.GetRequired("file"));
                case "hash":
                    return await commands.HashAsync(arguments.GetRequired("file"));
                case "diff":
                    return await commands.DiffAsync(arguments.GetRequired("from"), arguments.GetRequired("to"));
                default:
                    Console.Error.WriteLine(Usage);

                    return 2;
            }
        }
    }
}
