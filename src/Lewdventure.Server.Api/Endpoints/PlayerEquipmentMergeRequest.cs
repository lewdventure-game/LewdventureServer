namespace Server.Api.Endpoints
{
    internal sealed class PlayerEquipmentMergeRequest
    {
        public string InstanceId { get; set; } = string.Empty;

        public List<string> PaymentInstanceIds { get; set; } = new();

        public string RequestId { get; set; } = string.Empty;
    }
}
