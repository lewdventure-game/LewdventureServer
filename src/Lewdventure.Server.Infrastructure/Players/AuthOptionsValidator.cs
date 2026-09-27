using Microsoft.Extensions.Options;

namespace Server.Infrastructure.Players
{
    internal sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
    {
        private const int MinimumSigningKeyLength = 32;

        public ValidateOptionsResult Validate(string? name, AuthOptions options)
        {
            if (options.Enabled == false)
                return ValidateOptionsResult.Success;

            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < MinimumSigningKeyLength)
                failures.Add($"Auth:SigningKey must be at least {MinimumSigningKeyLength} characters when Auth:Enabled is true.");

            if (string.IsNullOrWhiteSpace(options.Issuer))
                failures.Add("Auth:Issuer is required.");

            if (string.IsNullOrWhiteSpace(options.Audience))
                failures.Add("Auth:Audience is required.");

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
