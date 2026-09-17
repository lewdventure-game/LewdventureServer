using Microsoft.Extensions.Options;

namespace Server.Api.Options
{
    internal sealed class ServerOptionsValidator : IValidateOptions<ServerOptions>
    {
        private readonly IHostEnvironment _hostEnvironment;

        public ServerOptionsValidator(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public ValidateOptionsResult Validate(string? name, ServerOptions options)
        {
            var failures = new List<string>();

            if (options.PublicPort == options.OpsPort)
                failures.Add("Server:PublicPort and Server:OpsPort must differ.");

            if (options.BindAddress == BindAddressType.Unknown)
                failures.Add("Server:BindAddress must be Loopback or Any.");

            if (options.EnableSwagger && _hostEnvironment.IsProduction())
                failures.Add("Server:EnableSwagger is not allowed in Production.");

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
