using Microsoft.Extensions.Options;

namespace Server.Api.Options
{
    internal sealed class AccessKeyOptionsValidator : IValidateOptions<AdminOptions>, IValidateOptions<ConfigPublisherOptions>
    {
        private const string LocalEnvironmentName = "Local";
        private const int LocalMinimumKeyLength = 8;
        private const int AdminMinimumKeyLength = 32;
        private const int PublisherMinimumKeyLength = 24;

        private readonly IHostEnvironment _hostEnvironment;

        public AccessKeyOptionsValidator(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public ValidateOptionsResult Validate(string? name, AdminOptions options)
        {
            if (options.Enabled == false)
                return ValidateOptionsResult.Success;

            var failures = new List<string>();

            ValidateKey(failures, "Admin", options.ApiKey, options.HeaderName, AdminMinimumKeyLength);

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }

        public ValidateOptionsResult Validate(string? name, ConfigPublisherOptions options)
        {
            if (options.Enabled == false)
                return ValidateOptionsResult.Success;

            var failures = new List<string>();

            ValidateKey(failures, "ConfigPublisher", options.ApiKey, options.HeaderName, PublisherMinimumKeyLength);

            if (_hostEnvironment.IsProduction())
                failures.Add("ConfigPublisher:Enabled is not allowed in Production.");

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }

        private void ValidateKey(List<string> failures, string section, string apiKey, string headerName, int minimumLength)
        {
            var requiredLength = _hostEnvironment.IsEnvironment(LocalEnvironmentName) ? LocalMinimumKeyLength : minimumLength;

            if (string.IsNullOrWhiteSpace(headerName))
                failures.Add($"{section}:HeaderName is required.");

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < requiredLength)
                failures.Add($"{section}:ApiKey must be at least {requiredLength} characters.");
        }
    }
}
