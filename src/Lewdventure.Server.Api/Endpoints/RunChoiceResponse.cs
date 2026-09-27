namespace Server.Api.Endpoints
{
    internal sealed class RunChoiceResponse
    {
        public string Kind { get; set; } = string.Empty;

        public List<int> Options { get; set; } = new();

        public int ChoiceCount { get; set; }
    }
}
