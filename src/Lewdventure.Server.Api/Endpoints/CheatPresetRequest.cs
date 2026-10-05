namespace Server.Api.Endpoints
{
    internal sealed class CheatPresetRequest
    {
        public string Preset { get; set; } = string.Empty;

        public int Amount { get; set; } = 1;
    }
}
