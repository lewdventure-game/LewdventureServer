using Microsoft.Extensions.Options;
using Server.Infrastructure.Mongo;

namespace Server.Api.Options
{
    internal sealed class GameConfigOptionsValidator : IValidateOptions<GameConfigOptions>
    {
        private readonly MongoOptions _mongoOptions;

        public GameConfigOptionsValidator(IOptions<MongoOptions> mongoOptions)
        {
            _mongoOptions = mongoOptions.Value;
        }

        public ValidateOptionsResult Validate(string? name, GameConfigOptions options)
        {
            var failures = new List<string>();

            if (options.Source == GameConfigSourceType.Unknown)
                failures.Add("GameConfig:Source must be File or Mongo.");

            if (options.Source == GameConfigSourceType.File && string.IsNullOrWhiteSpace(options.FilePath))
                failures.Add("GameConfig:FilePath is required when GameConfig:Source is File.");

            if (options.Source == GameConfigSourceType.Mongo && _mongoOptions.Enabled == false)
                failures.Add("GameConfig:Source Mongo requires Mongo:Enabled.");

            if (options.ReloadMode == GameConfigReloadMode.Unknown)
                failures.Add("GameConfig:ReloadMode must be Manual or Poll.");

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
