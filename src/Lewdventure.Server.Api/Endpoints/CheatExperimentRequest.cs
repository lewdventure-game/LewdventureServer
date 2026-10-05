namespace Server.Api.Endpoints
{
    internal sealed class CheatExperimentRequest
    {
        public string ExperimentId { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;
    }
}
