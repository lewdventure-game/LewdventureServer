using System.ComponentModel.DataAnnotations;

namespace Server.Api.Options
{
    internal sealed class QaDiagnosticsOptions
    {
        public const string SectionName = "QaDiagnostics";

        public bool Enabled { get; set; }

        [Range(256, 65536)]
        public int MaxBodyBytes { get; set; } = 4096;
    }
}
