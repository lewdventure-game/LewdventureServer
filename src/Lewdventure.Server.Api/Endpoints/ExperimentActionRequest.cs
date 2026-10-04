namespace Server.Api.Endpoints
{
    internal sealed class ExperimentActionRequest
    {
        public string GroupId { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }
}
