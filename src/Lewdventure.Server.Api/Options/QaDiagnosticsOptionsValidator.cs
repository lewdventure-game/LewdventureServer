using Microsoft.Extensions.Options;

namespace Server.Api.Options
{
    internal sealed class QaDiagnosticsOptionsValidator : IValidateOptions<QaDiagnosticsOptions>
    {
        private readonly IHostEnvironment _hostEnvironment;

        public QaDiagnosticsOptionsValidator(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public ValidateOptionsResult Validate(string? name, QaDiagnosticsOptions options)
        {
            if (options.Enabled && _hostEnvironment.IsProduction())
                return ValidateOptionsResult.Fail("QaDiagnostics:Enabled is not allowed in Production.");

            return ValidateOptionsResult.Success;
        }
    }
}
