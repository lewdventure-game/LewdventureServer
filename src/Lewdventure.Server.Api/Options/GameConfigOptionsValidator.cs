using Microsoft.Extensions.Options;
using Server.Infrastructure.Mongo;

namespace Server.Api.Options
{
    internal sealed class GameConfigOptionsValidator : IValidateOptions<GameConfigOptions>
    {
        private readonly IHostEnvironment _hostEnvironment;
        private readonly MongoOptions _mongoOptions;

        public GameConfigOptionsValidator(IHostEnvironment hostEnvironment, IOptions<MongoOptions> mongoOptions)
        {
            _hostEnvironment = hostEnvironment;
            _mongoOptions = mongoOptions.Value;
        }

        public ValidateOptionsResult Validate(string? name, GameConfigOptions options)
        {
            var failures = new List<string>();

            if (options.Source == GameConfigSourceType.Unknown)
                failures.Add("GameConfig:Source must be File, GoogleSheets or Mongo.");

            if (options.Source == GameConfigSourceType.File && string.IsNullOrWhiteSpace(options.FilePath))
                failures.Add("GameConfig:FilePath is required when GameConfig:Source is File.");

            if (options.Source == GameConfigSourceType.Mongo && _mongoOptions.Enabled == false)
                failures.Add("GameConfig:Source Mongo requires Mongo:Enabled.");

            if (options.ReloadMode == GameConfigReloadMode.Unknown)
                failures.Add("GameConfig:ReloadMode must be Manual or Poll.");

            if (_hostEnvironment.IsProduction() && options.Source == GameConfigSourceType.GoogleSheets)
                failures.Add("GameConfig:Source GoogleSheets is not allowed in Production.");

            if (_hostEnvironment.IsProduction() && options.BootstrapFromGoogleSheetsIfEmpty)
                failures.Add("GameConfig:BootstrapFromGoogleSheetsIfEmpty is not allowed in Production.");

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
