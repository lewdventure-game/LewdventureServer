using Microsoft.Extensions.Options;

namespace Server.Infrastructure.Alerts
{
    internal sealed class AlertsOptionsValidator : IValidateOptions<AlertsOptions>
    {
        public ValidateOptionsResult Validate(string? name, AlertsOptions options)
        {
            if (options.Enabled == false)
                return ValidateOptionsResult.Success;

            if (Uri.TryCreate(options.DiscordWebhookUrl, UriKind.Absolute, out var uri) == false || uri.Scheme != Uri.UriSchemeHttps)
                return ValidateOptionsResult.Fail("Alerts:DiscordWebhookUrl must be an absolute https URL when Alerts:Enabled is true.");

            return ValidateOptionsResult.Success;
        }
    }
}
