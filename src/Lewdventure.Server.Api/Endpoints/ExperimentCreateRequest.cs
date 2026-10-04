namespace Server.Api.Endpoints
{
    internal sealed class ExperimentCreateRequest
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public List<ExperimentGroupRequest> Groups { get; set; } = new();
    }
}
