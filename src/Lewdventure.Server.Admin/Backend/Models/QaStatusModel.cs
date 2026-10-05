namespace Server.Admin.Backend.Models
{
    public sealed class QaStatusModel
    {
        public bool CheatsEnabled { get; set; }

        public bool DiagnosticsEnabled { get; set; }

        public string Environment { get; set; } = string.Empty;
    }
}
