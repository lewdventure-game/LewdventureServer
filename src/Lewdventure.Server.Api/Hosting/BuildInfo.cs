using System.Reflection;

namespace Server.Api.Hosting
{
    internal sealed class BuildInfo
    {
        public BuildInfo()
        {
            var assembly = typeof(Program).Assembly;
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            Version = informationalVersion == null ? "unknown" : informationalVersion.InformationalVersion;
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        }

        public string Version { get; }

        public string Runtime { get; }
    }
}
