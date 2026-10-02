using System.Runtime.Serialization;

namespace Server.Equipments
{
    public enum EquipmentType
    {
        Unknown = 0,
        [EnumMember(Value = "body")]
        Body = 1,
        [EnumMember(Value = "pants")]
        Pants = 2,
        [EnumMember(Value = "boots")]
        Boots = 3,
        [EnumMember(Value = "necklace")]
        Necklace = 4,
    }
}
