using System.ComponentModel.DataAnnotations;

namespace Server.Api.Options
{
    internal sealed class ServerOptions
    {
        public const string SectionName = "Server";

        [Range(1, 65535)]
        public int PublicPort { get; set; } = 5000;

        [Range(1, 65535)]
        public int OpsPort { get; set; } = 9090;

        public BindAddressType BindAddress { get; set; } = BindAddressType.Any;

        public bool EnableSwagger { get; set; }

        [Range(1, 600)]
        public int ShutdownTimeoutSeconds { get; set; } = 30;
    }
}
