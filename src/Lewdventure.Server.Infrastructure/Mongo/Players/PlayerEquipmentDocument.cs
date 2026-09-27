namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerEquipmentDocument
    {
        public string InstanceId { get; set; } = string.Empty;

        public int ConfigId { get; set; }

        public int Level { get; set; } = 1;

        public long ExpSpent { get; set; }

        public int Breaks { get; set; }

        public int MergeNumber { get; set; }

        public DateTime ObtainedAt { get; set; }
    }
}
