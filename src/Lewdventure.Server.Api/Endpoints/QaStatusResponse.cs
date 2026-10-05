namespace Server.Api.Endpoints
{
    internal sealed class QaStatusResponse
    {
        public bool CheatsEnabled { get; set; }

        public bool DiagnosticsEnabled { get; set; }

        public string Environment { get; set; } = string.Empty;
    }
}
