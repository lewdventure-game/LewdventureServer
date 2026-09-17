using Microsoft.Extensions.Options;

namespace Server.Api.Options
{
    internal sealed class GameConfigOptionsValidator : IValidateOptions<GameConfigOptions>
    {
        public ValidateOptionsResult Validate(string? name, GameConfigOptions options)
        {
            if (options.Source == GameConfigSourceType.Unknown)
                return ValidateOptionsResult.Fail("GameConfig:Source must be File or GoogleSheets.");

            if (options.Source == GameConfigSourceType.File && string.IsNullOrWhiteSpace(options.FilePath))
                return ValidateOptionsResult.Fail("GameConfig:FilePath is required when GameConfig:Source is File.");

            return ValidateOptionsResult.Success;
        }
    }
}
