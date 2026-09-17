using Server.Api.Hosting;

namespace Server
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var healthProbeCommand = new HealthProbeCommand();

            if (healthProbeCommand.IsRequested(args))
                return await healthProbeCommand.RunAsync(args);

            await new ServerHost().RunAsync(args);

            return 0;
        }
    }
}
