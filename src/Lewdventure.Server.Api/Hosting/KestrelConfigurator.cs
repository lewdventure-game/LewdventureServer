using Microsoft.AspNetCore.Server.Kestrel.Core;
using Server.Api.Options;

namespace Server.Api.Hosting
{
    internal sealed class KestrelConfigurator
    {
        private readonly ServerOptions _serverOptions;

        public KestrelConfigurator(ServerOptions serverOptions)
        {
            _serverOptions = serverOptions;
        }

        public void Configure(KestrelServerOptions kestrelServerOptions)
        {
            kestrelServerOptions.AddServerHeader = false;

            Listen(kestrelServerOptions, _serverOptions.PublicPort);
            Listen(kestrelServerOptions, _serverOptions.OpsPort);
        }

        private void Listen(KestrelServerOptions kestrelServerOptions, int port)
        {
            if (_serverOptions.BindAddress == BindAddressType.Loopback)
            {
                kestrelServerOptions.ListenLocalhost(port);

                return;
            }

            kestrelServerOptions.ListenAnyIP(port);
        }
    }
}
