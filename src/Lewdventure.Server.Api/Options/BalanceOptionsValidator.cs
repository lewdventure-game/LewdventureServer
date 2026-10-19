using Microsoft.Extensions.Options;

namespace Server.Api.Options
{
    internal sealed class BalanceOptionsValidator : IValidateOptions<BalanceOptions>
    {
        private readonly IHostEnvironment _hostEnvironment;

        public BalanceOptionsValidator(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public ValidateOptionsResult Validate(string? name, BalanceOptions options)
        {
            if (options.Enabled && _hostEnvironment.IsProduction())
                return ValidateOptionsResult.Fail("Balance:Enabled is not allowed in Production.");

            return ValidateOptionsResult.Success;
        }
    }
}
