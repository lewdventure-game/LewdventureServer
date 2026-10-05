using Microsoft.Extensions.Options;

namespace Server.Api.Options
{
    internal sealed class CheatOptionsValidator : IValidateOptions<CheatOptions>
    {
        private readonly IHostEnvironment _hostEnvironment;

        public CheatOptionsValidator(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public ValidateOptionsResult Validate(string? name, CheatOptions options)
        {
            if (options.Enabled && _hostEnvironment.IsProduction())
                return ValidateOptionsResult.Fail("Cheats:Enabled is not allowed in Production.");

            return ValidateOptionsResult.Success;
        }
    }
}
