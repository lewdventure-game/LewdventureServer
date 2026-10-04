namespace Server.Admin.Backend.Models
{
    public sealed class PlayerEquipmentModel
    {
        public string InstanceId { get; set; } = string.Empty;

        public int ConfigId { get; set; }

        public int Level { get; set; }

        public int MergeNumber { get; set; }
    }
}
