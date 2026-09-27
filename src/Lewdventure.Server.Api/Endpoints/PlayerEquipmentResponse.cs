namespace Server.Api.Endpoints
{
    internal sealed class PlayerEquipmentResponse
    {
        public string InstanceId { get; set; } = string.Empty;

        public int ConfigId { get; set; }

        public int Level { get; set; }

        public int MergeNumber { get; set; }
    }
}
