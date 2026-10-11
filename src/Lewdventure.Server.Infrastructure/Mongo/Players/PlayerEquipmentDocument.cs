namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerEquipmentDocument
    {
        public string InstanceId { get; set; } = string.Empty;

        public int ConfigId { get; set; }

        public int Level { get; set; } = 1;

        public int MergeNumber { get; set; }

        public DateTime ObtainedAt { get; set; }
    }
}
